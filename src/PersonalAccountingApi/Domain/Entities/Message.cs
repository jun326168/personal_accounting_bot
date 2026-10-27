using System.Text.Json.Nodes;

namespace PersonalAccountingApi.Domain.Entities;

public enum MessageRole { Bot, User }

public enum MessageType { Text, FlexMessage, Template }

public sealed class Message
{
  public long Id { get; init; }
  public long UserId { get; init; }
  public MessageRole Role { get; init; }
  public required string LineMessageId { get; init; }
  public string? TextContent { get; init; }
  public JsonNode? JsonContent { get; init; } // for flex message or template
  public MessageType Type { get; init; }
  public bool IsTransaction { get; set; }
  public DateTimeOffset CreatedAt { get; init; }
}
