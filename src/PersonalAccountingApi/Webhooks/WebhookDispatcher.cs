using PersonalAccountingApi.Webhooks.Handlers;

namespace PersonalAccountingApi.Webhooks;

public interface IWebhookDispatcher
{
  Task DispatchAsync(WebhookEnvelope envelope, CancellationToken ct);
}

public sealed class WebhookDispatcher(
  IEnumerable<IWebhookHandler> handlers,
  ILogger<WebhookDispatcher> logger) : IWebhookDispatcher
{
  // Throws at startup if two handlers claim the same event type: fail fast
  private readonly Dictionary<string, IWebhookHandler> _byType = handlers.ToDictionary(h => h.EventType);

  public async Task DispatchAsync(WebhookEnvelope envelope, CancellationToken ct)
  {
    if (!_byType.TryGetValue(envelope.Type, out var handler))
    {
      logger.LogWarning("No handler for {EventType}; ignoring", envelope.Type);
      return;
    }

    await handler.HandleAsync(envelope, ct);
  }
}

