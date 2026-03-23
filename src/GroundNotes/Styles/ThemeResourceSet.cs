using Avalonia.Media;

namespace GroundNotes.Styles;

public sealed class ThemeResourceSet
{
    public required IReadOnlyDictionary<string, SolidColorBrush> Brushes { get; init; }
    public required IReadOnlyDictionary<string, Color> Colors { get; init; }
}
