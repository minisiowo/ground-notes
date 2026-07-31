using System.Text.Json;
using System.Text.Json.Serialization;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class CustomSlashCommandEditorService : ICustomSlashCommandEditorService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly ICustomSlashCommandCatalogService _catalogService;
    private readonly HashSet<string> _reservedTokens;
    private readonly Action<string> _deleteFile;

    public CustomSlashCommandEditorService(IEnumerable<string> reservedTokens, ICustomSlashCommandCatalogService? catalogService = null)
        : this(reservedTokens, catalogService, File.Delete)
    {
    }

    internal CustomSlashCommandEditorService(IEnumerable<string> reservedTokens, ICustomSlashCommandCatalogService? catalogService, Action<string> deleteFile)
    {
        ArgumentNullException.ThrowIfNull(reservedTokens);
        ArgumentNullException.ThrowIfNull(deleteFile);
        _reservedTokens = reservedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _catalogService = catalogService ?? new CustomSlashCommandCatalogService(_reservedTokens);
        _deleteFile = deleteFile;
    }

    public Task SaveCustomCommandAsync(string notesFolder, CustomSlashCommandDefinition command, CancellationToken cancellationToken = default) =>
        SaveCustomCommandCoreAsync(notesFolder, command, null, cancellationToken);

    public Task SaveCustomCommandAsync(string notesFolder, CustomSlashCommandDefinition command, string originalCommandId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalCommandId);
        return SaveCustomCommandCoreAsync(notesFolder, command, originalCommandId, cancellationToken);
    }

    private async Task SaveCustomCommandCoreAsync(string notesFolder, CustomSlashCommandDefinition command, string? originalCommandId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        var directory = _catalogService.GetNotesFolderCommandsDirectory(notesFolder);
        Directory.CreateDirectory(directory);
        var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal).ToList();
        var entries = new List<(string Path, CustomSlashCommandDefinition? Command)>();
        foreach (var file in files)
            entries.Add((file, await ReadCommandAsync(file, cancellationToken)));

        var excludedIds = new[] { originalCommandId ?? command.Id };
        var excludedFiles = entries.Where(entry => entry.Command is not null && excludedIds.Any(id =>
            string.Equals(entry.Command.Id, id, StringComparison.OrdinalIgnoreCase))).Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);
        var targetPath = originalCommandId is null
            ? entries.FirstOrDefault(entry => entry.Command is not null && string.Equals(entry.Command.Id, command.Id, StringComparison.OrdinalIgnoreCase)).Path
            : null;
        if (string.IsNullOrEmpty(targetPath)) targetPath = GetCustomCommandFilePath(notesFolder, command.Id);

        foreach (var entry in entries)
        {
            if (excludedFiles.Contains(entry.Path)) continue;
            if (string.Equals(entry.Path, targetPath, StringComparison.Ordinal))
                throw new InvalidOperationException("Another command uses the same filename. Choose a different command ID.");
            if (entry.Command is not null && IsValidStoredCommand(entry.Command) && GetTokens(entry.Command).Any(token => TokenOverlaps(command, token)))
                throw new InvalidOperationException("Command ID or alias collides with another command token.");
        }

        var normalized = command with
        {
            Name = command.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            Aliases = command.Aliases is { Count: > 0 } ? command.Aliases : null
        };
        var temporaryFilePath = targetPath + ".tmp";
        var stagedFiles = new List<(string OriginalPath, string BackupPath)>();
        try
        {
            await using (var stream = File.Create(temporaryFilePath))
                await JsonSerializer.SerializeAsync(stream, normalized, s_jsonOptions, cancellationToken);

            var staleFiles = entries
                .Where(entry => entry.Command is not null
                    && (string.Equals(entry.Command.Id, command.Id, StringComparison.OrdinalIgnoreCase)
                        || originalCommandId is not null && string.Equals(entry.Command.Id, originalCommandId, StringComparison.OrdinalIgnoreCase)))
                .Select(entry => entry.Path)
                .Where(path => !string.Equals(path, targetPath, StringComparison.Ordinal))
                .Append(targetPath)
                .Where(File.Exists)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            foreach (var staleFile in staleFiles)
            {
                var backupPath = staleFile + ".groundnotes-backup-" + Guid.NewGuid().ToString("N");
                File.Move(staleFile, backupPath);
                stagedFiles.Add((staleFile, backupPath));
            }

            File.Move(temporaryFilePath, targetPath, overwrite: true);

            foreach (var stagedFile in stagedFiles)
            {
                try
                {
                    _deleteFile(stagedFile.BackupPath);
                }
                catch (Exception) { }
            }
        }
        catch
        {
            foreach (var stagedFile in stagedFiles.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(stagedFile.OriginalPath)) File.Delete(stagedFile.OriginalPath);
                    if (File.Exists(stagedFile.BackupPath)) File.Move(stagedFile.BackupPath, stagedFile.OriginalPath);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryFilePath)) File.Delete(temporaryFilePath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public async Task DeleteCustomCommandAsync(string notesFolder, string commandId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        var directory = _catalogService.GetNotesFolderCommandsDirectory(notesFolder);
        foreach (var file in await FindFilesByIdAsync(directory, commandId, cancellationToken)) File.Delete(file);
    }

    public string GetCustomCommandFilePath(string notesFolder, string commandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ValidateId(commandId);
        return Path.Combine(_catalogService.GetNotesFolderCommandsDirectory(notesFolder), commandId + ".json");
    }

    private void Validate(CustomSlashCommandDefinition command)
    {
        ValidateId(command.Id);
        if (string.IsNullOrWhiteSpace(command.Name)) throw new InvalidOperationException("Command name is required.");
        if (string.IsNullOrWhiteSpace(command.Template)) throw new InvalidOperationException("Command template is required.");
        var tokens = GetTokens(command);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            if (!IsValidToken(token)) throw new InvalidOperationException($"Command token '{token}' is invalid.");
            if (!seen.Add(token)) throw new InvalidOperationException($"Command token '{token}' is duplicated.");
            if (_reservedTokens.Contains(token)) throw new InvalidOperationException($"Command token '{token}' is reserved.");
        }
    }

    private void ValidateId(string commandId)
    {
        if (!IsValidToken(commandId)) throw new InvalidOperationException("Command ID must contain only lowercase ASCII letters, digits, underscores, and hyphens.");
        if (_reservedTokens.Contains(commandId)) throw new InvalidOperationException("Command ID is reserved.");
    }

    private static async Task<IReadOnlyList<string>> FindFilesByIdAsync(string directory, string commandId, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory)) return [];
        var matches = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
        {
            var command = await ReadCommandAsync(file, cancellationToken);
            if (string.Equals(command?.Id, commandId, StringComparison.OrdinalIgnoreCase)) matches.Add(file);
        }
        return matches;
    }

    private static async Task<CustomSlashCommandDefinition?> ReadCommandAsync(string file, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<CustomSlashCommandDefinition>(stream, JsonDefaults.ReadOptions, cancellationToken);
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static IReadOnlyList<string> GetTokens(CustomSlashCommandDefinition command) => new[] { command.Id }.Concat(command.Aliases ?? []).ToList();
    private static bool TokenOverlaps(CustomSlashCommandDefinition command, string token) => GetTokens(command).Any(candidate => string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase));
    private static bool IsValidToken(string token) => !string.IsNullOrEmpty(token) && string.Equals(token.Trim(), token, StringComparison.Ordinal) && IsAsciiLetterOrDigit(token[0]) && token.Skip(1).All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
    private bool IsValidStoredCommand(CustomSlashCommandDefinition command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Template)) return false;
        var tokens = GetTokens(command);
        return tokens.All(IsValidToken) && tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count() == tokens.Count && tokens.All(token => !_reservedTokens.Contains(token));
    }
    private static bool IsAsciiLetterOrDigit(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9';
}
