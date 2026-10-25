using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using PersonalAccountingApi.Options;
using PersonalAccountingApi.Infrastructure.ExternalApis;

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

    // LINE Messaging API client
    services.AddHttpClient<ILineMessagingClient, LineMessagingClient>((sp, client) =>
    {
      var opt = sp.GetRequiredService<IOptions<LineOptions>>().Value;
      client.BaseAddress = new Uri("https://api.line.me/");
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opt.ChannelAccessToken);
      client.Timeout = TimeSpan.FromSeconds(10);
    });

    return services;
  }

  // add webhooks extension
  public static IServiceCollection AddWebhooks(this IServiceCollection services, IConfiguration c)
  {
    // register infra
    services.addScoped<ILineMessagingClient, LineMessagingClient>();

    // register services
    services.addScoped<IFollowService, FollowService>();
    services.addScoped<IMessageService, MessageService>();
    services.addScoped<IPostbackService, PostbackService>();

    // register handlers & dispatcher
    services.AddScoped<IWebhookHandler, FollowHandler>();
    services.AddScoped<IWebhookHandler, MessageHandler>();
    services.AddScoped<IWebhookHandler, PostbackHandler>();
    services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();

    return services;
  }
}