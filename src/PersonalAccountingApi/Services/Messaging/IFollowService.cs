namespace PersonalAccountingApi.Services.Messaging;

public record FollowPayload(string UserId, string ReplyToken);

public interface IFollowService
{
    Task HandleUserFollowAsync(FollowPayload payload, CancellationToken ct);
}
