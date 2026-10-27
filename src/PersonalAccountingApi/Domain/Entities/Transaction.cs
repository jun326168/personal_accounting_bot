namespace PersonalAccountingApi.Domain.Entities;

public enum TransactionKind { Opening, Normal, Reversal, Adjusting }

public sealed class Transaction
{
  public long Id { get; init; }
  public long MessageId { get; init; }
  public string? Description { get; set; }
  public DateOnly Date { get; init; }
  public TransactionKind Kind { get; init; }
  public long? ReversesTransactionId { get; init; } // set if and only if Kind is Reversal
  public string? Note { get; set; }
  public DateTimeOffset CreatedAt { get; init; }
}
