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
  public static IResult ReceiveWebhook(WebhookDto dto, IWebhookQueue queue)
  => queue.TryEnqueue(dto)
    ? Results.Accepted()
    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

}