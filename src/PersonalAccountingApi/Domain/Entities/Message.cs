using System.Text.Json.Nodes;

namespace PersonalAccountingApi.Domain.Entities;

public enum MessageRole { Bot, User }

public enum MessageType { Text, FlexMessage, Template }

public sealed record Message(
  long Id,
  long UserId,
  MessageRole Role,
  string LineMessageId,
  string? TextContent,
  JsonNode? JsonContent, // for flex message or template
  MessageType Type,
  bool IsTransaction,
  DateTimeOffset CreatedAt);
