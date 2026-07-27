namespace GroundNotes.Models;

public enum VimClipboardMode
{
    ExplicitSystemRegister,
    InternalOnly,
    UnnamedSystemClipboard
}

public sealed record VimModeSettings(
    bool IsEnabled,
    string LeaderKey,
    int KeySequenceTimeoutMilliseconds,
    int WhichKeyDelayMilliseconds,
    bool UseStandardCtrlBindings,
    VimClipboardMode ClipboardMode,
    bool ShowStatus)
{
    public const string DefaultLeaderKey = "<Space>";
    public const int DefaultKeySequenceTimeoutMilliseconds = 1000;
    public const int MinKeySequenceTimeoutMilliseconds = 100;
    public const int MaxKeySequenceTimeoutMilliseconds = 5000;
    public const int DefaultWhichKeyDelayMilliseconds = 220;
    public const int MinWhichKeyDelayMilliseconds = 0;
    public const int MaxWhichKeyDelayMilliseconds = 1000;

    public static VimModeSettings Default { get; } = new(
        IsEnabled: false,
        LeaderKey: DefaultLeaderKey,
        KeySequenceTimeoutMilliseconds: DefaultKeySequenceTimeoutMilliseconds,
        WhichKeyDelayMilliseconds: DefaultWhichKeyDelayMilliseconds,
        UseStandardCtrlBindings: true,
        ClipboardMode: VimClipboardMode.ExplicitSystemRegister,
        ShowStatus: true);

    public static VimModeSettings Normalize(VimModeSettings? settings)
    {
        if (settings is null)
        {
            return Default;
        }

        return settings with
        {
            LeaderKey = NormalizeLeaderKey(settings.LeaderKey),
            KeySequenceTimeoutMilliseconds = Math.Clamp(
                settings.KeySequenceTimeoutMilliseconds,
                MinKeySequenceTimeoutMilliseconds,
                MaxKeySequenceTimeoutMilliseconds),
            WhichKeyDelayMilliseconds = Math.Clamp(
                settings.WhichKeyDelayMilliseconds,
                MinWhichKeyDelayMilliseconds,
                MaxWhichKeyDelayMilliseconds),
            ClipboardMode = Enum.IsDefined(settings.ClipboardMode)
                ? settings.ClipboardMode
                : Default.ClipboardMode
        };
    }

    private static string NormalizeLeaderKey(string? leaderKey)
    {
        var normalized = leaderKey?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return DefaultLeaderKey;
        }

        return normalized.Equals("Space", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(DefaultLeaderKey, StringComparison.OrdinalIgnoreCase)
            ? DefaultLeaderKey
            : normalized;
    }
}
