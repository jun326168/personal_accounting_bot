using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalAccountingApi.Domain.Entities;

namespace PersonalAccountingApi.Infrastructure.AiTools;

public enum TransactionSort { Newest, Oldest, Largest, Smallest }

public enum SummarizeGroupBy { Account, Type, Month }

public static class AiJson
{
  public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
  {
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
  };
}

public static class AiToolHelper
{
  public static AccountType? ParseAccountType(string name) => name.ToLowerInvariant().Trim() switch
  {
    "asset" or "assets" => AccountType.Asset,
    "liability" or "liabilities" => AccountType.Liability,
    "equity" or "equities" => AccountType.Equity,
    "revenue" or "revenues" => AccountType.Revenue,
    "expense" or "expenses" => AccountType.Expense,
    _ => null
  };

  public static ToolOutput Error(string message) => new(message, IsError: true);
}