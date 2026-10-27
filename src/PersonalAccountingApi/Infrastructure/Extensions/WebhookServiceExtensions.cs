using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using PersonalAccountingApi.Options;
using PersonalAccountingApi.Webhooks;
using PersonalAccountingApi.Webhooks.Handlers;
using PersonalAccountingApi.Services.Messaging;
using PersonalAccountingApi.Infrastructure.Data;
using PersonalAccountingApi.Infrastructure.ExternalApis;

namespace PersonalAccountingApi.Infrastructure.Extensions;

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
  public static IServiceCollection AddWebhooks(this IServiceCollection services)
  {
    // register infra
    services.AddScoped<ILineMessagingClient, LineMessagingClient>();

    // register services
    services.AddScoped<IFollowService, FollowService>();
    services.AddScoped<IMessageService, MessageService>();
    services.AddScoped<IPostbackService, PostbackService>();

    // register handlers & dispatcher
    services.AddScoped<IWebhookHandler, FollowHandler>();
    services.AddScoped<IWebhookHandler, MessageHandler>();
    services.AddScoped<IWebhookHandler, PostbackHandler>();
    services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();

    return services;
  }

  // add db persistence
  public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
  {
    var url = config["DATABASE_URL"]
      ?? throw new InvalidOperationException("DATABASE_URL is not set.");
    var connectionString = ToNpgsqlConnectionString(url);

    services.AddDbContext<AppDbContext>(o => o
      .UseNpgsql(connectionString)
      .UseSnakeCaseNamingConvention());

    return services;
  }

  // Npgsql does not accept the postgresql:// URI form, so convert it to a key/value connection string.
  private static string ToNpgsqlConnectionString(string url)
  {
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);

    return new NpgsqlConnectionStringBuilder
    {
      Host = uri.Host,
      Port = uri.IsDefaultPort ? 5432 : uri.Port,
      Database = uri.AbsolutePath.TrimStart('/'),
      Username = Uri.UnescapeDataString(userInfo[0]),
      Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
    }.ConnectionString;
  }
}
