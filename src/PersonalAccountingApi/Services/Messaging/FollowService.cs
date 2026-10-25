using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class FollowService(ILineMessagingClient lineClient) : IFollowService
{
  public async Task HandleUserFollowAsync(FollowPayload payload, CancellationToken ct)
  {
    await lineClient.ReplyAsync(payload.ReplyToken, [FollowMessage.BuildFollowMessage()], ct);

    // save the message to db
  }
}
