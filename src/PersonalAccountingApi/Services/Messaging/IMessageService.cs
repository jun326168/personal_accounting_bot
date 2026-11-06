using System.Collections.Generic;

namespace PersonalAccountingApi.Services.Messaging;

// response record
public abstract record AssistantReply;
public sealed record AnswerReply(string Text) : AssistantReply;
public sealed record ProposalReply(long ProposalId) : AssistantReply; // render as confirm card


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

