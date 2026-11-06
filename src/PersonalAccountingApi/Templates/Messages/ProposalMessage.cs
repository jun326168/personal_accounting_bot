using System.Text.Json.Nodes;
using PersonalAccountingApi.Common.Line;
using PersonalAccountingApi.Domain.Entities;

namespace PersonalAccountingApi.Templates.Messages;

public static class ProposalMessage
{
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
        ["contents"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = summary, ["wrap"] = true }),
      },
      ["footer"] = new JsonObject
      {
        ["type"] = "box",
        ["layout"] = "horizontal",
        ["spacing"] = "md",
        ["contents"] = new JsonArray(
          Button("確認", $"action=confirm&proposalId={proposalId}", "primary"),
          Button("取消", $"action=cancel&proposalId={proposalId}", "secondary")),
      },
    };

    // altText is shown in notifications and the chat list (max 400 chars)
    var altText = summary.Length <= 400 ? summary : summary[..399] + "…";
    return new FlexMessage(altText, bubble);
  }

  private static JsonObject Button(string label, string data, string style) => new()
  {
    ["type"] = "button",
    ["style"] = style,
    ["action"] = new JsonObject { ["type"] = "postback", ["label"] = label, ["data"] = data },
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
