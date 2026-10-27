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
  public Task PushAsync(string to, IEnumerable<LineMessage> messages, CancellationToken ct = default)
    => PostAsync("v2/bot/message/push", new { to, messages }, ct);

  // reply to webhook
  public Task ReplyAsync(string replyToken, IEnumerable<LineMessage> messages, CancellationToken ct = default)
    => PostAsync("v2/bot/message/reply", new { replyToken, messages }, ct);

  private async Task PostAsync<T>(string url, T payload, CancellationToken ct)
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
  }
}