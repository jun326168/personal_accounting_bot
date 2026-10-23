using System.Text.Json;

namespace PersonalAccountingApi.Webhooks.Handlers;

public sealed class FollowHandler(
  ILogger<FollowHandler> logger) : IWebhookHandler
{
  public string EventType = "follow";

  public Task async HandleAsync(WebhookEnvelope envelope, CancellationToken ct)
  {
    
  }
}