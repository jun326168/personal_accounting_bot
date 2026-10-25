namespace PersonalAccountingApi.Domain.Entities;

public sealed record User(
  long Id,
  string LineUserId,
  DateTimeOffset CreatedAt);
