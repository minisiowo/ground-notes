using System.Text;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class NotesRepository : INotesRepository
{
    public async Task<IReadOnlyList<NoteSummary>> LoadSummariesAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var notes = new List<NoteSummary>();
        foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document = await LoadNoteAsync(filePath, cancellationToken);
            if (document is not null)
            {
                notes.Add(ToSummary(document));
            }
        }

        return notes;
    }

    public async Task<NoteDocument?> LoadNoteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ParseDocument(filePath, content);
    }

    public NoteDocument CreateDraftNote(string folderPath, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(folderPath);
        var localTimestamp = timestamp.ToLocalTime();
        var baseName = localTimestamp.ToString("yyyy-MM-dd-HHmm");
        var filePath = GetUniqueFilePath(folderPath, baseName);

        return new NoteDocument
        {
            Id = filePath,
            FilePath = filePath,
            Title = baseName,
            OriginalTitle = baseName,
            Body = string.Empty,
            Tags = [],
            CreatedAt = localTimestamp.DateTime,
            UpdatedAt = localTimestamp.DateTime,
            IsAutoCreated = true
        };
    }

    public async Task<NoteDocument> SaveNoteAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(folderPath);
        var persisted = Clone(document);
        persisted.Title = SanitizeTitle(document.Title);
        persisted.UpdatedAt = DateTime.Now;

        var targetPath = persisted.FilePath;
        var currentFolder = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var currentName = Path.GetFileNameWithoutExtension(targetPath);

        if (!string.Equals(currentFolder, folderPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(currentName, persisted.Title, StringComparison.Ordinal))
        {
            targetPath = GetUniqueFilePath(folderPath, persisted.Title, persisted.FilePath);
        }

        if (!string.Equals(document.FilePath, targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(document.FilePath))
        {
            File.Move(document.FilePath, targetPath);
        }

        persisted.FilePath = targetPath;
        persisted.Id = targetPath;
        persisted.OriginalTitle = persisted.Title;

        var serialized = Serialize(persisted);
        await File.WriteAllTextAsync(targetPath, serialized, Encoding.UTF8, cancellationToken);
        return persisted;
    }

    public async Task<NoteDocument> RenameNoteAsync(string folderPath, NoteDocument document, string newTitle, CancellationToken cancellationToken = default)
    {
        var renamed = Clone(document);
        renamed.Title = newTitle;
        return await SaveNoteAsync(folderPath, renamed, cancellationToken);
    }

    public Task DeleteNoteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<NoteSummary> QueryNotes(IEnumerable<NoteSummary> notes, string searchText, string? selectedTag, SortOption sortOption)
    {
        var query = notes;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(note =>
                note.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                note.SearchText.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                note.Tags.Any(tag => tag.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(selectedTag))
        {
            query = query.Where(note => note.Tags.Contains(selectedTag, StringComparer.OrdinalIgnoreCase));
        }

        query = sortOption switch
        {
            SortOption.Title => query.OrderBy(note => note.Title, StringComparer.OrdinalIgnoreCase),
            SortOption.CreatedAt => query.OrderByDescending(note => note.CreatedAt),
            _ => query.OrderByDescending(note => note.UpdatedAt)
        };

        return query.ToList();
    }

    public static string Serialize(NoteDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"title: {EscapeValue(document.Title)}");
        builder.AppendLine($"tags: [{string.Join(", ", document.Tags.Select(EscapeTag))}]");
        builder.AppendLine($"createdAt: {document.CreatedAt:O}");
        builder.AppendLine($"updatedAt: {document.UpdatedAt:O}");
        builder.AppendLine("---");
        if (!string.IsNullOrEmpty(document.Body))
        {
            builder.Append(document.Body);
        }

        return builder.ToString();
    }

    public static NoteDocument ParseDocument(string filePath, string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        using var reader = new StringReader(normalized);
        var firstLine = reader.ReadLine();
        var title = Path.GetFileNameWithoutExtension(filePath);
        var tags = new List<string>();
        var createdAt = File.Exists(filePath) ? File.GetCreationTime(filePath) : DateTime.Now;
        var updatedAt = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : createdAt;
        string body;

        if (firstLine == "---")
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line == "---")
                {
                    break;
                }

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                switch (key)
                {
                    case "title":
                        title = UnescapeValue(value);
                        break;
                    case "tags":
                        tags = ParseTags(value);
                        break;
                    case "createdAt" when DateTime.TryParse(value, out var parsedCreatedAt):
                        createdAt = parsedCreatedAt;
                        break;
                    case "updatedAt" when DateTime.TryParse(value, out var parsedUpdatedAt):
                        updatedAt = parsedUpdatedAt;
                        break;
                }
            }

            body = reader.ReadToEnd();
        }
        else
        {
            body = content;
        }

        return new NoteDocument
        {
            Id = filePath,
            FilePath = filePath,
            Title = title,
            OriginalTitle = title,
            Body = body,
            Tags = tags,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            IsAutoCreated = false
        };
    }

    private static NoteSummary ToSummary(NoteDocument document)
    {
        var preview = document.Body.ReplaceLineEndings(" ").Trim();
        if (preview.Length > 96)
        {
            preview = preview[..96] + "...";
        }

        return new NoteSummary
        {
            Id = document.Id,
            FilePath = document.FilePath,
            Title = document.Title,
            Tags = [.. document.Tags],
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            Preview = preview,
            SearchText = string.Join(' ', new[] { document.Title, document.Body, string.Join(' ', document.Tags) })
        };
    }

    private static NoteDocument Clone(NoteDocument document)
    {
        return new NoteDocument
        {
            Id = document.Id,
            FilePath = document.FilePath,
            Title = document.Title,
            OriginalTitle = document.OriginalTitle,
            Body = document.Body,
            Tags = [.. document.Tags],
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            IsAutoCreated = document.IsAutoCreated
        };
    }

    private static string SanitizeTitle(string? title)
    {
        var candidate = string.IsNullOrWhiteSpace(title) ? DateTime.Now.ToString("yyyy-MM-dd-HHmm") : title.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(candidate.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? DateTime.Now.ToString("yyyy-MM-dd-HHmm") : cleaned;
    }

    private static string GetUniqueFilePath(string folderPath, string title, string? currentFilePath = null)
    {
        var candidatePath = Path.Combine(folderPath, $"{title}.txt");
        var counter = 1;

        while (File.Exists(candidatePath) && !string.Equals(candidatePath, currentFilePath, StringComparison.OrdinalIgnoreCase))
        {
            candidatePath = Path.Combine(folderPath, $"{title}-{counter}.txt");
            counter++;
        }

        return candidatePath;
    }

    private static string EscapeValue(string value)
    {
        return value.Replace("\r", string.Empty).Replace("\n", " ");
    }

    private static string EscapeTag(string tag)
    {
        return $"\"{tag.Replace("\"", "\\\"")}\"";
    }

    private static string UnescapeValue(string value)
    {
        return value.Trim().Trim('"');
    }

    private static List<string> ParseTags(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            return [];
        }

        trimmed = trimmed[1..^1];
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        return trimmed
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim().Trim('"'))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
