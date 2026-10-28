using System.Text.Json;
using PersonalAccountingApi.Webhooks;
using PersonalAccountingApi.Services.Messaging;

namespace PersonalAccountingApi.Webhooks.Handlers;

public sealed class MessageHandler(
  IMessageService messageService,
  ILogger<MessageHandler> logger) : IWebhookHandler
{
  public string EventType { get; } = "message";

  public async Task HandleAsync(LineEventDto dto, CancellationToken ct)
  {
    string? userId = null;
    if (dto.Source.ValueKind != JsonValueKind.Undefined && dto.Source.TryGetProperty("userId", out var userIdProp))
    {
      userId = userIdProp.GetString();
    }

    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(dto.ReplyToken))
    {
      logger.LogWarning("Missing UserId={UserId}, ReplyToken={ReplyToken}", userId, dto.ReplyToken);
      return;
    }

    var messageElement = dto.Message!.Value;
    string msgType = messageElement.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
    string msgId = messageElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
    string text = messageElement.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";

    var payload = new MessagePayload(userId, dto.ReplyToken, msgId, msgType, text, dto.Timestamp);

    await messageService.HandleUserMessageAsync(payload, ct);
  }
}