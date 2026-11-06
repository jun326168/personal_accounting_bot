using System.Threading.Channels;

namespace PersonalAccountingApi.Webhooks;

public interface IWebhookQueue
{
  bool TryEnqueue(WebhookDto dto);
  IAsyncEnumerable<WebhookDto> ReadAllAsync(CancellationToken ct);
}

public sealed class WebhookQueue : IWebhookQueue
{
  private readonly Channel<WebhookDto> _channel = Channel.CreateBounded<WebhookDto>(
    new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

  public bool TryEnqueue(WebhookDto dto) => _channel.Writer.TryWrite(dto);

  public IAsyncEnumerable<WebhookDto> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
