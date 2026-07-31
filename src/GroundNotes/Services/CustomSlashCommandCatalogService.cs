using System.Text.Json;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class CustomSlashCommandCatalogService : ICustomSlashCommandCatalogService
{
    private const string CommandsDirectoryName = "slash-commands";
    private readonly HashSet<string> _reservedTokens;

    public CustomSlashCommandCatalogService(IEnumerable<string> reservedTokens)
    {
        ArgumentNullException.ThrowIfNull(reservedTokens);
        _reservedTokens = reservedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string GetNotesFolderCommandsDirectory(string notesFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        return Path.Combine(notesFolder, ".groundnotes", CommandsDirectoryName);
    }

    public async Task<CustomSlashCommandCatalogLoadResult> LoadCommandsAsync(
        string notesFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        var directory = GetNotesFolderCommandsDirectory(notesFolder);
        if (!Directory.Exists(directory))
        {
            return CustomSlashCommandCatalogLoadResult.Empty;
        }

        var commands = new List<CustomSlashCommandDefinition>();
        var warnings = new List<CustomSlashCommandCatalogWarning>();
        var occupiedTokens = new HashSet<string>(_reservedTokens, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }
        catch (IOException)
        {
            return new([], [new(directory, "could not read slash command directory.")]);
        }
        catch (UnauthorizedAccessException)
        {
            return new([], [new(directory, "could not read slash command directory.")]);
        }

        foreach (var file in files)
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var command = await JsonSerializer.DeserializeAsync<CustomSlashCommandDefinition>(
                    stream, JsonDefaults.ReadOptions, cancellationToken);
                if (!IsValid(command, out var reason))
                {
                    warnings.Add(new(file, reason));
                    continue;
                }

                var validCommand = command!;
                var tokens = GetTokens(validCommand).Select(token => token!).ToList();
                var collision = tokens.FirstOrDefault(occupiedTokens.Contains);
                if (collision is not null)
                {
                    warnings.Add(new(file, $"token '{collision}' collides with an already loaded or reserved token."));
                    continue;
                }

                foreach (var token in tokens) occupiedTokens.Add(token);

                commands.Add(validCommand with
                {
                    Name = validCommand.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(validCommand.Description) ? null : validCommand.Description.Trim(),
                    Aliases = validCommand.Aliases is { Count: > 0 } ? validCommand.Aliases : null
                });
            }
            catch (JsonException)
            {
                warnings.Add(new(file, "malformed JSON."));
            }
            catch (IOException)
            {
                warnings.Add(new(file, "could not be read."));
            }
            catch (UnauthorizedAccessException)
            {
                warnings.Add(new(file, "could not be read."));
            }
        }

        return new(
            commands.OrderBy(command => command.Order)
                .ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(command => command.Id, StringComparer.Ordinal)
                .ToList(),
            warnings);
    }

    private bool IsValid(CustomSlashCommandDefinition? command, out string reason)
    {
        if (command is null)
        {
            reason = "missing required fields.";
            return false;
        }

        if (!IsValidToken(command.Id))
        {
            reason = "invalid command ID.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            reason = "invalid command name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.Template))
        {
            reason = "invalid command template.";
            return false;
        }

        var tokens = GetTokens(command);
        var localTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            if (!IsValidToken(token))
            {
                reason = $"invalid command token '{token}'.";
                return false;
            }

            if (!localTokens.Add(token!))
            {
                reason = $"duplicate command token '{token}'.";
                return false;
            }

            if (_reservedTokens.Contains(token!))
            {
                reason = $"command token '{token}' is reserved.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static IReadOnlyList<string?> GetTokens(CustomSlashCommandDefinition command) =>
        new[] { command.Id }.Concat(command.Aliases ?? []).ToList();

    private static bool IsValidToken(string? token) =>
        !string.IsNullOrEmpty(token)
        && string.Equals(token.Trim(), token, StringComparison.Ordinal)
        && IsAsciiLetterOrDigit(token[0])
        && token.Skip(1).All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9';
}
