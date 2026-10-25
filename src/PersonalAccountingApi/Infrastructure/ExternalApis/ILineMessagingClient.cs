using PersonalAccountingApi.Common.Line;

namespace PersonalAccountingApi.Infrastructure.ExternalApis;

public interface ILineMessagingClient
{
    Task PushAsync(string to, IEnumerable<LineMessage> messages, CancellationToken ct = default);
    Task ReplyAsync(string replyToken, IEnumerable<LineMessage> messages, CancellationToken ct = default);
}