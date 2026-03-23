using System.Text.RegularExpressions;

namespace GroundNotes.Services;

internal static partial class NotePreviewFormatter
{
    public static string Build(string body, int maxLength = 96)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var preview = body.Replace("\r\n", "\n", StringComparison.Ordinal);
        preview = ImageRegex().Replace(preview, "${alt}");
        preview = LinkRegex().Replace(preview, "${label}");
        preview = HtmlTagRegex().Replace(preview, string.Empty);
        preview = FenceMarkerRegex().Replace(preview, string.Empty);
        preview = InlineCodeRegex().Replace(preview, "${code}");
        preview = EmphasisRegex().Replace(preview, "${text}");
        preview = LineMarkerRegex().Replace(preview, "${text}");
        preview = HorizontalRuleRegex().Replace(preview, string.Empty);
        preview = preview.ReplaceLineEndings(" ");
        preview = WhitespaceRegex().Replace(preview, " ").Trim();

        if (preview.Length <= maxLength)
        {
            return preview;
        }

        return preview[..maxLength] + "...";
    }

    [GeneratedRegex("!\\[(?<alt>[^\\]]*)\\]\\((?<url>[^)]+)\\)", RegexOptions.Compiled)]
    private static partial Regex ImageRegex();

    [GeneratedRegex("\\[(?<label>[^\\]]+)\\]\\((?<url>[^)]+)\\)", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("^```.*$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex FenceMarkerRegex();

    [GeneratedRegex("`(?<code>[^`]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex("(\\*\\*|__|\\*|_|~~)(?<text>.+?)(\\1)", RegexOptions.Compiled)]
    private static partial Regex EmphasisRegex();

    [GeneratedRegex("^\\s*(?:#{1,6}|>|[-+*]|\\d+[.)])(?:\\s+\\[(?: |x|X)\\])?\\s*(?<text>.*)$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex LineMarkerRegex();

    [GeneratedRegex("^\\s*(?:---|\\*\\*\\*|___)\\s*$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
