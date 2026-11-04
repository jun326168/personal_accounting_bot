using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Infrastructure.Data;

namespace PersonalAccountingApi.Infrastructure.AiTools;

public sealed record SummarizeArgs(DateOnly DateFrom, DateOnly DateTo, string[]? AccountNames, SummarizeGroupBy? GroupBy);

public sealed class SummarizeTool(AppDbContext db) : IAiTool
{
  public AiToolDefinition Definition { get; } = new(
    Name: "summarize",
    Description:
      """
      Get period totals: how much was spent or earned between two dates, grouped by account, type or month.
      Use it for questions like "how much did I spend on food this month?", "what was my income last quarter?", etc.
      Each group returns its own row (group, type, total); nothing is added up across groups, and revenue and expenses are never combined into one number.
      Totals are positive in the account's normal direction: an expense total of 500 means 500 was spent, a revenue total of 2000 means 2000 was earned.
      Reversed transactions are already netted out, adjusting entries are included and opening balances are not.
      Both dates are required. Narrow with accountNames (an exact ledger account name, or Revenue / Expenses); omit it to cover all revenue and expenses. accountNames is only used when grouping by account, and is ignored for type and month.
      Assets and liabilities are balances, not period totals: use get_balance for how much is in an account right now.
      Use search_transactions when the individual transactions are needed, or to find a transaction id.
      """,
    ParametersSchema: JsonNode.Parse("""
      {
        "type": "object",
        "properties": {
          "dateFrom": { "type": "string", "format": "date", "description": "Start date, inclusive, YYYY-MM-DD. Resolve relative periods like 'this month' or 'last quarter' from today's date." },
          "dateTo": { "type": "string", "format": "date", "description": "End date, inclusive, YYYY-MM-DD. For a single day, set equal to dateFrom." },
          "accountNames": {
            "type": "array",
            "items": { "type": "string" },
            "description": "Only include these accounts. Ledger account names (e.g. \"Food\", \"Salary\") or Revenue / Expenses. E.g. [\"Expenses\"] for all spending."
          },
          "groupBy": { "type": "string", "enum": ["account", "type", "month"], "description": "How to group the totals. Defaults to account. Use month for a trend over time, type for revenue vs expenses. type and month cover all revenue and expenses." }
        },
        "required": ["dateFrom", "dateTo"]
      }
      """)!.AsObject());

  public async Task<AiToolResult> ExecuteAsync(AiToolCall call, ToolContext ctx, CancellationToken ct)
  {
    SummarizeArgs? args;
    try
    {
      args = call.Arguments.Deserialize<SummarizeArgs>(AiJson.Options);
    }
    catch (JsonException ex)
    {
      return AiToolHelper.Error($"Invalid Arguments: {ex.Message}");
    }
    if (args is null) return AiToolHelper.Error("dateFrom and dateTo are required");

    // check invalid args
    if (args.DateFrom > args.DateTo) return AiToolHelper.Error("dateFrom cannot be later than dateTo");

    var cleaned = (args.AccountNames ?? [])
      .Where(n => !string.IsNullOrWhiteSpace(n))
      .Select(n => n.Trim())
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
    var isAccountType = cleaned.ToLookup(n => AiToolHelper.ParseAccountType(n) is not null);
    var types = isAccountType[true].Select(n => AiToolHelper.ParseAccountType(n)!).ToList();
    var names = isAccountType[false].ToList();

    // every ledger account must exist, so a new account with no entries returns 0
    var existing = await db.Accounts
      .Where(a => (a.Type == AccountType.Revenue || a.Type == AccountType.Expense) && names.Contains(a.Name))
      .Select(a => a.Name)
      .ToListAsync(ct);
    var unknown = names.Except(existing).ToList();
    if (unknown.Count > 0) return AiToolHelper.Error($"Account not found or not belong to revenue or expenses: {string.Join(", ", unknown)}. For assets and liabilities, use get_balance.");
    if (types.Any(t => t is not (AccountType.Revenue or AccountType.Expense))) return AiToolHelper.Error($"Account type must be revenue or expenses. For assets and liabilities, use get_balance.");

    // query with different groupby
    var results = new List<object>();
    switch (args.GroupBy)
    {
      // group by account
      case SummarizeGroupBy.Account or null:
        var nameSummaries = await (
          from a in db.Accounts
          where (names.Count == 0 && types.Count == 0 || names.Contains(a.Name))
          join j in db.JournalEntries on a.Id equals j.AccountId
          join t in db.Transactions on j.TransactionId equals t.Id
          join m in db.Messages on t.MessageId equals m.Id
          where m.UserId == ctx.UserId
          where t.Kind != TransactionKind.Opening && t.Date >= args.DateFrom && t.Date <= args.DateTo
          where a.Type == AccountType.Revenue || a.Type == AccountType.Expense
          group j by new { a.Name, a.Type } into g
          select new { group = g.Key.Name, type = g.Key.Type, total = g.Sum(j => j.Amount) }
        ).ToListAsync(ct);

        var typeSummaries = await (
          from a in db.Accounts
          where types.Contains(a.Type)
          join j in db.JournalEntries on a.Id equals j.AccountId
          join t in db.Transactions on j.TransactionId equals t.Id
          join m in db.Messages on t.MessageId equals m.Id
          where m.UserId == ctx.UserId
          where t.Kind != TransactionKind.Opening && t.Date >= args.DateFrom && t.Date <= args.DateTo
          where a.Type == AccountType.Revenue || a.Type == AccountType.Expense
          group j by a.Type into g
          select new { group = g.Key.ToString(), type = g.Key, total = g.Sum(j => j.Amount) }
        ).ToListAsync(ct);

        results = nameSummaries.Concat(typeSummaries).OrderBy(x => x.group, StringComparer.Ordinal).ThenBy(x => x.type).Cast<object>().ToList();
        break;

      // group by type (expense, revenue)
      case SummarizeGroupBy.Type:
        var summaries = await(
          from a in db.Accounts
          join j in db.JournalEntries on a.Id equals j.AccountId
          join t in db.Transactions on j.TransactionId equals t.Id
          join m in db.Messages on t.MessageId equals m.Id
          where m.UserId == ctx.UserId
          where t.Kind != TransactionKind.Opening && t.Date >= args.DateFrom && t.Date <= args.DateTo
          where a.Type == AccountType.Revenue || a.Type == AccountType.Expense
          group j by a.Type into g
          select new { group = g.Key.ToString(), type = g.Key, total = g.Sum(j => j.Amount) }
        ).ToListAsync(ct);

        results = summaries.OrderBy(x => x.group, StringComparer.Ordinal).ThenBy(x => x.type).Cast<object>().ToList();
        break;

      // group by month
      case SummarizeGroupBy.Month:
        var yearmonthSummaries = await(
          from a in db.Accounts
          join j in db.JournalEntries on a.Id equals j.AccountId
          join t in db.Transactions on j.TransactionId equals t.Id
          join m in db.Messages on t.MessageId equals m.Id
          where m.UserId == ctx.UserId
          where t.Kind != TransactionKind.Opening && t.Date >= args.DateFrom && t.Date <= args.DateTo
          where a.Type == AccountType.Revenue || a.Type == AccountType.Expense
          group j by new { a.Type, t.Date.Year, t.Date.Month } into g
          select new { year = g.Key.Year, month = g.Key.Month, type = g.Key.Type, total = g.Sum(j => j.Amount) }
        ).ToListAsync(ct);

        results = yearmonthSummaries
          .Select(x => new { group = $"{x.year:D4}-{x.month:D2}", type = x.type, total = x.total })
          .OrderBy(x => x.group, StringComparer.Ordinal)
          .ThenBy(x => x.type)
          .Cast<object>().ToList();
        break;

      default:
        break;
    }

    if (results.Count == 0) return new ToolOutput("No results.");

    return new ToolOutput(JsonSerializer.Serialize(results, AiJson.Options));
  }
}