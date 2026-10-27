using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class MessageService(ILineMessagingClient lineClient) : IMessageService
{
  public async Task HandleUserMessageAsync(MessagePayload payload, CancellationToken ct)
  {
    
  }
}
