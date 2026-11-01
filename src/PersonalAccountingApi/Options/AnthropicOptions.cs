using System.ComponentModel.DataAnnotations;

namespace PersonalAccountingApi.Options;

public sealed class AnthropicOptions
{
  [Required, MinLength(1)] public string ApiKey { get; init; } = "";
  [Required, MinLength(1)] public string Model { get; init; } = "claude-haiku-5-5";
  public int MaxTokens { get; init; } = 16000;
}