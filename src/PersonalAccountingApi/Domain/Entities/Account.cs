namespace PersonalAccountingApi.Domain.Entities;

public enum AccountType { Asset, Liability, Equity, Revenue, Expense }

public enum NormalBalance { Debit, Credit }

public sealed class Account
{
  public long Id { get; init; }
  public required string Name { get; set; }
  public AccountType Type { get; init; }

  // generated column in the db, read-only here
  public NormalBalance NormalBalance { get; private set; }
  public DateTimeOffset CreatedAt { get; init; }
}
