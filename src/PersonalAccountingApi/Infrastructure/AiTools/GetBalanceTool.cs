using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Infrastructure.Data;

namespace PersonalAccountingApi.Infrastructure.AiTools;

// define what the model send to the tool
public sealed record GetBalanceArgs(string[] AccountNames);

public sealed class GetBalanceTool(AppDbContext db) : IAiTool
{
  // define the tool schema
  public AiToolDefinition Definition { get; } = new(
    Name: "get_balance",
    Description:
      """
      Get the current balance of one or more accounts. Use it when the answer needs how much is in an account right now
      (e.g. "how much cash do I have?", "what do I owe on my credit card?"), or when you need a balance before proposing a transaction.
      Each requested name returns its own row (account, type, balance); nothing is added up across names. Request each name once.
      A name must be either an exact ledger account name (e.g. "Cash"), or one of the 5 fundamentals: Assets, Liabilities, Equity, Revenue, Expenses (a fundamental returns the combined balance of all accounts of that type).
      Balances are positive in the account's normal direction: an asset balance of 1000 means you have 1000, a liability
      balance of 300 means you owe 300.
      This returns only the current balance. For past activity or period totals, use search_transactions or summarize.
      """,
    ParametersSchema: JsonNode.Parse("""
      {
        "type": "object",
        "properties": {
          "accountNames": { 
            "type": "array",
            "items": {
              "type": "string"
            },
            "description": "Account names, each either an exact ledger account name or one of Assets, Liabilities, Equity, Revenue, Expenses. E.g. [\"Cash\", \"Credit Card Debt\"] or [\"Assets\"]." 
          }
        },
        "required": ["accountNames"]
      }
      """)!.AsObject());

  public async Task<AiToolResult> ExecuteAsync(AiToolCall call, ToolContext ctx, CancellationToken ct)
  {
    // get args, a failure goes back to the model
    GetBalanceArgs? args;
    try
    {
      args = call.Arguments.Deserialize<GetBalanceArgs>(AiJson.Options);
    }
    catch (JsonException ex)
    {
      return AiToolHelper.Error($"Invalid Arguments: {ex.Message}");
    }
    if (args?.AccountNames is not { Length: > 0 }) return AiToolHelper.Error("AccountNames must not be empty");

    // query
    var types = new List<AccountType>(); // assets, liabilities, etc
    var names = new List<string>(); // cash, loan receivable, etc
    foreach (var raw in args.AccountNames.Select(n => n?.Trim() ?? "").Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
    {
      if (AiToolHelper.ParseAccountType(raw) is { } type) { if (!types.Contains(type)) types.Add(type); }
      else names.Add(raw);
    }

    // every ledger account must exist, so a new account with no entries returns 0
    var existing = await db.Accounts
      .Where(a => names.Contains(a.Name))
      .Select(a => a.Name)
      .ToListAsync(ct);
    var unknown = names.Except(existing).ToList();
    if (unknown.Count > 0) return AiToolHelper.Error($"Account not found: {string.Join(", ", unknown)}.");

    // get user's all journal entries
    var userEntries =
      from j in db.JournalEntries
      join t in db.Transactions on j.TransactionId equals t.Id
      join m in db.Messages on t.MessageId equals m.Id
      where m.UserId == ctx.UserId
      select j;

    // get the fundamental types
    var typeBalances = await (
      from j in userEntries
      join a in db.Accounts on j.AccountId equals a.Id
      where types.Contains(a.Type)
      group j by a.Type into g
      select new { g.Key, Balance = g.Sum(e => e.Amount) }
    ).ToListAsync(ct); 

    // get the individual account balances
    var nameBalances = await (
      from j in userEntries
      join a in db.Accounts on j.AccountId equals a.Id
      where names.Contains(a.Name)
      group j by new { a.Name, a.Type } into g
      select new { g.Key.Name, g.Key.Type, Balance = g.Sum(e => e.Amount) }
    ).ToListAsync(ct);

    // combine, fill in 0 for anything that had no entries
    var results = new List<object>();
    foreach (var type in types)
      results.Add(new { account = type.ToString(), type, balance = typeBalances.FirstOrDefault(r => r.Key == type)?.Balance ?? 0m });
    foreach (var name in names)
    {
      var row = nameBalances.FirstOrDefault(r => r.Name == name);
      results.Add(new { account = name, type = row?.Type, balance = row?.Balance ?? 0m });
    }

    return new ToolOutput(JsonSerializer.Serialize(results, AiJson.Options));
  }
}