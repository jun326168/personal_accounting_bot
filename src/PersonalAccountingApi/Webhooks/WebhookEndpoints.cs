using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PersonalAccountingApi.Filters;

namespace PersonalAccountingApi.Webhooks;

public static class WebhookEndpoints
{
  public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
  {
    app.MapPost("/webhook", ReceiveWebhook)
      .AddEndpointFilter<LineSignatureFilter>();
    return app;
  }

  // dispatch to different handlers
  public static async Task<IResult> ReceiveWebhook(
    WebhookDto dto,
    IWebhookDispatcher dispatcher,
    CancellationToken ct)
  {
    await dispatcher.DispatchAsync(dto, ct);
    return Results.Accepted();
  }
}