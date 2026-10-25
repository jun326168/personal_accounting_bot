namespace PersonalAccountingApi.Domain.Entities;

// Amount can be negative: e.g. the account's normal balance is debit but amount is negative -> credit
public sealed record JournalEntry(
  long Id,
  long TransactionId,
  long AccountId,
  decimal Amount,
  DateTimeOffset CreatedAt);
