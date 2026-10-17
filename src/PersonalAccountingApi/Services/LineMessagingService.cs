using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using PersonalAccountingApi.Options;

namespace PersonalAccountingApi.Services;

public sealed class LineMessagingService(HttpClient http, IOptions<LineOptions> options) : ILineMessagingService
{
    public async Task ReplyTextAsync(string replyToken, string text, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/bot/message/reply")
        {
            Content = JsonContent.Create(new
            {
                replyToken,
                messages = new[] { new { type = "text", text } },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ChannelAccessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
