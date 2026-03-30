using System.Collections.Concurrent;
using System.Text;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class NotesRepository : INotesRepository
{
    private const string MarkdownExtension = ".md";
    private const int SummaryLoadMaxParallelism = 8;
    private static readonly string[] s_supportedExtensions = [MarkdownExtension, ".txt"];

    public async Task<IReadOnlyList<NoteSummary>> LoadSummariesAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var pathList = EnumeratePreferredNoteFiles(folderPath).ToArray();
        if (pathList.Length == 0)
        {
            return [];
        }

        var ordered = new ConcurrentBag<(int Index, NoteSummary Summary)>();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, pathList.Length),
            new ParallelOptions { MaxDegreeOfParallelism = SummaryLoadMaxParallelism, CancellationToken = cancellationToken },
            async (i, ct) =>
            {
                var filePath = pathList[i];
                if (!File.Exists(filePath))
                {
                    return;
                }

                var metadata = GetFileMetadata(filePath);
                var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct).ConfigureAwait(false);
                var note = ParseSummary(filePath, content, metadata.CreatedAt, metadata.UpdatedAt);
                ordered.Add((i, note));
            }).ConfigureAwait(false);

        return ordered
            .OrderBy(entry => entry.Index)
            .Select(entry => entry.Summary)
            .ToList();
    }

    public async Task<NoteDocument?> LoadNoteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var metadata = GetFileMetadata(filePath);
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ParseDocument(filePath, content, metadata.CreatedAt, metadata.UpdatedAt);
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

        var targetPath = GetUniqueFilePath(folderPath, persisted.Title, persisted.FilePath);

        if (!string.Equals(document.FilePath, targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(document.FilePath))
        {
            File.Move(document.FilePath, targetPath);
        }

        persisted.FilePath = targetPath;
        persisted.Id = targetPath;
        persisted.OriginalTitle = persisted.Title;
        persisted.FrontMatterText = MergeStructuredFieldsIntoFrontMatter(persisted.FrontMatterText, persisted);

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

    public IReadOnlyList<NoteSummary> QueryNotes(IEnumerable<NoteSummary> notes, string searchText, IReadOnlyList<string> selectedTags, bool matchAllSelectedTags, DateTime? selectedDate, SortOption sortOption)
    {
        var query = notes;

        if (selectedTags.Count > 0)
        {
            query = matchAllSelectedTags
                ? query.Where(note => selectedTags.All(selectedTag => note.Tags.Contains(selectedTag, StringComparer.OrdinalIgnoreCase)))
                : query.Where(note => selectedTags.Any(selectedTag => note.Tags.Contains(selectedTag, StringComparer.OrdinalIgnoreCase)));
        }

        if (selectedDate is not null)
        {
            var targetDate = selectedDate.Value.Date;
            query = query.Where(note => note.CreatedAt.Date == targetDate);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var queryTokens = searchText.Trim()
                .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var scoredNotes = query
                .Select(note => new ScoredNote(note, ScorePickerMatch(note, queryTokens)))
                .Where(result => result.Score is not null)
                .OrderByDescending(result => result.Score);

            return ApplySidebarSort(scoredNotes, sortOption)
                .Select(result => result.Note)
                .ToList();
        }

        return OrderBySidebarSort(query, sortOption).ToList();
    }

    public IReadOnlyList<NoteSummary> QueryNotesForPicker(IEnumerable<NoteSummary> notes, string searchText, int maxResults)
    {
        IEnumerable<NoteSummary> query;
        var normalizedSearchText = searchText.Trim();
        var queryTokens = normalizedSearchText
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            query = notes
                .OrderByDescending(note => note.UpdatedAt)
                .ThenBy(note => GetPickerCandidate(note), StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            query = notes
                .Select(note => new
                {
                    Note = note,
                    Score = ScorePickerMatch(note, queryTokens)
                })
                .Where(result => result.Score is not null)
                .OrderByDescending(result => result.Score)
                .ThenByDescending(result => result.Note.UpdatedAt)
                .ThenBy(result => GetPickerCandidate(result.Note), StringComparer.OrdinalIgnoreCase)
                .Select(result => result.Note);
        }

        if (maxResults > 0)
        {
            query = query.Take(maxResults);
        }

        return query.ToList();
    }

    public static string Serialize(NoteDocument document)
    {
        return BuildEditableDocumentText(document);
    }

    public static NoteDocument ParseDocument(string filePath, string content)
    {
        var metadata = GetFileMetadata(filePath);
        return ParseDocument(filePath, content, metadata.CreatedAt, metadata.UpdatedAt);
    }

    private static NoteDocument ParseDocument(string filePath, string content, DateTime createdAt, DateTime updatedAt)
    {
        var parsed = ParseNoteContent(filePath, content, createdAt, updatedAt);

        return new NoteDocument
        {
            Id = filePath,
            FilePath = filePath,
            Title = parsed.Title,
            OriginalTitle = parsed.Title,
            Body = parsed.Body,
            FrontMatterText = parsed.FrontMatterText,
            Tags = parsed.Tags,
            CreatedAt = parsed.CreatedAt,
            UpdatedAt = parsed.UpdatedAt,
            IsAutoCreated = false
        };
    }

    private static NoteSummary ParseSummary(string filePath, string content, DateTime createdAt, DateTime updatedAt)
    {
        var parsed = ParseNoteContent(filePath, content, createdAt, updatedAt);
        return NoteSummary.FromContent(filePath, filePath, parsed.Title, parsed.Tags, parsed.CreatedAt, parsed.UpdatedAt, parsed.Body);
    }

    private static ParsedNoteContent ParseNoteContent(string filePath, string content, DateTime createdAt, DateTime updatedAt)
    {
        var normalized = content.Replace("\r\n", "\n");
        var title = Path.GetFileNameWithoutExtension(filePath);
        var tags = new List<string>();
        var body = normalized;
        string? frontMatterText = null;

        if (TryReadFrontMatter(normalized, ref title, ref tags, ref createdAt, ref updatedAt, out var parsedBody, out frontMatterText))
        {
            body = parsedBody;
        }

        return new ParsedNoteContent(title, tags, body, createdAt, updatedAt, frontMatterText);
    }

    public static string BuildEditableDocumentText(NoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine(MergeStructuredFieldsIntoFrontMatter(document.FrontMatterText, document));
        builder.AppendLine("---");

        if (!string.IsNullOrEmpty(document.Body))
        {
            builder.Append(document.Body);
        }

        return builder.ToString();
    }

    public static bool TryParseEditableDocumentText(NoteDocument baseDocument, string editorText, out NoteDocument parsedDocument, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(baseDocument);

        var normalized = editorText.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal) && normalized != "---")
        {
            parsedDocument = baseDocument;
            errorMessage = "Invalid YAML frontmatter. The document must start with ---.";
            return false;
        }

        var title = baseDocument.Title;
        var tags = new List<string>(baseDocument.Tags);
        var createdAt = baseDocument.CreatedAt;
        var updatedAt = baseDocument.UpdatedAt;

        if (!TryReadFrontMatter(normalized, ref title, ref tags, ref createdAt, ref updatedAt, out var body, out var frontMatterText))
        {
            parsedDocument = baseDocument;
            errorMessage = "Invalid YAML frontmatter. Fix it before leaving YAML mode or saving.";
            return false;
        }

        parsedDocument = baseDocument with
        {
            Title = string.IsNullOrWhiteSpace(title) ? baseDocument.Title : title,
            OriginalTitle = baseDocument.OriginalTitle,
            Body = body,
            Tags = tags,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            FrontMatterText = frontMatterText
        };

        errorMessage = string.Empty;
        return true;
    }

    public static string MergeStructuredFieldsIntoFrontMatter(string? frontMatterText, NoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var lines = string.IsNullOrWhiteSpace(frontMatterText)
            ? new List<string>()
            : frontMatterText.Replace("\r\n", "\n").Split('\n').ToList();

        UpsertFrontMatterLine(lines, "title", EscapeValue(document.Title));
        UpsertFrontMatterLine(lines, "tags", $"[{string.Join(", ", document.Tags.Select(EscapeTag))}]");
        UpsertFrontMatterLine(lines, "createdAt", document.CreatedAt.ToString("O"));
        UpsertFrontMatterLine(lines, "updatedAt", document.UpdatedAt.ToString("O"));

        return string.Join('\n', lines);
    }

    private static NoteDocument Clone(NoteDocument document)
    {
        return document with { Tags = [.. document.Tags] };
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
        var candidatePath = Path.Combine(folderPath, $"{title}{MarkdownExtension}");
        var counter = 1;

        while (HasConflictingPath(candidatePath, currentFilePath))
        {
            candidatePath = Path.Combine(folderPath, $"{title}-{counter}{MarkdownExtension}");
            counter++;
        }

        return candidatePath;
    }

    private static IEnumerable<string> EnumeratePreferredNoteFiles(string folderPath)
    {
        return Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedNotePath)
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(path => string.Equals(Path.GetExtension(path), MarkdownExtension, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .First());
    }

    private static FileMetadata GetFileMetadata(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var now = DateTime.Now;
            return new FileMetadata(now, now);
        }

        return new FileMetadata(File.GetCreationTime(filePath), File.GetLastWriteTime(filePath));
    }

    private static bool IsSupportedNotePath(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return s_supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasConflictingPath(string candidatePath, string? currentFilePath)
    {
        var folderPath = Path.GetDirectoryName(candidatePath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(candidatePath);

        foreach (var extension in s_supportedExtensions)
        {
            var path = Path.Combine(folderPath, stem + extension);
            if (!File.Exists(path))
            {
                continue;
            }

            if (string.Equals(path, currentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryReadFrontMatter(
        string normalizedContent,
        ref string title,
        ref List<string> tags,
        ref DateTime createdAt,
        ref DateTime updatedAt,
        out string body,
        out string? frontMatterText)
    {
        body = normalizedContent;
        frontMatterText = null;
        var lines = normalizedContent.Split('\n');
        if (lines.Length < 3 || lines[0] != "---")
        {
            return false;
        }

        var closingIndex = Array.IndexOf(lines, "---", 1);
        if (closingIndex <= 1)
        {
            return false;
        }

        for (var i = 1; i < closingIndex; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            if (lines[i].IndexOf(':') <= 0)
            {
                return false;
            }
        }

        for (var i = 1; i < closingIndex; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
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

        frontMatterText = string.Join('\n', lines.Skip(1).Take(closingIndex - 1));
        body = string.Join('\n', lines.Skip(closingIndex + 1));
        return true;
    }

    private static void UpsertFrontMatterLine(List<string> lines, string key, string value)
    {
        var replacement = $"{key}: {value}";

        for (var i = 0; i < lines.Count; i++)
        {
            if (TryReadFrontMatterKey(lines[i], out var existingKey) && string.Equals(existingKey, key, StringComparison.Ordinal))
            {
                lines[i] = replacement;
                return;
            }
        }

        lines.Add(replacement);
    }

    private static bool TryReadFrontMatterKey(string line, out string key)
    {
        key = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var separatorIndex = line.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        key = line[..separatorIndex].Trim();
        return key.Length > 0;
    }

    private static int? ScorePickerMatch(NoteSummary note, IReadOnlyList<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return null;
        }

        var filename = GetPickerCandidate(note);
        if (string.IsNullOrWhiteSpace(filename))
        {
            return null;
        }

        var filenameScore = 0;
        var tagScore = 0;

        foreach (var token in queryTokens)
        {
            var filenameTokenScore = ScorePickerTextMatch(filename, token);
            if (filenameTokenScore is not null)
            {
                filenameScore += filenameTokenScore.Value;
                continue;
            }

            var bestTagScore = note.Tags
                .Select(tag => ScorePickerTextMatch(tag, token))
                .Where(score => score is not null)
                .Select(score => score!.Value)
                .DefaultIfEmpty(int.MinValue)
                .Max();

            if (bestTagScore == int.MinValue)
            {
                return null;
            }

            tagScore += 1_500 + (bestTagScore / 4);
        }

        return (filenameScore * 10) + tagScore;
    }

    private static int? ScorePickerTextMatch(string candidate, string searchText)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        var candidateText = candidate.ToLowerInvariant();
        var queryText = searchText.ToLowerInvariant();

        if (candidateText == queryText)
        {
            return 10_000 - candidateText.Length;
        }

        var substringIndex = candidateText.IndexOf(queryText, StringComparison.Ordinal);
        if (substringIndex == 0)
        {
            return 8_000 - (candidateText.Length * 2);
        }

        if (substringIndex > 0)
        {
            return 7_000 - (substringIndex * 40) - candidateText.Length;
        }

        var score = 1_000 - candidateText.Length;
        var previousMatchIndex = -1;

        foreach (var queryCharacter in queryText)
        {
            var matchIndex = candidateText.IndexOf(queryCharacter, previousMatchIndex + 1);
            if (matchIndex < 0)
            {
                return null;
            }

            score += 100;

            if (matchIndex == 0)
            {
                score += 120;
            }
            else if (IsWordBoundary(candidate[matchIndex - 1]))
            {
                score += 60;
            }

            if (previousMatchIndex >= 0)
            {
                var gap = matchIndex - previousMatchIndex - 1;
                if (gap == 0)
                {
                    score += 90;
                }
                else
                {
                    score -= gap * 8;
                }
            }

            previousMatchIndex = matchIndex;
        }

        return score;
    }

    private static IOrderedEnumerable<NoteSummary> OrderBySidebarSort(IEnumerable<NoteSummary> notes, SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.Title => notes
                .OrderBy(note => GetPickerCandidate(note), StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(note => note.UpdatedAt),
            SortOption.CreatedAt => notes
                .OrderByDescending(note => note.CreatedAt)
                .ThenBy(note => GetPickerCandidate(note), StringComparer.OrdinalIgnoreCase),
            _ => notes
                .OrderByDescending(note => note.UpdatedAt)
                .ThenBy(note => GetPickerCandidate(note), StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IOrderedEnumerable<ScoredNote> ApplySidebarSort(IOrderedEnumerable<ScoredNote> query, SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.Title => query
                .ThenBy(result => GetPickerCandidate(result.Note), StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(result => result.Note.UpdatedAt),
            SortOption.CreatedAt => query
                .ThenByDescending(result => result.Note.CreatedAt)
                .ThenBy(result => GetPickerCandidate(result.Note), StringComparer.OrdinalIgnoreCase),
            _ => query
                .ThenByDescending(result => result.Note.UpdatedAt)
                .ThenBy(result => GetPickerCandidate(result.Note), StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed record ScoredNote(NoteSummary Note, int? Score);

    private static string GetPickerCandidate(NoteSummary note)
    {
        var displayName = Path.GetFileNameWithoutExtension(note.FilePath);
        return string.IsNullOrWhiteSpace(displayName) ? note.Title : displayName;
    }

    private static bool IsWordBoundary(char character)
    {
        return character is ' ' or '-' or '_' or '/' or '\\' or '.';
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

    private readonly record struct FileMetadata(DateTime CreatedAt, DateTime UpdatedAt);

    private readonly record struct ParsedNoteContent(
        string Title,
        List<string> Tags,
        string Body,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string? FrontMatterText);
}
