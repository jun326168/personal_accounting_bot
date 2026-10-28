using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class MessageService() : IMessageService
{
  public Task HandleUserMessageAsync(MessagePayload payload, CancellationToken ct)
    => Task.CompletedTask;
}
