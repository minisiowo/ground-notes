namespace GroundNotes.Models;

public sealed record CustomSlashCommandMutationResult(
    IReadOnlyList<CustomSlashCommandDefinition> Commands,
    IReadOnlyList<CustomSlashCommandCatalogWarning> Warnings,
    bool Succeeded)
{
    public CustomSlashCommandMutationResult(IReadOnlyList<CustomSlashCommandDefinition> commands, bool succeeded)
        : this(commands, [], succeeded) { }
}
