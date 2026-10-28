using System.Text.Json.Serialization;
using PersonalAccountingApi.Common.Line;

namespace PersonalAccountingApi.Infrastructure.ExternalApis;

public record LineSendMessageResponse(
  [property: JsonPropertyName("sentMessages")] List<SentMessage> SentMessages
);

public record SentMessage(
  [property: JsonPropertyName("id")] string Id,
  [property: JsonPropertyName("quoteToken")] string? QuoteToken
);

public interface ILineMessagingClient
{
  Task<LineSendMessageResponse> PushAsync(string to, IEnumerable<LineMessage> messages, CancellationToken ct = default);
  Task<LineSendMessageResponse> ReplyAsync(string replyToken, IEnumerable<LineMessage> messages, CancellationToken ct = default);
}