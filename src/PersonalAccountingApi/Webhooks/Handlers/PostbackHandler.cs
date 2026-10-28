using System.Text.Json;
using System.Web;
using PersonalAccountingApi.Webhooks;
using PersonalAccountingApi.Services.Messaging;

namespace PersonalAccountingApi.Webhooks.Handlers;

public sealed class PostbackHandler(
  IPostbackService postbackService,
  ILogger<PostbackHandler> logger) : IWebhookHandler
{
  public string EventType { get; } = "postback";

  public async Task HandleAsync(LineEventDto dto, CancellationToken ct)
  {
    string? userId = null;
    if (dto.Source.ValueKind != JsonValueKind.Undefined && dto.Source.TryGetProperty("userId", out var userIdProp))
    {
      userId = userIdProp.GetString();
    }

    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(dto.ReplyToken))
    {
      logger.LogWarning("Missing UserId={UserId}, ReplyToken={ReplyToken}", userId, dto.ReplyToken);
      return;
    }

    var postbackElement = dto.Postback!.Value;
    string data = postbackElement.TryGetProperty("data", out var dataProp) ? dataProp.GetString() ?? "" : "";

    // turn params into dictionary
    var queryParams = HttpUtility.ParseQueryString(data);
    var paramsDict = new Dictionary<string, string>();
    foreach (string? key in queryParams.AllKeys)
    {
      if (key != null) paramsDict[key] = queryParams[key] ?? "";
    }

    var payload = new PostbackPayload(userId, dto.ReplyToken, data, paramsDict, dto.Timestamp);

    await postbackService.HandleUserPostbackAsync(payload, ct);
  }
}