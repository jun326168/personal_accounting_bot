namespace PersonalAccountingApi.Domain.Entities;

public sealed class User
{
  public long Id { get; init; }
  public required string LineUserId { get; init; }
  public DateTimeOffset CreatedAt { get; init; }
}
