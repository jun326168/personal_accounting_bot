using PersonalAccountingApi.Common.Line;

namespace PersonalAccountingApi.Templates.Messages;

public static class FollowMessage
{
  public static LineMessage BuildFollowMessage()
  {
    string content = """
      歡迎使用 Double-entry 個人帳務管家！
      與一般單式記帳不同，我採用企業級的複式記帳（Double-entry），幫你追蹤「資產、負債與真實淨值」
      為了建立你的第一份「資產負債表」，請先完成「開帳設定」，設定你的初始資產與負債：
      直接輸入你目前的口袋現金、銀行存款、信用卡未繳卡費、信貸、學貸等等
      例如：現金 12000, 中信銀行 85000, ...
      """;

    return new TextMessage(content);
  }
}