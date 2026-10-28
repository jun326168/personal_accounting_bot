using PersonalAccountingApi.Common.Line;

namespace PersonalAccountingApi.Templates.Messages;

public static class ThinkingMessage
{
  public static LineMessage BuildThinkingMessage()
  {
    string content = """
      💭 正在分析你的訊息...
      """;

    return new TextMessage(content);
  }
}