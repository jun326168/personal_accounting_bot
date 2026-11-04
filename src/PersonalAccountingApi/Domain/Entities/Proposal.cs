using System.Text.Json.Serialization;

namespace PersonalAccountingApi.Domain.Entities;

public enum ProposalStatus { Pending, Confirmed, Cancelled }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProposedKind { Normal, Opening, Adjusting }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProposalType { Transactions, Reversal }

public sealed record ReversalRequest(long TransactionId, string? Note);

public sealed record ProposalJournalEntry(string AccountName, NormalBalance Side, decimal Amount);
public sealed record ProposalTransaction(DateOnly Date, ProposedKind Kind, string Description, string? Note, ProposalJournalEntry[] Entries);

// Type says which payload is filled; the confirm handler switches on it.
public sealed record ProposalContent(
  ProposalType Type,
  ProposalTransaction[]? Transactions = null,
  ReversalRequest? Reversal = null);

public sealed class Proposal
{
  public long Id { get; init; }
  public long MessageId { get; init; }
  public required ProposalContent Content { get; init; }
  public ProposalStatus Status { get; set; }
  public DateTimeOffset CreatedAt { get; init; }
  public DateTimeOffset? DecidedAt { get; set; }
}
