using PersonalAccountingApi.Common.Line;

namespace PersonalAccountingApi.Templates.Messages;

public static class PostbackMessage
{
  public const string AlreadyHandled = "已經處理過或找不到這筆提案 🥲";
  public const string Cancelled = "已取消，沒有記錄任何東西 👌🏼";
  public const string Recorded = "已記錄 📍";
  public static string RecordedMultiple(int count) => $"已記錄 {count} 筆 📍";
  public const string Reversed = "已沖銷 🔄";
}