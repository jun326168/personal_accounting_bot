namespace PersonalAccountingApi.Webhooks.Handlers;

public interface IWebhookHandler
{
  string EventType { get; }
  Task HandleAsync(WebhookEnvelope envelope, CancellationToken ct);
}