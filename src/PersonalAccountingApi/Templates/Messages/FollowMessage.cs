using PersonalAccountingApi.Common.Line;

namespace PersonalAccountingApi.Templates.Messages;

public static class FollowMessage
{
  public static LineMessage BuildFollowMessage()
  {
    string content = """
      歡迎使用 Double-entry 個人帳務管家！🫰🏼
      
      為了建立你的第一份「資產負債表」，直接回覆你的初始資產與負債：
      
      例如：現金 12000, 中信銀行 85000, 信用卡未繳卡費 6500, ...
      """;

    return new TextMessage(content);
  }
}