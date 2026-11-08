using System.Text.Json.Nodes;
using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Domain.Entities;

namespace PersonalAccountingApi.Templates.Messages;

public static class ProposalMessage
{
  private const string Primary = "#3D4F63"; // dark gray-blue
  private const string PrimaryText = "#FFFFFF";
  private const string SecondaryBg = "#E8ECF0";
  private const string SecondaryText = "#3D4F63";
  private const string BodyText = "#2B3846";

  // confirm card as a flex bubble: summary text + confirm/cancel postback buttons
  public static FlexMessage Build(long proposalId, string summary)
  {
    var bubble = new JsonObject
    {
      ["type"] = "bubble",
      ["body"] = new JsonObject
      {
        ["type"] = "box",
        ["layout"] = "vertical",
        ["paddingAll"] = "16px",
        ["contents"] = new JsonArray(new JsonObject
        {
          ["type"] = "text",
          ["text"] = summary,
          ["wrap"] = true,
          ["size"] = "sm",
          ["color"] = BodyText,
        }),
      },
      ["footer"] = new JsonObject
      {
        ["type"] = "box",
        ["layout"] = "horizontal",
        ["spacing"] = "sm",
        ["paddingTop"] = "0px",
        ["paddingBottom"] = "16px",
        ["paddingStart"] = "16px",
        ["paddingEnd"] = "16px",
        ["contents"] = new JsonArray(
          Button("確認", $"action=confirm&proposalId={proposalId}", Primary, PrimaryText),
          Button("取消", $"action=cancel&proposalId={proposalId}", SecondaryBg, SecondaryText)),
      },
    };

    // altText is shown in notifications and the chat list (max 400 chars)
    var altText = summary.Length <= 400 ? summary : summary[..399] + "…";
    return new FlexMessage(altText, bubble);
  }

  // compact pill button: a box with a postback action (a plain "button" can't be shrunk or fully rounded)
  private static JsonObject Button(string label, string data, string background, string textColor) => new()
  {
    ["type"] = "box",
    ["layout"] = "vertical",
    ["flex"] = 1,
    ["backgroundColor"] = background,
    ["cornerRadius"] = "20px",
    ["paddingTop"] = "8px",
    ["paddingBottom"] = "8px",
    ["action"] = new JsonObject { ["type"] = "postback", ["label"] = label, ["data"] = data },
    ["contents"] = new JsonArray(new JsonObject
    {
      ["type"] = "text",
      ["text"] = label,
      ["size"] = "sm",
      ["weight"] = "bold",
      ["align"] = "center",
      ["color"] = textColor,
    }),
  };

  // ---- summary text shown on the card (zh = english ledger name -> chinese name) ----

  public static string FormatTransactions(ProposalTransaction[] transactions, IReadOnlyDictionary<string, string> zh)
  {
    var blocks = transactions.Select(t =>
    {
      var kind = t.Kind switch { ProposedKind.Opening => "（期初餘額）", ProposedKind.Adjusting => "（調整）", _ => "" };
      var lines = new List<string> { $"{t.Date:yyyy-MM-dd} {t.Description}{kind}" };
      lines.AddRange(t.Entries.Select(e =>
        $"{(e.Side == NormalBalance.Debit ? "借" : "貸")}　{zh.GetValueOrDefault(e.AccountName, e.AccountName)}　{e.Amount:N0}"));
      if (!string.IsNullOrWhiteSpace(t.Note)) lines.Add($"備註：{t.Note}");
      return string.Join("\n", lines);
    });
    return "請確認以下記帳：\n\n" + string.Join("\n\n", blocks);
  }

  public static string FormatReversal(Transaction target, decimal total, string? note)
  {
    var text = $"請確認撤銷以下交易：\n{target.Date:yyyy-MM-dd} {target.Description}　{total:N0}";
    return string.IsNullOrWhiteSpace(note) ? text : text + $"\n原因：{note}";
  }
}
