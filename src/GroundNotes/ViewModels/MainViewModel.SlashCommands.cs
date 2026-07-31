using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.ViewModels;

public partial class MainViewModel
{
    private long _slashCommandLoadGeneration;

    public string CurrentSlashCommandsDirectory => !HasSelectedFolder || _customSlashCommandCatalogService is null
        ? string.Empty
        : _customSlashCommandCatalogService.GetNotesFolderCommandsDirectory(NotesFolder);

    private SettingsSlashCommandActions BuildSettingsSlashCommandActions() => new(
        SaveCustomSlashCommandFromSettingsAsync,
        DeleteCustomSlashCommandFromSettingsAsync,
        ReloadCustomSlashCommandsFromSettingsAsync);

    private async Task<CustomSlashCommandMutationResult> SaveCustomSlashCommandFromSettingsAsync(CustomSlashCommandDefinition command, string? originalId)
    {
        var folder = NotesFolder;
        if (_customSlashCommandEditorService is null || string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "Choose a notes folder first.";
            return new(CustomSlashCommands, CustomSlashCommandWarnings, false);
        }
        try
        {
            if (string.IsNullOrWhiteSpace(originalId) || string.Equals(originalId, command.Id, StringComparison.OrdinalIgnoreCase))
                await _customSlashCommandEditorService.SaveCustomCommandAsync(folder, command);
            else
                await _customSlashCommandEditorService.SaveCustomCommandAsync(folder, command, originalId);
            var result = await LoadCustomSlashCommandsAsync(folder);
            if (!string.Equals(folder, NotesFolder, StringComparison.OrdinalIgnoreCase))
                return new(CustomSlashCommands, CustomSlashCommandWarnings, false);
            StatusMessage = $"Saved slash command \"{command.Name}\".";
            return new(result.Commands, result.Warnings, true);
        }
        catch (IOException) { StatusMessage = "Could not save slash command."; return new(CustomSlashCommands, CustomSlashCommandWarnings, false); }
        catch (UnauthorizedAccessException) { StatusMessage = "Could not save slash command."; return new(CustomSlashCommands, CustomSlashCommandWarnings, false); }
        catch (InvalidOperationException ex) { StatusMessage = ex.Message; return new(CustomSlashCommands, CustomSlashCommandWarnings, false); }
        catch (ArgumentException ex) { StatusMessage = ex.Message; return new(CustomSlashCommands, CustomSlashCommandWarnings, false); }
    }

    private async Task<CustomSlashCommandMutationResult> DeleteCustomSlashCommandFromSettingsAsync(CustomSlashCommandDefinition command)
    {
        var folder = NotesFolder;
        if (_customSlashCommandEditorService is null || string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "Choose a notes folder first.";
            return new(CustomSlashCommands, CustomSlashCommandWarnings, false);
        }
        try
        {
            await _customSlashCommandEditorService.DeleteCustomCommandAsync(folder, command.Id);
            var result = await LoadCustomSlashCommandsAsync(folder);
            if (!string.Equals(folder, NotesFolder, StringComparison.OrdinalIgnoreCase))
                return new(CustomSlashCommands, CustomSlashCommandWarnings, false);
            StatusMessage = $"Deleted slash command \"{command.Name}\".";
            return new(result.Commands, result.Warnings, true);
        }
        catch (IOException) { StatusMessage = "Could not delete slash command."; return new(CustomSlashCommands, CustomSlashCommandWarnings, false); }
        catch (UnauthorizedAccessException) { StatusMessage = "Could not delete slash command."; return new(CustomSlashCommands, CustomSlashCommandWarnings, false); }
    }

    private async Task<CustomSlashCommandCatalogLoadResult> ReloadCustomSlashCommandsFromSettingsAsync()
    {
        var folder = NotesFolder;
        var result = await LoadCustomSlashCommandsAsync(folder);
        if (!string.Equals(folder, NotesFolder, StringComparison.OrdinalIgnoreCase))
            return new(CustomSlashCommands, CustomSlashCommandWarnings);
        StatusMessage = result.Warnings.Count > 0 ? $"Loaded {result.Commands.Count} slash commands. {result.Warnings.Count} warning(s)." : "Slash commands reloaded.";
        return result;
    }

    private async Task<CustomSlashCommandCatalogLoadResult> LoadCustomSlashCommandsAsync(string? folder = null)
    {
        var target = folder ?? NotesFolder;
        if (_customSlashCommandCatalogService is null || string.IsNullOrWhiteSpace(target))
        {
            CustomSlashCommands = [];
            CustomSlashCommandWarnings = [];
            return CustomSlashCommandCatalogLoadResult.Empty;
        }
        var generation = Interlocked.Increment(ref _slashCommandLoadGeneration);
        var result = await _customSlashCommandCatalogService.LoadCommandsAsync(target);
        if (generation == _slashCommandLoadGeneration && string.Equals(target, NotesFolder, StringComparison.OrdinalIgnoreCase))
        {
            CustomSlashCommands = result.Commands;
            CustomSlashCommandWarnings = result.Warnings;
            OnPropertyChanged(nameof(CurrentSlashCommandsDirectory));
            if (result.Warnings.Count > 0) StatusMessage = $"Loaded {result.Commands.Count} slash commands. {result.Warnings.Count} warning(s).";
        }
        return result;
    }

    private static string BuildFolderLoadStatus(
        AiPromptCatalogLoadResult prompts,
        CustomSlashCommandCatalogLoadResult commands)
    {
        if (prompts.Warnings.Count == 0 && commands.Warnings.Count == 0)
        {
            return "Ready.";
        }

        var warningParts = new List<string>();
        if (prompts.Warnings.Count > 0)
        {
            warningParts.Add($"{prompts.Warnings.Count} AI prompt warning(s)");
        }
        if (commands.Warnings.Count > 0)
        {
            warningParts.Add($"{commands.Warnings.Count} slash command warning(s)");
        }

        return $"Loaded with {string.Join(" and ", warningParts)}.";
    }
}
