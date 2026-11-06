using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using PersonalAccountingApi.Options;
using PersonalAccountingApi.Infrastructure.AiTools;

namespace PersonalAccountingApi.Infrastructure.ExternalApis;

public class AiClient(
  AnthropicClient client,
  IOptions<AnthropicOptions> options) : IAiClient
{
  public async Task<AiTurn> CompleteAsync(string systemPrompt, IReadOnlyList<AiMessage> messages, IReadOnlyList<AiToolDefinition> tools, CancellationToken ct)
  {
    var response = await client.Messages.Create(new MessageCreateParams
    {
      Model = options.Value.Model,
      MaxTokens = options.Value.MaxTokens,
      System = systemPrompt,
      Messages = ToMessageParams(messages),
      Tools = tools.Select(t => (ToolUnion)ToTool(t)).ToList(),
      OutputConfig = new OutputConfig { Effort = Effort.Medium },
    }, ct);

    List<ContentBlockParam> echo = [];
    List<string> texts = [];
    List<AiToolCall> calls = [];
    foreach (var block in response.Content)
    {
      if (block.TryPickText(out TextBlock? t))
      {
        texts.Add(t.Text);
        echo.Add(new TextBlockParam { Text = t.Text });
      }
      else if (block.TryPickThinking(out ThinkingBlock? th))
        echo.Add(new ThinkingBlockParam { Thinking = th.Thinking, Signature = th.Signature });
      else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? rt))
        echo.Add(new RedactedThinkingBlockParam { Data = rt.Data });
      else if (block.TryPickToolUse(out ToolUseBlock? tu))
      {
        calls.Add(new AiToolCall(tu.ID, tu.Name, ToJsonObject(tu.Input)));
        echo.Add(new ToolUseBlockParam { ID = tu.ID, Name = tu.Name, Input = tu.Input });
      }
    }

    if (calls.Count > 0) return new AiToolCallTurn(calls, echo);
    return new AiTextTurn(string.Join("\n", texts).Trim());
  }

  // map ai messages to anthropic message param
  private static List<MessageParam> ToMessageParams(IReadOnlyList<AiMessage> messages)
  {
    List<MessageParam> result = [];
    List<ContentBlockParam> pending = [];
    void Flush() { if (pending.Count == 0) return; result.Add(new() { Role = Role.User, Content = pending }); pending = []; }

    foreach (var m in messages)
    {
      if (m.Role == AiRole.Tool)
      {
        pending.Add(new ToolResultBlockParam { ToolUseID = m.ToolCallId!, Content = m.Text ?? "", IsError = m.IsError });
        continue;
      }
      Flush();
      if (m.Role == AiRole.User) { result.Add(new() { Role = Role.User, Content = m.Text ?? "" }); continue; }

      if (m.ProviderData is List<ContentBlockParam> echo) // replay the original blocks verbatim
        result.Add(new() { Role = Role.Assistant, Content = echo });
      else  // otherwise (e.g. loaded from DB) rebuild
      {
        List<ContentBlockParam> blocks = [];
        if (!string.IsNullOrEmpty(m.Text)) blocks.Add(new TextBlockParam { Text = m.Text });
        foreach (var c in m.ToolCalls ?? [])
          blocks.Add(new ToolUseBlockParam
          {
            ID = c.Id,
            Name = c.Name,
            Input = c.Arguments.ToDictionary(kv => kv.Key, kv => JsonSerializer.SerializeToElement(kv.Value))
          });
        result.Add(new() { Role = Role.Assistant, Content = blocks });
      }
    }
    Flush();
    return result;
  }

  // map ai tool to anthropic tool
  private static Tool ToTool(AiToolDefinition d)
  {
    var props = new Dictionary<string, JsonElement>();
    if (d.ParametersSchema["properties"] is JsonObject p)
      foreach (var (name, schema) in p) props[name] = JsonSerializer.SerializeToElement(schema);
    var required = d.ParametersSchema["required"] is JsonArray r ? r.Select(n => n!.GetValue<string>()).ToList() : [];
    return new Tool { Name = d.Name, Description = d.Description, InputSchema = new() { Properties = props, Required = required } };
  }

  private static JsonObject ToJsonObject(IReadOnlyDictionary<string, JsonElement> input)
  {
    var o = new JsonObject();
    foreach (var (k, v) in input) o[k] = JsonNode.Parse(v.GetRawText());
    return o;
  }

}