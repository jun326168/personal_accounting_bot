using PersonalAccountingApi.Webhooks;
using PersonalAccountingApi.Webhooks.Handlers;
using PersonalAccountingApi.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppOptions();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddWebhooks();

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 1_000_000;
});

var app = builder.Build();

// Parameter binding reads the body before endpoint filters run, so buffer it here
// to let LineSignatureFilter hash the raw bytes afterwards.
app.Use((ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/webhook"))
        ctx.Request.EnableBuffering();
    return next();
});

app.MapGet("/health", () => Results.Ok());

app.MapWebhookEndpoints();

app.Run();
