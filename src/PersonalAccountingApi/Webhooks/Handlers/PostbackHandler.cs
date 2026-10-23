using System.Text.Json;

namespace PersonalAccountingApi.Webhooks.Handlers;

public sealed class PostbackHandler(
  ILogger<PostbackHandler> logger) : IWebhookHandler
{
  public string EventType = "postback";

  public Task async HandleAsync(WebhookEnvelope envelope, CancellationToken ct)
  {
    
  }
}