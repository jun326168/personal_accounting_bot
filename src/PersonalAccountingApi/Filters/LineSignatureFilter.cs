using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PersonalAccountingApi.Options;

namespace PersonalAccountingApi.Filters;

/// <summary>
/// Rejects requests whose X-Line-Signature does not match the HMAC-SHA256 of the raw body.
/// Apply to any endpoint LINE calls: <c>.AddEndpointFilter&lt;LineSignatureFilter&gt;()</c>.
/// </summary>
public sealed class LineSignatureFilter(IOptions<LineOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        // Body is buffered by middleware (binding has already read it); rewind to hash the raw bytes.
        request.Body.Position = 0;
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, context.HttpContext.RequestAborted);
        request.Body.Position = 0;

        if (!IsValid(ms.ToArray(), request.Headers["X-Line-Signature"].ToString()))
            return Results.Unauthorized();

        return await next(context);
    }

    private bool IsValid(byte[] body, string signature)
    {
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.ChannelSecret), body);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
