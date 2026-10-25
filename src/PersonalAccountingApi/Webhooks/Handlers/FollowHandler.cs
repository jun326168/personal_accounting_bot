using System.Text.Json;
using PersonalAccountingApi.Webhooks;

namespace PersonalAccountingApi.Webhooks.Handlers;

public sealed class FollowHandler(
  IFollowService followService,
  ILogger<FollowHandler> logger) : IWebhookHandler
{
  public string EventType = "follow";

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

    var payload = new FollowPayload(userId, dto.ReplyToken);

    await followService.HandleUserFollowAsync(payload, ct);
  }
}