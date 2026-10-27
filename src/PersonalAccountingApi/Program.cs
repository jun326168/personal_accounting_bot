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

app.MapGet("/health", () => Results.Ok());

app.MapWebhookEndpoints();

app.Run();
