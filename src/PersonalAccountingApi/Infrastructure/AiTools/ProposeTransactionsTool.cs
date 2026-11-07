using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Infrastructure.Data;

namespace PersonalAccountingApi.Infrastructure.AiTools;

public sealed record ProposeTransactionsArgs(ProposalTransaction[] Transactions);

public sealed class ProposeTransactionsTool(AppDbContext db) : IAiTool
{
  public AiToolDefinition Definition { get; } = new(
    Name: "propose_transactions",
    Description:
      """
      Propose one or more new transactions for the user to confirm. Use when the user describes spending, income, a transfer, or a payment that should be recorded (e.g. "lunch 120", "got paid 50000", "paid off my credit card"). This does NOT save anything: the user sees a confirmation card and must approve first. You must not claim that something is recorded unless a [SYSTEM_REPLY] in the history says so or a tool result shows it.
      Call it once per user message, putting every transaction from that message in the same call. Do not call it if the amount or the account is unclear; ask the user instead. To undo or correct an existing transaction, use propose_reversal instead.
      3 kinds of transactions, default to normal, opening only once, corrections use adjusting.
      Each transaction is double-entry: its entries must balance, with total debits equal to total credits.
      """,
    ParametersSchema: JsonNode.Parse("""
      {
        "type": "object",
        "properties": {
          "transactions": {
            "type": "array",
            "description": "All transactions to propose from this message. Usually one; use several when the user lists multiple items.",
            "items": { 
              "type": "object",
              "properties": {
                "date": { "type": "string", "description": "Date of the transaction, YYYY-MM-DD. Resolve relative dates like 'yesterday' or 'last Friday' using today's date. Use today if the user gives no date." },
                "kind": {
                  "type": "string",
                  "enum": ["normal", "opening", "adjusting"],
                  "description": "Default 'normal' (spending, income, transfers). Use 'opening' only when the user states their starting balances, e.g. 'I currently have 5000 in cash'. Use 'adjusting' only when the user corrects a balance rather than reporting an event, e.g. 'my cash is actually 800, not 1000'. Never use for undoing a transaction; that is propose_reversal."
                },
                "description": { "type": "string", "description": "Short label for the transaction in the user's language, e.g. '午餐'. Not a sentence." },
                "note": { "type": "string", "description": "Optional extra detail the user mentioned, such as the merchant or reason. Omit if there is none. Adjusting transactions need a note." },
                "entries": {
                  "type": "array",
                  "description": "The journal entries. At least 2. Total debits must equal total credits.",
                  "items": { 
                    "type": "object",
                    "properties": {
                      "accountName": { "type": "string", "description": "Exact account name from the accounts list. Never invent a name." },
                      "side": { "type": "string", "enum": ["debit", "credit"], "description": "'debit' or 'credit'. In most of the cases (despite some situations like contra account), debit increases assets and expenses and credit increases liabilities, equity and revenue." },
                      "amount": { "type": "integer", "description": "Positive number in the user's currency, with no currency symbol. Never negative; use side to express direction. Integer only." }
                    },
                    "required": ["accountName", "side", "amount"]
                  }
                }
              },
              "required": ["date", "kind", "description", "entries"]
            }
          }
        },
        "required": ["transactions"]
      }
      """)!.AsObject());

  public async Task<AiToolResult> ExecuteAsync(AiToolCall call, ToolContext ctx, CancellationToken ct)
  {
    ProposeTransactionsArgs? args;
    try
    {
      args = call.Arguments.Deserialize<ProposeTransactionsArgs>(AiJson.Options);
    }
    catch (JsonException ex)
    {
      return AiToolHelper.Error($"Invalid Arguments: {ex.Message}");
    }
    if (args?.Transactions is not { Length: > 0 }) return AiToolHelper.Error("At least one transaction is required.");

    // opening checks
    var hasOpening = await (
      from t in db.Transactions
      join m in db.Messages on t.MessageId equals m.Id
      where m.UserId == ctx.UserId && t.Kind == TransactionKind.Opening
      select t
    ).AnyAsync(ct);
    if (hasOpening && args.Transactions.Any(t => t.Kind == ProposedKind.Opening))
      return AiToolHelper.Error("An opening balance already exists. Use kind 'adjusting' to correct a balance, or the default 'normal'.");

    if (args.Transactions.Where(t => t.Kind == ProposedKind.Opening).Skip(1).Any())
      return AiToolHelper.Error("A user can only have one opening transaction.");

    // lookup: any casing/whitespace the model sends -> the canonical account name
    var accountLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var n in await db.Accounts.Select(a => a.Name).ToListAsync(ct))
      accountLookup.TryAdd(n, n);
      
    // validate, and build the normalised copy that gets stored
    var normalised = new List<ProposalTransaction>();
    for (var i = 0; i < args.Transactions.Length; i++)
    {
      var t = args.Transactions[i];
      var label = $"Transaction {i + 1} ('{t.Description}')";

      if (string.IsNullOrWhiteSpace(t.Description)) // description
        return AiToolHelper.Error($"{label}: description cannot be empty.");
      if (t.Entries is not { Length: >= 2 }) // at least 2 entries
        return AiToolHelper.Error($"{label}: at least 2 entries are required.");
      if (t.Kind == ProposedKind.Adjusting && string.IsNullOrWhiteSpace(t.Note)) // adjusting requires note
        return AiToolHelper.Error($"{label}: an adjusting transaction needs a note explaining why.");
      
      var entries = new List<ProposalJournalEntry>();
      foreach (var e in t.Entries)
      {
        // check account name
        if (!accountLookup.TryGetValue(e.AccountName?.Trim() ?? "", out var canonical))
          return AiToolHelper.Error(
            $"{label}: account '{e.AccountName}' not found. Valid accounts: {string.Join(", ", accountLookup.Values)}.");
        // check amount > 0
        if (e.Amount <= 0 || e.Amount != decimal.Truncate(e.Amount))
          return AiToolHelper.Error($"{label}: amount {e.Amount} for '{canonical}' must be a positive whole number.");
        entries.Add(e with { AccountName = canonical });
      }
      // check if total debits = total credits
      var debits = entries.Where(e => e.Side == NormalBalance.Debit).Sum(e => e.Amount);
      var credits = entries.Where(e => e.Side == NormalBalance.Credit).Sum(e => e.Amount);
      if (debits != credits) return AiToolHelper.Error($"{label}: unbalanced. Total debits ({debits}) do not equal total credits ({credits}).");

      normalised.Add(t with
      {
        Description = t.Description.Trim(),
        Note = string.IsNullOrWhiteSpace(t.Note) ? null : t.Note.Trim(),
        Entries = entries.ToArray(),
      });
    }

    // set the validated content
    var content = new ProposalContent(ProposalType.Transactions, Transactions: normalised.ToArray());

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