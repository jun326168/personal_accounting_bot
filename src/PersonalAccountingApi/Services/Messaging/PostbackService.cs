using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class PostbackService(ILineMessagingClient lineClient) : IPostbackService
{
  public async Task HandleUserPostbackAsync(PostbackPayload payload, CancellationToken ct)
  {
    
  }
}
