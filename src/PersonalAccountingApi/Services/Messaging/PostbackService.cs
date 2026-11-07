using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.Data;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class PostbackService(
  ILineMessagingClient lineClient,
  AppDbContext db,
  ILogger<PostbackService> logger) : IPostbackService
{
  public async Task HandleUserPostbackAsync(PostbackPayload payload, CancellationToken ct)
  {
    if (!payload.Params.TryGetValue("action", out var action)
      || !payload.Params.TryGetValue("proposalId", out var idText)
      || !long.TryParse(idText, out var proposalId)
      || action is not ("confirm" or "cancel"))
    {
      logger.LogWarning("Unknown postback data {Data}", payload.Data);
      return;
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == payload.UserId, ct);
    if (user is null)
    {
      logger.LogWarning($"User UserId={payload.UserId} not found.");
      return;
    }

    string replyText;
    try
    {
      replyText = action == "confirm"
        ? await ConfirmAsync(user.Id, proposalId, ct)
        : await CancelAsync(user.Id, proposalId, ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "Postback failed for UserId={UserId}, ProposalId={ProposalId}", user.Id, proposalId);
      db.ChangeTracker.Clear();
      replyText = "記錄失敗，請再按一次確認，或重新輸入。";
    }

    await ReplyAndSaveAsync(user.Id, payload.ReplyToken, replyText, ct);
  }

  private Task<int> ClaimAsync(long userId, long proposalId, ProposalStatus to, CancellationToken ct) =>
    db.Proposals
      .Where(p => p.Id == proposalId
        && p.Status == ProposalStatus.Pending
        && db.Messages.Any(m => m.Id == p.MessageId && m.UserId == userId))
      .ExecuteUpdateAsync(s => s
        .SetProperty(p => p.Status, to)
        .SetProperty(p => p.DecidedAt, DateTimeOffset.UtcNow), ct);

  private async Task<string> CancelAsync(long userId, long proposalId, CancellationToken ct) =>
    await ClaimAsync(userId, proposalId, ProposalStatus.Cancelled, ct) == 0
      ? PostbackMessage.AlreadyHandled
      : PostbackMessage.Cancelled;

  private async Task<string> ConfirmAsync(long userId, long proposalId, CancellationToken ct)
  {
    // claim + inserts commit together, so a failed insert rolls the claim back and the card stays tappable
    await using var tx = await db.Database.BeginTransactionAsync(ct);

    if (await ClaimAsync(userId, proposalId, ProposalStatus.Confirmed, ct) == 0) return PostbackMessage.AlreadyHandled;

    var proposal = await db.Proposals.AsNoTracking().FirstAsync(p => p.Id == proposalId, ct);
    string result;
    switch (proposal.Content.Type)
    {
      case ProposalType.Transactions:
        result = await InsertTransactionsAsync(proposal, ct);
        break;
      case ProposalType.Reversal:
        result = await InsertReversalAsync(userId, proposal, ct);
        break;
      default:
        throw new InvalidOperationException($"Unknown proposal type {proposal.Content.Type}");
    }

    // set the original message to is_transaction
    await db.Messages.Where(m => m.Id == proposal.MessageId)
      .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsTransaction, true), ct);

    await tx.CommitAsync(ct); // deferred balance triggers fire here, so an unbalanced transaction throws here
    return result;
  }

  private async Task<string> InsertTransactionsAsync(Proposal proposal, CancellationToken ct)
  {
    var txs = proposal.Content.Transactions!;
    var names = txs.SelectMany(t => t.Entries.Select(e => e.AccountName)).Distinct().ToList();
    var accounts = await db.Accounts.Where(a => names.Contains(a.Name)).ToDictionaryAsync(a => a.Name, ct);

    foreach (var t in txs)
    {
      var header = new Transaction
      {
        MessageId = proposal.MessageId,
        Description = t.Description,
        Date = t.Date,
        Kind = Enum.Parse<TransactionKind>(t.Kind.ToString()),
        Note = t.Note,
      };
      db.Transactions.Add(header);
      await db.SaveChangesAsync(ct); // need header.Id before adding entries

      foreach (var e in t.Entries)
      {
        var acc = accounts[e.AccountName]; // missing name throws -> generic failure
        db.JournalEntries.Add(new JournalEntry
        {
          TransactionId = header.Id,
          AccountId = acc.Id,
          Amount = e.Side == acc.NormalBalance ? e.Amount : -e.Amount,
        });
      }
      await db.SaveChangesAsync(ct);
    }
    return txs.Length == 1 ? PostbackMessage.Recorded : PostbackMessage.RecordedMultiple(txs.Length);
  }

  private async Task<string> InsertReversalAsync(long userId, Proposal proposal, CancellationToken ct)
  {
    var req = proposal.Content.Reversal!;

    // ownership again: never trust an id that came from a stored payload
    var original = await (
      from t in db.Transactions
      join m in db.Messages on t.MessageId equals m.Id
      where t.Id == req.TransactionId && m.UserId == userId
      select t).FirstAsync(ct);

    var entries = await db.JournalEntries.Where(j => j.TransactionId == original.Id).ToListAsync(ct);

    var header = new Transaction
    {
      MessageId = proposal.MessageId,
      Description = $"沖銷：{original.Description}",
      Date = original.Date,
      Kind = TransactionKind.Reversal,
      ReversesTransactionId = original.Id,
      Note = req.Note,
    };
    db.Transactions.Add(header);
    await db.SaveChangesAsync(ct);

    foreach (var j in entries)
      db.JournalEntries.Add(new JournalEntry { TransactionId = header.Id, AccountId = j.AccountId, Amount = -j.Amount });
    await db.SaveChangesAsync(ct);

    return PostbackMessage.Reversed;
  }

  private async Task ReplyAndSaveAsync(long userId, string replyToken, string text, CancellationToken ct)
  {
    try
    {
      LineMessage message = new TextMessage(text);
      var response = await lineClient.ReplyAsync(replyToken, [message], ct);

      db.Messages.Add(new Message
      {
        UserId = userId,
        Role = MessageRole.Bot,
        LineMessageId = response.SentMessages[0].Id,
        TextContent = $"[SYSTEM_REPLY] {text}",
        Type = MessageType.Text,
      });
      await db.SaveChangesAsync(ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "Failed to reply postback result to UserId={UserId}", userId);
    }
  }
}
