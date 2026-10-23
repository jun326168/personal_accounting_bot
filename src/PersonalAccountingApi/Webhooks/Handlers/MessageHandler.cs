using System.Text.Json;

namespace PersonalAccountingApi.Webhooks.Handlers;

public sealed class MessageHandler(
  ILogger<MessageHandler> logger) : IWebhookHandler
{
  public string EventType = "message";

  public Task async HandleAsync(WebhookEnvelope envelope, CancellationToken ct)
  {
    
  }
}