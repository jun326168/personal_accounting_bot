using System.Net.Http.Json;
using System.Text.Json;
using PersonalAccountingApi.Common.Line;
using System.Text.Json.Serialization;

namespace PersonalAccountingApi.Infrastructure.ExternalApis;

public class LineMessagingClient : ILineMessagingClient
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  private readonly HttpClient _http;
  public LineMessagingClient(HttpClient http) => _http = http;


  // push to userId / groupId / roomId
  public Task<LineSendMessageResponse> PushAsync(string to, IEnumerable<LineMessage> messages, CancellationToken ct = default)
    => PostAsync<object, LineSendMessageResponse>("v2/bot/message/push", new { to, messages }, ct);

  // reply to webhook
  public Task<LineSendMessageResponse> ReplyAsync(string replyToken, IEnumerable<LineMessage> messages, CancellationToken ct = default)
    => PostAsync<object, LineSendMessageResponse>("v2/bot/message/reply", new { replyToken, messages }, ct);

  private async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest payload, CancellationToken ct)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
      Content = JsonContent.Create(payload, options: JsonOptions)
    };
    request.Headers.Add("X-Line-Retry-Key", Guid.NewGuid().ToString());

    using var response = await _http.SendAsync(request, ct);
    if (!response.IsSuccessStatusCode)
    {
      var body = await response.Content.ReadAsStringAsync(ct);
      throw new HttpRequestException(
          $"LINE API Failed: {(int)response.StatusCode} {body}", null, response.StatusCode);
    }

    return await response.Content.ReadFromJsonAsync<TResponse>(options: JsonOptions, cancellationToken: ct)
      ?? throw new InvalidOperationException("LINE API Failed: Message ID not received");
  }
}