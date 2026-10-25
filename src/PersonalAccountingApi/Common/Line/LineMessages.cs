using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PersonalAccountingApi.Common.Line;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextMessage), "text")]
[JsonDerivedType(typeof(FlexMessage), "flex")]
[JsonDerivedType(typeof(TemplateMessage), "template")]
public abstract record LineMessage;

public sealed record TextMessage(string Text) : LineMessage;

public sealed record FlexMessage(string AltText, JsonNode Contents) : LineMessage
{
  public static FlexMessage FromJson(string altText, string json)
    => new(altText, JsonNode.Parse(json)!);
}

public sealed record TemplateMessage(string AltText, LineTemplate Template) : LineMessage;

// ---- Template ----

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ButtonsTemplate), "buttons")]
[JsonDerivedType(typeof(ConfirmTemplate), "confirm")]
public abstract record LineTemplate;

public sealed record ButtonsTemplate(
  string Text,
  IReadOnlyList<LineAction> Actions,
  string? Title = null,
  string? ThumbnailImageUrl = null) : LineTemplate;

public sealed record ConfirmTemplate(string Text, IReadOnlyList<LineAction> Actions) : LineTemplate;

// ---- Action ----

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PostbackAction), "postback")]
[JsonDerivedType(typeof(MessageAction), "message")]
[JsonDerivedType(typeof(UriAction), "uri")]
public abstract record LineAction;

public sealed record PostbackAction(string Label, string Data, string? DisplayText = null) : LineAction;
public sealed record MessageAction(string Label, string Text) : LineAction;
public sealed record UriAction(string Label, string Uri) : LineAction;