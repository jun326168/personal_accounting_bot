using PersonalAccountingApi.Infrastructure.AiTools;

namespace PersonalAccountingApi.Infrastructure.ExternalApis;

// what the model returned this turn (loop)
public abstract record AiTurn;
public sealed record AiTextTurn(string Text) : AiTurn;
public sealed record AiToolCallTurn(IReadOnlyList<AiToolCall> Calls, object? ProviderData = null) : AiTurn;

public enum AiRole { User, Assistant, Tool };
public sealed record AiMessage(
  AiRole Role, 
  string? Text, 
  IReadOnlyList<AiToolCall>? ToolCalls = null, 
  string? ToolCallId = null, 
  bool IsError = false, 
  object? ProviderData = null);

// AI API client used by MessageService to generate the real response.
public interface IAiClient
{
  Task<AiTurn> CompleteAsync(string systemPrompt, IReadOnlyList<AiMessage> messages, IReadOnlyList<AiToolDefinition> tools, CancellationToken ct);
}