using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalAccountingApi.Webhooks;

public sealed record WebhookDto(
  [property: JsonPropertyName("destination")] string Destination,
  [property: JsonPropertyName("events")] LineEventDto[] Events
);

public sealed record LineEventDto(
  [property: JsonPropertyName("webhookEventId")] string Id,
  [property: JsonPropertyName("type")] string Type,

  [property: JsonPropertyName("timestamp")] long Timestamp,

  [property: JsonPropertyName("replyToken")] string? ReplyToken,
  [property: JsonPropertyName("source")] JsonElement Source,

  [property: JsonPropertyName("message")] JsonElement? Message,
  [property: JsonPropertyName("postback")] JsonElement? Postback
);