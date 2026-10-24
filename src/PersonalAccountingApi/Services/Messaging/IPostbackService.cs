using System.Collections.Generic;

namespace PersonalAccountingApi.Services.Messaging;

public record PostbackPayload(
  string UserId,
  string ReplyToken,
  string Data,  // Original Postback Data (e.g. "action=buy&id=5")
  Dictionary<string, string> Params, // Key-Value map for service
  long Timestamp,
  string? SelectedDatetime = null  // if it's datetime picker
);

public interface IPostbackService
{
  Task HandleUserPostbackAsync(PostbackPayload payload, CancellationToken ct);
}
