using System.Text.Json.Nodes;

namespace PersonalAccountingApi.Infrastructure.AiTools;

public sealed record AiToolDefinition(string Name, string Description, JsonObject ParametersSchema);
public sealed record AiToolCall(string Id, string Name, JsonObject Arguments);

public abstract record AiToolResult;
public sealed record ToolOutput(string Content, bool IsError = false) : AiToolResult;  // back to the model
public sealed record ToolCompleted(long ProposalId) : AiToolResult; // ends the loop

public sealed record ToolContext(long UserId, long MessageId);

public interface IAiTool
{
  AiToolDefinition Definition { get; }

  // Loop tool: returns a result string fed back to the model.
  // Terminal tool: returns a reply that ends the turn.
  Task<AiToolResult> ExecuteAsync(AiToolCall call, ToolContext ctx, CancellationToken ct);
}
