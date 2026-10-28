using Microsoft.EntityFrameworkCore;
using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Domain.Entities;
using PersonalAccountingApi.Infrastructure.Data;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class FollowService(
  ILineMessagingClient lineClient,
  AppDbContext db,
  ILogger<FollowService> logger) : IFollowService
{
  public async Task HandleUserFollowAsync(FollowPayload payload, CancellationToken ct)
  {
    // send the welcome message
    var message = FollowMessage.BuildFollowMessage();
    var response = await lineClient.ReplyAsync(payload.ReplyToken, [message], ct);

    // the reply is already sent: don't fail the webhook (LINE would retry and resend the welcome)
    try
    {
      var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == payload.UserId, ct);
      // create a new user if not exist
      if (user == null)
      {
        user = new User { LineUserId = payload.UserId };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct); // populates user.Id automatically
      }
      // create message
      db.Messages.Add(new Message
      {
        UserId = user.Id,
        Role = MessageRole.Bot,
        LineMessageId = response.SentMessages[0].Id,
        TextContent = ((TextMessage)message).Text,
        Type = MessageType.Text
      });
      await db.SaveChangesAsync(ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogError(ex, "Failed to save follow for UserId={UserId}", payload.UserId);
    }
  }
}
