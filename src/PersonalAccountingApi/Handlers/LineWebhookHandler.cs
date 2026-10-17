using System.Text.Json;
using PersonalAccountingApi.Filters;
using PersonalAccountingApi.Services;

namespace PersonalAccountingApi.Handlers;

/// <summary>
/// Parses LINE webhook payloads and routes each event to the right service.
/// </summary>
public sealed class LineWebhookHandler(
    IEnumerable<IMessageService> messageServices,
    ILineMessagingService lineMessaging,
    ILogger<LineWebhookHandler> logger)
{
    public async Task HandleAsync(Stream body, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("events", out var events)) return;

        foreach (var ev in events.EnumerateArray())
        {
            try
            {
                await HandleEventAsync(ev, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad event must not make LINE retry the whole batch.
                logger.LogError(ex, "Failed to handle LINE event");
            }
        }
    }

    private async Task HandleEventAsync(JsonElement ev, CancellationToken ct)
    {
        switch (ev.GetProperty("type").GetString())
        {
            case "message" when ev.GetProperty("message").GetProperty("type").GetString() == "text":
                var userId = ev.GetProperty("source").GetProperty("userId").GetString()!;
                var text = ev.GetProperty("message").GetProperty("text").GetString()!;
                var service = messageServices.FirstOrDefault(s => s.CanHandle(text));
                var reply = service is null
                    ? "抱歉，我無法處理這則訊息。"
                    : await service.HandleAsync(userId, text, ct);
                await lineMessaging.ReplyTextAsync(ev.GetProperty("replyToken").GetString()!, reply, ct);
                break;
        }
    }
}

public static class LineWebhookExtensions
{
    public static IServiceCollection AddLineWebhook(this IServiceCollection services)
    {
        services.AddHttpClient<ILineMessagingService, LineMessagingService>(c => c.BaseAddress = new Uri("https://api.line.me/"));

        // Message services are tried in this order; add new ones here.
        services.AddScoped<IMessageService, BookKeepingService>();

        services.AddScoped<LineWebhookHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapLineWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook", async (HttpRequest request, LineWebhookHandler handler) =>
            {
                await handler.HandleAsync(request.Body, request.HttpContext.RequestAborted);
                return Results.Ok();
            })
            .AddEndpointFilter<LineSignatureFilter>();
        return app;
    }
}
