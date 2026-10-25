namespace PersonalAccountingApi.Domain.Entities;

public enum AccountType { Asset, Liability, Equity, Revenue, Expense }

public enum NormalBalance { Debit, Credit }

// NormalBalance is a generated column in the db, read-only here
public sealed record Account(
  long Id,
  string Name,
  AccountType Type,
  NormalBalance NormalBalance,
  DateTimeOffset CreatedAt);
