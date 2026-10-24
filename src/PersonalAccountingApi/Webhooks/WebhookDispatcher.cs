using PersonalAccountingApi.Webhooks.Handlers;

namespace PersonalAccountingApi.Webhooks;

public interface IWebhookDispatcher
{
  Task DispatchAsync(WebhookDto dto, CancellationToken ct);
}

public sealed class WebhookDispatcher(
  IEnumerable<IWebhookHandler> handlers,
  ILogger<WebhookDispatcher> logger) : IWebhookDispatcher
{
  // Throws at startup if two handlers claim the same event type: fail fast
  private readonly Dictionary<string, IWebhookHandler> _byType = handlers.ToDictionary(h => h.EventType);

  public async Task DispatchAsync(WebhookDto webhookDto, CancellationToken ct)
  {
    // loop through and handle events
    foreach (LineEventDto eventDto in webhookDto.Events)
    {
      if (!_byType.TryGetValue(eventDto.Type, out var handler))
      {
        logger.LogWarning("No handler for {EventType}; ignoring", eventDto.Type);
        return;
      }

      await handler.HandleAsync(eventDto, ct);
    }
  }
}

