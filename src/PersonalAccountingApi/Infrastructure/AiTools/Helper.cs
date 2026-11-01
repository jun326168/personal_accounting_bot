using System.Text.Json;
using PersonalAccountingApi.Domain.Entities;

namespace PersonalAccountingApi.Infrastructure.AiTools;

public static class AiJson
{
  public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
  {
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
  };
}

public static class AccountTypes {
  public static AccountType? Parse(string name) => name.ToLowerInvariant().Trim() switch
  {
    "asset" or "assets" => AccountType.Asset,
    "liability" or "liabilities" => AccountType.Liability,
    "equity" or "equities" => AccountType.Equity,
    "revenue" or "revenues" => AccountType.Revenue,
    "expense" or "expenses" => AccountType.Expense,
    _ => null
  };
}