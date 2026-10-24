using System.Collections.Generic;

namespace PersonalAccountingApi.Services.Messaging;

public record MessagePayload(
  string UserId,  // LINE user ID (LINE UID)
  string ReplyToken,
  string MessageId,
  string MessageType, // text, image, audio, video, location, sticker, etc
  string Text,  // content if type is text
  long Timestamp
);

public interface IMessageService
{
  Task HandleUserMessageAsync(MessagePayload payload, CancellationToken ct);
}
