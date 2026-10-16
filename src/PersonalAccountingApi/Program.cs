using PersonalAccountingApi;

var builder = WebApplication.CreateBuilder(args);

// Secrets come from environment variables (Line__ChannelSecret, Line__ChannelAccessToken).
// The app refuses to start if they are missing.
builder.Services.AddOptions<LineOptions>()
    .BindConfiguration("Line")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 1_000_000;
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.MapPost("/webhook", () =>
    {
        // TODO: parse and handle LINE events here.
        return Results.Ok();
    })
    .AddEndpointFilter<LineSignatureFilter>();

app.Run();
