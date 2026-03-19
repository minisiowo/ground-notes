namespace QuickNotesTxt.Models;

public readonly record struct EditorLayoutSettings(int IndentationSize, double LineHeightFactor)
{
    public static EditorLayoutSettings Normalize(EditorLayoutSettings settings)
        => new(
            EditorDisplaySettings.NormalizeIndentSize(settings.IndentationSize),
            EditorDisplaySettings.NormalizeLineHeightFactor(settings.LineHeightFactor));
}
