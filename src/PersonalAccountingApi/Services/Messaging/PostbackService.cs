using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Templates.Messages;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Services.Messaging;

public sealed class PostbackService() : IPostbackService
{
  public Task HandleUserPostbackAsync(PostbackPayload payload, CancellationToken ct)
    => Task.CompletedTask;
}
