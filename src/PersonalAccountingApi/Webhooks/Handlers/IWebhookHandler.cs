using PersonalAccountingApi.Webhooks;

namespace PersonalAccountingApi.Webhooks.Handlers;

public interface IWebhookHandler
{
  string EventType { get; }
  Task HandleAsync(LineEventDto dto, CancellationToken ct);
}