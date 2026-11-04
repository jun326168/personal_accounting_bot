namespace PersonalAccountingApi.Infrastructure.Data;

public static class DataHelper
{
  public static string EscapeILikePattern(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;

    return input
        .Replace("\\", "\\\\") // Escape backslash first
        .Replace("%", "\\%")   // Escape percent
        .Replace("_", "\\_");  // Escape underscore
  }
}