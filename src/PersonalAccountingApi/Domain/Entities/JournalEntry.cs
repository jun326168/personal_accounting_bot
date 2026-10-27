namespace PersonalAccountingApi.Domain.Entities;

public sealed class JournalEntry
{
  public long Id { get; init; }
  public long TransactionId { get; init; }
  public long AccountId { get; init; }

  // can be negative: e.g. the account's normal balance is debit but amount is negative -> credit
  public decimal Amount { get; init; }
  public DateTimeOffset CreatedAt { get; init; }
}
