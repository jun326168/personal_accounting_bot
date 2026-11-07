using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Infrastructure.Data;

namespace PersonalAccountingApi.Infrastructure.AiTools;

public sealed record ProposeReversalArgs(long TransactionId, string? Note);

public sealed class ProposeReversalTool(AppDbContext db) : IAiTool
{
  public AiToolDefinition Definition { get; } = new(
    Name: "propose_reversal",
    Description:
      """
      Propose reversing (undoing) an existing transaction, for the user to confirm. Use when the user wants to cancel, delete, or undo something already recorded (e.g. "cancel that", "I entered the lunch twice", "delete yesterday's taxi"). This does NOT change anything: the user sees a confirmation card and must approve first. You must not claim that something is recorded unless a [SYSTEM_REPLY] in the history says so or a tool result shows it.
      You need the exact transaction id. If the user does not give one, call search_transactions first to find it, and if more than one transaction could match, ask the user which one instead of guessing. Only one transaction can be reversed per call. Entries are never edited or deleted; a reversal adds offsetting entries. A transaction that has already been reversed, or that is itself a reversal, cannot be reversed again.
      Do not use this to record a new transaction (use propose_transactions).
      """,
    ParametersSchema: JsonNode.Parse("""
      {
        "type": "object",
        "properties": {
          "transactionId": { "type": "integer", "description": "The id of the transaction to reverse, taken from search_transactions results. Never guess or invent an id." },
          "note": { "type": "string", "description": "Optional short reason for the reversal in the user's language, e.g. '重複輸入'. Omit if the user gave none." }
        },
        "required": ["transactionId"]
      }
      """)!.AsObject());

  public async Task<AiToolResult> ExecuteAsync(AiToolCall call, ToolContext ctx, CancellationToken ct)
  {
    ProposeReversalArgs? args;
    try
    {
      args = call.Arguments.Deserialize<ProposeReversalArgs>(AiJson.Options);
    }
    catch (JsonException ex)
    {
      return AiToolHelper.Error($"Invalid Arguments: {ex.Message}");
    }
    if (args is null || args.TransactionId <= 0) return AiToolHelper.Error("Transaction id is required.");

    // check if the transaction exists
    var reversingTransaction = await (
      from t in db.Transactions
      join m in db.Messages on t.MessageId equals m.Id
      where m.UserId == ctx.UserId && t.Id == args.TransactionId
      select new
      {
        t.Kind,
        IsReversed = db.Transactions.Any(r => r.ReversesTransactionId == t.Id),
      }
    ).FirstOrDefaultAsync(ct);
    if (reversingTransaction is null) 
      return AiToolHelper.Error($"Transaction {args.TransactionId} not found.");
    // check if is itself a reversal or it's already reversed 
    if (reversingTransaction.Kind == TransactionKind.Reversal) 
      return AiToolHelper.Error($"Transaction {args.TransactionId} is itself a reversal.");
    if (reversingTransaction.IsReversed)
      return AiToolHelper.Error($"Transaction {args.TransactionId} is already reversed.");
    // check if it's opening
    if (reversingTransaction.Kind == TransactionKind.Opening)
      return AiToolHelper.Error("The opening balance cannot be reversed. Use an 'adjusting' transaction to correct a balance.");

    // set the validated content
    var note = string.IsNullOrWhiteSpace(args.Note) ? null : args.Note.Trim();
    var content = new ProposalContent(ProposalType.Reversal, Reversal: new ReversalRequest(args.TransactionId, note));

    var proposal = new Proposal {
      MessageId = ctx.MessageId,
      Status = ProposalStatus.Pending,
      Content = content
    };

    // insert the proposal
    db.Proposals.Add(proposal);
    await db.SaveChangesAsync(ct);

    return new ToolCompleted(proposal.Id);
  }
}
