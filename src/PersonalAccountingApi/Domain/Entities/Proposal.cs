using System.Text.Json.Nodes;

namespace PersonalAccountingApi.Domain.Entities;

public enum ProposalStatus { Pending, Confirmed, Cancelled }

public sealed class Proposal
{
  public long Id { get; init; }
  public long MessageId { get; init; }
  public required JsonNode Content { get; init; }
  public ProposalStatus Status { get; set; }
  public DateTimeOffset CreatedAt { get; init; }
  public DateTimeOffset? DecidedAt { get; set; }
}
