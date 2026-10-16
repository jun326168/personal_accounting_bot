using System.ComponentModel.DataAnnotations;

namespace PersonalAccountingApi;

public sealed class LineOptions
{
    [Required, MinLength(1)]
    public string ChannelSecret { get; init; } = "";

    [Required, MinLength(1)]
    public string ChannelAccessToken { get; init; } = "";
}
