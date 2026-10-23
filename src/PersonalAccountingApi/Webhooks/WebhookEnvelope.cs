using System.Text.Json;

namespace PersonalAccountingApi.Webhooks;

public sealed record WebhookEnvelope(string Id, string Type, DateTimeOffset CreatedAt, JsonElement Data);

public static class JsonDefaults
{
    // Providers send snake_case: payment_id -> PaymentId. Web defaults make matching case-insensitive.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}