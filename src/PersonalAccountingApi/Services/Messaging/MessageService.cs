using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class MessageService(
  ILineMessagingClient lineClient,
  AppDbContext db,
  ILogger<MessageService> logger) : IMessageService
{
  public async Task HandleUserMessageAsync(MessagePayload payload, CancellationToken ct)
  {
    // send the reply message first and let ai process the real response
    var thinkingMessage = ThinkingMessage.BuildThinkingMessage();
    var replyResponse = await lineClient.ReplyAsync(payload.ReplyToken, [thinkingMessage], ct);

    // create ai response

    // push ai response

  }
}
