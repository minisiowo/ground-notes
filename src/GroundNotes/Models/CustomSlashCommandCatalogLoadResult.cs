namespace GroundNotes.Models;

public sealed record CustomSlashCommandCatalogLoadResult(
    IReadOnlyList<CustomSlashCommandDefinition> Commands,
    IReadOnlyList<CustomSlashCommandCatalogWarning> Warnings)
{
    public static CustomSlashCommandCatalogLoadResult Empty { get; } = new([], []);
}
