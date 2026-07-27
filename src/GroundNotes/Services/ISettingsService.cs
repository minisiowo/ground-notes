using GroundNotes.Models;

namespace GroundNotes.Services;

public interface ISettingsService
{
    AppSettings GetSettingsSync();

    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    void SaveSettingsSync(AppSettings settings);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);

    void UpdateSettingsSync(Func<AppSettings, AppSettings> update);

    Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default);

    Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default);
}

public sealed record AppSettings(
    string? NotesFolder,
    double? EditorFontSize,
    double? UiFontSize,
    int? EditorIndentSize,
    double? EditorLineHeightFactor,
    string? FontName,
    string? FontVariantName,
    string? SidebarFontName,
    string? SidebarFontVariantName,
    string? CodeFontName,
    string? CodeFontVariantName,
    string? ThemeName,
    bool ShowYamlFrontMatterInEditor,
    bool ShowScrollBars,
    WindowLayout? WindowLayout,
    AiSettings AiSettings,
    KeyboardShortcutSettings? KeyboardShortcuts = null,
    double? SidebarFontSize = null,
    bool ShowSidebarListBackground = true,
    bool ShowSidebarListBorder = true,
    string? FileListFontName = null,
    string? FileListFontVariantName = null,
    double? FileListFontSize = null,
    string? UiFontName = null,
    string? UiFontVariantName = null,
    NoteWindowLayout? StandardNoteWindowLayout = null,
    NoteWindowLayout? ZenNoteWindowLayout = null,
    VimModeSettings? VimModeSettings = null);

public sealed record WindowLayout(
    double Width,
    double Height,
    double X,
    double Y,
    bool IsMaximized,
    double? SidebarWidth = null,
    bool? SidebarCollapsed = null,
    bool? SidebarCalendarExpanded = null,
    double? EditorCanvasWidth = null,
    IReadOnlyList<double>? PaneSplitWeights = null,
    double? MultiPaneSharedWidth = null,
    IReadOnlyList<string>? SidebarExpandedTagPaths = null)
{
    public bool Equals(WindowLayout? other)
    {
        return other is not null
               && Width.Equals(other.Width)
               && Height.Equals(other.Height)
               && X.Equals(other.X)
               && Y.Equals(other.Y)
               && IsMaximized == other.IsMaximized
               && Nullable.Equals(SidebarWidth, other.SidebarWidth)
               && SidebarCollapsed == other.SidebarCollapsed
               && SidebarCalendarExpanded == other.SidebarCalendarExpanded
               && Nullable.Equals(EditorCanvasWidth, other.EditorCanvasWidth)
               && SequenceEqual(PaneSplitWeights, other.PaneSplitWeights)
               && Nullable.Equals(MultiPaneSharedWidth, other.MultiPaneSharedWidth)
               && SequenceEqual(SidebarExpandedTagPaths, other.SidebarExpandedTagPaths, StringComparer.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Width);
        hash.Add(Height);
        hash.Add(X);
        hash.Add(Y);
        hash.Add(IsMaximized);
        hash.Add(SidebarWidth);
        hash.Add(SidebarCollapsed);
        hash.Add(SidebarCalendarExpanded);
        hash.Add(EditorCanvasWidth);
        AddSequence(ref hash, PaneSplitWeights);
        hash.Add(MultiPaneSharedWidth);
        AddSequence(ref hash, SidebarExpandedTagPaths, StringComparer.OrdinalIgnoreCase);
        return hash.ToHashCode();
    }

    private static bool SequenceEqual<T>(IReadOnlyList<T>? first, IReadOnlyList<T>? second, IEqualityComparer<T>? comparer = null)
    {
        return ReferenceEquals(first, second)
               || (first is not null && second is not null && first.SequenceEqual(second, comparer));
    }

    private static void AddSequence<T>(ref HashCode hash, IReadOnlyList<T>? values, IEqualityComparer<T>? comparer = null)
    {
        if (values is null)
        {
            hash.Add(0);
            return;
        }

        hash.Add(values.Count);
        foreach (var value in values)
        {
            hash.Add(value, comparer);
        }
    }
}
