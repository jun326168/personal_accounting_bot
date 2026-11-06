namespace PersonalAccountingApi.Webhooks;

public sealed class WebhookWorker(
  IWebhookQueue queue,
  IServiceScopeFactory scopeFactory,
  ILogger<WebhookWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    try
    {
      await foreach (var dto in queue.ReadAllAsync(stoppingToken))
      {
        try
        {
          await using var scope = scopeFactory.CreateAsyncScope();
          var dispatcher = scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>();
          await dispatcher.DispatchAsync(dto, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          // one bad job must never stop the loop
          logger.LogError(ex, "Webhook processing failed");
        }
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // shutting down
    }
  }
}
