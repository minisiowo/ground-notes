namespace GroundNotes.Models;

public sealed record SettingsSlashCommandActions(
    Func<CustomSlashCommandDefinition, string?, Task<CustomSlashCommandMutationResult>> SaveCommandAsync,
    Func<CustomSlashCommandDefinition, Task<CustomSlashCommandMutationResult>> DeleteCommandAsync,
    Func<Task<CustomSlashCommandCatalogLoadResult>> ReloadCommandsAsync);
