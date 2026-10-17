namespace PersonalAccountingApi.Services;

public interface ILineMessagingService
{
    Task ReplyTextAsync(string replyToken, string text, CancellationToken ct = default);
}
