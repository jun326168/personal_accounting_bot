using PersonalAccountingApi.Webhooks.Extensions;
using PersonalAccountingApi.Webhooks.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppOptions();
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
