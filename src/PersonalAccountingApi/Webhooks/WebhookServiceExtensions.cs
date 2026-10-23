using PersonalAccountingApi.Options;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalAccountingApi.Webhooks;
namespace PersonalAccountingApi.Webhooks.Handlers;

public static class WebhookServiceExtensions
{
  // add line options (verify the signature)
  public static IServiceCollection AddAppOptions(this IServiceCollection services)
  {
    // Secrets come from environment variables (Line__ChannelSecret, Line__ChannelAccessToken).
    // The app refuses to start if they are missing.
    services.AddOptions<LineOptions>()
      .BindConfiguration("Line")
      .ValidateDataAnnotations()
      .ValidateOnStart();
  }

  // add webhooks extension
  public static IServiceCollection AddWebhooks(this IServiceCollection services, IConfiguration c)
  {
    // register
    services.AddScoped<IWebhookHandler, FollowHandler>();
    services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();

    return services;
  }
}