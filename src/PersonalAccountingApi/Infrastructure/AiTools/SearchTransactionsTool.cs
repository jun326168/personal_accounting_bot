using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Infrastructure.Data;

namespace PersonalAccountingApi.Infrastructure.AiTools;

public sealed record SearchTransactionsArgs(
  DateOnly? DateFrom,
  DateOnly? DateTo,
  string[]? AccountNames,
  string[]? Texts,
  decimal? AmountMin,
  decimal? AmountMax,
  TransactionKind? Kind,
  long? TransactionId,
  TransactionSort? Sort,
  int? Limit);

public sealed class SearchTransactionsTool(AppDbContext db) : IAiTool
{
  public AiToolDefinition Definition { get; } = new(
    Name: "search_transactions",
    Description:
      "Search the user's recorded transactions, each returned with its journal entries (account, debit/credit side, amount). " +
      "Use to answer questions about past spending or income, and to find the id of a transaction before proposing a reversal. " +
      "All filters are optional and combined with AND, but give at least a date range or one other filter. " +
      "Use summarize instead when only totals are needed, and get_balance for current balances.",
    ParametersSchema: JsonNode.Parse("""
      {
        "type": "object",
        "properties": {
          "dateFrom": { "type": "string", "format": "date", "description": "Start date, inclusive, YYYY-MM-DD. Resolve relative dates like 'last week' from today's date." },
          "dateTo": { "type": "string", "format": "date", "description": "End date, inclusive, YYYY-MM-DD. For a single day, set equal to dateFrom." },
          "accountNames": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Match transactions that touch any of these accounts. Ledger account names (e.g. \"Cash\", \"Loan Receivable\") or the 5 general accounts (Assets, Liabilities, Equity, Revenue, Expenses). E.g. [\"Expenses\"] for all spending."
          },
          "texts": { 
            "type": "array",
            "items": { "type": "string" },
            "description": "Substrings to look for in the description or note, e.g. [\"clothing\", \"shoes\"]." 
          },
          "amountMin": { "type": "number", "description": "Minimum transaction amount (the total of one side). For an exact amount, set equal to amountMax." },
          "amountMax": { "type": "number", "description": "Maximum transaction amount (the total of one side)." },
          "kind": { "type": "string", "enum": ["normal", "opening", "reversal", "adjusting"], "description": "Only this kind of transaction. Omit to get normal transactions." },
          "transactionId": { "type": "integer", "description": "Fetch one specific transaction by id, e.g. one returned by an earlier search." },
          "sort": { "type": "string", "enum": ["newest", "oldest", "largest", "smallest"], "description": "Defaults to newest first." },
          "limit": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Max transactions to return. Defaults to 10." }
        }
      }
      """)!.AsObject());

  public async Task<AiToolResult> ExecuteAsync(AiToolCall call, ToolContext ctx, CancellationToken ct)
  {
    SearchTransactionsArgs? args;
    try
    {
      args = call.Arguments.Deserialize<SearchTransactionsArgs>(AiJson.Options);
    }
    catch (JsonException ex)
    {
      return AiToolHelper.Error($"Invalid Arguments: {ex.Message}");
    }
    if (args is null) return AiToolHelper.Error("At least one argument required");

    // check invalid args
    if (args.DateFrom > args.DateTo) return AiToolHelper.Error("dateFrom cannot be later than dateTo");
    if (args.AmountMin > args.AmountMax) return AiToolHelper.Error("amountMin cannot be larger than amountMax");

    // get user's transactions queryable
    IQueryable<Transaction> q =
      from t in db.Transactions
      join m in db.Messages on t.MessageId equals m.Id
      where m.UserId == ctx.UserId
      select t;

    // filter transactions

    if (args.TransactionId is null)
      q = q.Where(t => t.Kind == (args.Kind ?? TransactionKind.Normal)); // kind
    if (args.DateFrom is { } dateFrom) q = q.Where(t => t.Date >= dateFrom); // date from
    if (args.DateTo is { } dateTo) q = q.Where(t => t.Date <= dateTo); // date to
    if (args.TransactionId is { } id) q = q.Where(t => t.Id == id); // transaction id
    // texts
    var patterns = args.Texts?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => $"%{DataHelper.EscapeILikePattern(s)}%").ToList() ?? new List<string>();
    IQueryable<Transaction>? pq = null;
    foreach (string pattern in patterns)
    {
      var subq = q.Where(t => EF.Functions.ILike(t.Description ?? "", pattern) || EF.Functions.ILike(t.Note ?? "", pattern));
      pq = pq == null ? subq : pq.Union(subq);
    }
    q = pq ?? q;
    // amount (the transaction total is half the sum of absolute amounts, because every transaction balances)
    if (args.AmountMin is { } min)
      q = q.Where(t => db.JournalEntries.Where(j => j.TransactionId == t.Id).Sum(j => Math.Abs(j.Amount)) / 2 >= min);
    if (args.AmountMax is { } max)
      q = q.Where(t => db.JournalEntries.Where(j => j.TransactionId == t.Id).Sum(j => Math.Abs(j.Amount)) / 2 <= max);
    // account
    if (args.AccountNames is { Length: > 0 })
    {
      var cleaned = args.AccountNames
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => n.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
      var isAccountType = cleaned.ToLookup(n => AiToolHelper.ParseAccountType(n) is not null);
      var types = isAccountType[true].Select(n => AiToolHelper.ParseAccountType(n)!);
      var names = isAccountType[false].ToList();

      var existing = await db.Accounts.Where(a => names.Contains(a.Name)).Select(a => a.Name).ToListAsync(ct);
      var unknown = names.Except(existing).ToList();
      if (unknown.Count > 0) return AiToolHelper.Error($"Account not found: {string.Join(", ", unknown)}. Use search_accounts to find the exact name");

      var accountIds = await db.Accounts.Where(a => names.Contains(a.Name) || types.Contains(a.Type)).Select(a => a.Id).ToListAsync(ct);
      q = q.Where(t => db.JournalEntries.Any(j => j.TransactionId == t.Id && accountIds.Contains(j.AccountId)));
    }
    // sort and limit
    q = ((args.Sort ?? TransactionSort.Newest) switch
    {
      TransactionSort.Newest => q.OrderByDescending(t => t.Date),
      TransactionSort.Oldest => q.OrderBy(t => t.Date),
      TransactionSort.Largest => q.OrderByDescending(t => db.JournalEntries.Where(j => j.TransactionId == t.Id).Sum(j => Math.Abs(j.Amount))),
      TransactionSort.Smallest => q.OrderBy(t => db.JournalEntries.Where(j => j.TransactionId == t.Id).Sum(j => Math.Abs(j.Amount))),
      _ => q.OrderByDescending(t => t.Date)
    }).ThenByDescending(t => t.Id);
    var transactions = await q.Take(Math.Clamp(args.Limit ?? 10, 1, 50)).ToListAsync(ct);

    var results = new List<object>();
    if (transactions.Any())
    {
      // get journal entries from the transactions
      var transactionIds = transactions.Select(t => t.Id).ToList();
      var journalEntries = await (
        from j in db.JournalEntries
        join a in db.Accounts on j.AccountId equals a.Id
        where transactionIds.Contains(j.TransactionId)
        select new { j.TransactionId, AccountName = a.Name, a.NormalBalance, j.Amount }
      ).ToListAsync(ct);

      var reversedIds = await db.Transactions
        .Where(r => r.ReversesTransactionId != null && transactionIds.Contains(r.ReversesTransactionId.Value))
        .Select(r => r.ReversesTransactionId!.Value)
        .ToListAsync(ct);

      results = transactions.Select(t => (object)new
      {
        id = t.Id,
        date = t.Date,
        description = t.Description,
        note = t.Note,
        kind = t.Kind,
        reversesTransactionId = t.ReversesTransactionId,
        isReversed = reversedIds.Contains(t.Id),
        entries = journalEntries.Where(j => j.TransactionId == t.Id).Select(j => (object)new
        {
          account = j.AccountName,
          side = (j.NormalBalance == NormalBalance.Debit) == (j.Amount >= 0) ? "debit" : "credit",
          amount = Math.Abs(j.Amount),
        }).ToList()
      }).ToList();
    }
    else
    {
      return new ToolOutput("No results.");
    }

    return new ToolOutput(JsonSerializer.Serialize(results, AiJson.Options));
  }
}