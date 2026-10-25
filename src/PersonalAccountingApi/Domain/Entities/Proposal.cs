using System.Text.Json.Nodes;

namespace PersonalAccountingApi.Domain.Entities;

public enum ProposalStatus { Pending, Confirmed, Cancelled }

public sealed record Proposal(
  long Id,
  long MessageId,
  JsonNode Content,
  ProposalStatus Status,
  DateTimeOffset CreatedAt,
  DateTimeOffset? DecidedAt);
