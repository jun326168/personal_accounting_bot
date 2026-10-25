using PersonalAccountingApi.Common.Line;

namespace PersonalAccountingApi.Templates.Messages;

public static class FollowMessage
{
  public static LineMessage BuildFollowMessage()
  {
    string content = """

      """;

    return new TextMessage(content);
  }
}