namespace PersonalAccountingApi.Domain.Entities;

public enum TransactionKind { Opening, Normal, Reversal, Adjusting }

public sealed record Transaction(
  long Id,
  long MessageId,
  string? Description,
  DateOnly Date,
  TransactionKind Kind,
  long? ReversesTransactionId, // set if and only if Kind is Reversal
  string? Note,
  DateTimeOffset CreatedAt);
