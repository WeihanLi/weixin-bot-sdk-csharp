namespace Weixin.Bot.Sdk.Utilities;

/// <summary>Utility to strip Markdown syntax to plain text suitable for WeChat.</summary>
internal static partial class Markdown
{
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var result = markdown;
        result = CodeFence().Replace(result, static m => m.Groups[1].Value.Trim());
        result = Image().Replace(result, string.Empty);
        result = Link().Replace(result, "$1");
        result = TableDivider().Replace(result, string.Empty);
        result = TableRow().Replace(result, static m =>
        {
            var cells = m.Groups[1].Value
                .Split('|')
                .Select(static cell => cell.Trim());
            return string.Join("  ", cells);
        });
        result = Bold().Replace(result, "$2");
        result = Italic().Replace(result, "$2");
        result = Strike().Replace(result, "$1");
        result = Heading().Replace(result, string.Empty);
        result = Quote().Replace(result, string.Empty);
        result = InlineCode().Replace(result, "$1");
        return result;
    }

    [GeneratedRegex(@"```[^\n]*\n?([\s\S]*?)```", RegexOptions.Compiled)]
    private static partial Regex CodeFence();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)", RegexOptions.Compiled)]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled)]
    private static partial Regex Link();

    [GeneratedRegex(@"^\|[\s:|-]+\|$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex TableDivider();

    [GeneratedRegex(@"^\|(.+)\|$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex TableRow();

    [GeneratedRegex(@"(\*\*|__)(.*?)\1", RegexOptions.Compiled)]
    private static partial Regex Bold();

    [GeneratedRegex(@"(\*|_)(.*?)\1", RegexOptions.Compiled)]
    private static partial Regex Italic();

    [GeneratedRegex(@"~~(.*?)~~", RegexOptions.Compiled)]
    private static partial Regex Strike();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^[>\s]*>", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex Quote();

    [GeneratedRegex(@"`([^`]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCode();
}
