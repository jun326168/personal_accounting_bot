using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.Data;
using PersonalAccountingApi.Infrastructure.ExternalApis;
using PersonalAccountingApi.Infrastructure.AiPrompts;
using PersonalAccountingApi.Infrastructure.AiTools;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class MessageService(
  ILineMessagingClient lineClient,
  IAiClient aiClient,
  IEnumerable<IAiTool> tools,
  AppDbContext db,
  ILogger<MessageService> logger) : IMessageService
{
  private const int HistoryLimit = 12;
  private const int MaxIterations = 6;
  private const string FallbackText = "我暫時處理不了這個請求，請換個說法再試一次。";
  private readonly Dictionary<string, IAiTool> toolsByName = tools.ToDictionary(t => t.Definition.Name);

  private static readonly JsonSerializerOptions LineJson = new(JsonSerializerDefaults.Web)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public async Task HandleUserMessageAsync(MessagePayload payload, CancellationToken ct)
  {
    // return if it's non text
    if (payload.MessageType != "text") return;

    // check if it's a redeliver
    var message = await db.Messages.FirstOrDefaultAsync(m => m.LineMessageId == payload.MessageId, ct);
    if (message is not null) return;

    // send the reply message first and let ai process the real response
    try
    {
      await lineClient.ReplyAsync(payload.ReplyToken, [ThinkingMessage.BuildThinkingMessage()], ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "Failed to reply response to LineUserId={LineUserId}", payload.UserId);
      return;
    }

    // create user if not exist, which is unlikely to happen, check it again for the db logic
    var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == payload.UserId, ct);
    if (user == null)
    {
      user = new User { LineUserId = payload.UserId };
      db.Users.Add(user);
      await db.SaveChangesAsync(ct); // populates user.Id automatically
    }

    // save user message
    var userMessage = new Message
    {
      UserId = user.Id,
      Role = MessageRole.User,
      LineMessageId = payload.MessageId,
      TextContent = payload.Text,
      Type = MessageType.Text,
    };
    db.Messages.Add(userMessage);
    await db.SaveChangesAsync(ct);

    // load messages history and create ai response
    AssistantReply reply;
    try
    {
      var history = await LoadHistoryAsync(user.Id, ct);

      var accounts = await db.Accounts.AsNoTracking().OrderBy(a => a.Type).ThenBy(a => a.Id).ToArrayAsync(ct);
      var taipeiNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Taipei");
      var systemPrompt = SystemPromptBuilder.Build(accounts, taipeiNow);

      reply = await CreateAiResponseAsync(systemPrompt, history, new ToolContext(user.Id, userMessage.Id), ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      // don't rethrow: LINE would redeliver. Don't leave the user on "thinking" either
      logger.LogError(ex, "AI response failed for UserId={UserId}", user.Id);
      reply = new AnswerReply(FallbackText);
    }

    // push ai response (a proposal card or a text message)
    var (lineMessage, text, type) = await RenderAsync(reply, ct);
    LineSendMessageResponse sent;
    try
    {
      sent = await lineClient.PushAsync(user.LineUserId, [lineMessage], ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "Failed to push response to UserId={UserId}", user.Id);
      return; // nothing was delivered, so nothing to save
    }

    // save ai message
    try
    {
      var lineId = sent.SentMessages.FirstOrDefault()?.Id;
      if (lineId is null) { logger.LogWarning("Push returned no message id for UserId={UserId}", user.Id); return; }

      db.Messages.Add(new Message
      {
        UserId = user.Id,
        Role = MessageRole.Bot,
        LineMessageId = lineId,
        TextContent = text,
        JsonContent = type == MessageType.Text ? null : JsonSerializer.SerializeToNode<LineMessage>(lineMessage, LineJson),
        Type = type,
      });
      await db.SaveChangesAsync(ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "Failed to save bot message for UserId={UserId}", user.Id);
    }
  }

  // load history
  private async Task<List<AiMessage>> LoadHistoryAsync(long userId, CancellationToken ct)
  {
    // newest N rows with text, then back to oldest-first. Includes the message just saved
    var rows = await db.Messages
      .AsNoTracking()
      .Where(m => m.UserId == userId && !string.IsNullOrWhiteSpace(m.TextContent))
      .OrderByDescending(m => m.Id)
      .Take(HistoryLimit)
      .Select(m => new { m.Role, m.TextContent })
      .ToListAsync(ct);
    rows.Reverse();

    var history = new List<AiMessage>();
    foreach (var row in rows.SkipWhile(r => r.Role == MessageRole.Bot)) // must start with a user turn
    {
      var role = row.Role == MessageRole.User ? AiRole.User : AiRole.Assistant;

      // keep roles alternating: merge consecutive same-role rows
      if (history.Count > 0 && history[^1].Role == role)
        history[^1] = history[^1] with { Text = history[^1].Text + "\n" + row.TextContent };
      else
        history.Add(new AiMessage(role, row.TextContent));
    }
    return history;
  }

  // get ai response
  private async Task<AssistantReply> CreateAiResponseAsync(
    string systemPrompt, List<AiMessage> messages, ToolContext ctx, CancellationToken ct)
  {
    var definitions = toolsByName.Values.Select(t => t.Definition).ToList();

    for (var i = 0; i < MaxIterations; i++)
    {
      var turn = await aiClient.CompleteAsync(systemPrompt, messages, definitions, ct);

      if (turn is AiTextTurn text)
        return new AnswerReply(string.IsNullOrWhiteSpace(text.Text) ? FallbackText : text.Text);

      var toolTurn = (AiToolCallTurn)turn;
      messages.Add(new AiMessage(AiRole.Assistant, null, toolTurn.Calls, ProviderData: toolTurn.ProviderData));

      foreach (var call in toolTurn.Calls)
      {
        switch (await RunToolAsync(call, ctx, ct))
        {
          case ToolCompleted done:
            return new ProposalReply(done.ProposalId); // summary is filled in when rendering
          case ToolOutput output:
            messages.Add(new AiMessage(AiRole.Tool, output.Content, ToolCallId: call.Id, IsError: output.IsError));
            break;
        }
      }
    }

    logger.LogWarning("AI loop hit {Max} iterations for UserId={UserId}", MaxIterations, ctx.UserId);
    return new AnswerReply(FallbackText);
  }

  // execute tool
  private async Task<AiToolResult> RunToolAsync(AiToolCall call, ToolContext ctx, CancellationToken ct)
  {
    if (!toolsByName.TryGetValue(call.Name, out var tool))
      return AiToolHelper.Error($"Unknown tool '{call.Name}'.");

    try
    {
      return await tool.ExecuteAsync(call, ctx, ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "Tool {Tool} failed", call.Name);
      db.ChangeTracker.Clear(); // a failed SaveChanges leaves bad entities that would be retried by the next save
      return AiToolHelper.Error("The tool failed unexpectedly. Do not retry it; tell the user you could not complete the request.");
    }
  }

  // render the message
  private async Task<(LineMessage Message, string Text, MessageType Type)> RenderAsync(AssistantReply reply, CancellationToken ct)
  {
    if (reply is ProposalReply p)
    {
      var summary = await BuildProposalSummaryAsync(p.ProposalId, ct);
      if (summary is not null)
        return (ProposalMessage.Build(p.ProposalId, summary), summary, MessageType.FlexMessage);

      logger.LogWarning("Could not build summary for ProposalId={ProposalId}", p.ProposalId);
    }

    var text = reply is AnswerReply a ? a.Text : FallbackText;
    if (text.Length > 5000) text = text[..4999] + "…"; // LINE text message limit
    return (new TextMessage(text), text, MessageType.Text);
  }

  // get proposal data from the id and format the message
  private async Task<string?> BuildProposalSummaryAsync(long proposalId, CancellationToken ct)
  {
    var proposal = await db.Proposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proposalId, ct);
    if (proposal is null) return null;

    var content = proposal.Content;

    if (content.Type == ProposalType.Transactions && content.Transactions is { Length: > 0 } txs)
    {
      var zh = await db.Accounts.AsNoTracking()
        .ToDictionaryAsync(a => a.Name, a => a.NameZh ?? a.Name, StringComparer.OrdinalIgnoreCase, ct);
      return ProposalMessage.FormatTransactions(txs, zh);
    }

    if (content.Type == ProposalType.Reversal && content.Reversal is { } rev)
    {
      var target = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == rev.TransactionId, ct);
      if (target is null) return null;

      // total = sum of the debit-side entries (stored amounts are signed relative to the account's normal balance)
      var entries = await (
        from j in db.JournalEntries
        join a in db.Accounts on j.AccountId equals a.Id
        where j.TransactionId == target.Id
        select new { a.NormalBalance, j.Amount }
      ).ToListAsync(ct);
      var total = entries
        .Where(e => (e.NormalBalance == NormalBalance.Debit) == (e.Amount >= 0))
        .Sum(e => Math.Abs(e.Amount));

      return ProposalMessage.FormatReversal(target, total, rev.Note);
    }

    return null;
  }
}
