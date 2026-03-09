using Xunit;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;

namespace QuickNotesTxt.Tests;

public sealed class NotesRepositoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "QuickNotesTxt.Tests", Guid.NewGuid().ToString("N"));
    private readonly NotesRepository _repository = new();

    [Fact]
    public void CreateDraftNote_UsesTimestampTitle()
    {
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 3, 9, 7, 33, 0));
        var note = _repository.CreateDraftNote(_tempRoot, new DateTimeOffset(2026, 3, 9, 7, 33, 0, localOffset));

        Assert.Equal("2026-03-09-0733", note.Title);
        Assert.EndsWith("2026-03-09-0733.txt", note.FilePath);
    }

    [Fact]
    public void SerializeAndParse_RoundTripsMetadata()
    {
        var note = new NoteDocument
        {
            FilePath = Path.Combine(_tempRoot, "sample.txt"),
            Title = "sample",
            OriginalTitle = "sample",
            Body = "hello\nworld",
            Tags = ["alpha", "beta"],
            CreatedAt = new DateTime(2026, 3, 9, 7, 33, 0, DateTimeKind.Local),
            UpdatedAt = new DateTime(2026, 3, 9, 7, 34, 0, DateTimeKind.Local)
        };

        var serialized = NotesRepository.Serialize(note);
        var parsed = NotesRepository.ParseDocument(note.FilePath, serialized);

        Assert.Equal(note.Title, parsed.Title);
        Assert.Equal(note.Tags, parsed.Tags);
        Assert.Equal(note.Body, parsed.Body);
    }

    [Fact]
    public async Task SaveNoteAsync_RenamesWhenTitleChanges()
    {
        var draft = _repository.CreateDraftNote(_tempRoot, DateTimeOffset.Now);
        draft.Body = "body";

        var saved = await _repository.SaveNoteAsync(_tempRoot, draft);
        var originalPath = saved.FilePath;
        saved.Title = "renamed";

        var renamed = await _repository.SaveNoteAsync(_tempRoot, saved);

        Assert.True(File.Exists(renamed.FilePath));
        Assert.EndsWith("renamed.txt", renamed.FilePath);
        Assert.False(File.Exists(originalPath));
    }

    [Fact]
    public async Task LoadSummariesAsync_LoadsPlainTextFilesWithoutFrontMatter()
    {
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "plain.txt"), "plain body");

        var summaries = await _repository.LoadSummariesAsync(_tempRoot);

        Assert.Single(summaries);
        Assert.Equal("plain", summaries[0].Title);
        Assert.Equal("plain body", summaries[0].Preview);
    }

    [Fact]
    public void QueryNotes_FiltersBySearchAndTagAndSorts()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                Title = "Zulu",
                Preview = "plain",
                SearchText = "Zulu plain misc",
                Tags = ["misc"],
                CreatedAt = new DateTime(2026, 3, 1),
                UpdatedAt = new DateTime(2026, 3, 4)
            },
            new NoteSummary
            {
                Title = "Alpha",
                Preview = "contains project details",
                SearchText = "Alpha contains project details work",
                Tags = ["work"],
                CreatedAt = new DateTime(2026, 3, 3),
                UpdatedAt = new DateTime(2026, 3, 5)
            }
        };

        var queried = _repository.QueryNotes(notes, "project", "work", SortOption.Title);

        var note = Assert.Single(queried);
        Assert.Equal("Alpha", note.Title);
    }

    [Fact]
    public async Task DeleteNoteIfExistsAsync_RemovesFile()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "sample.txt");
        await File.WriteAllTextAsync(filePath, "hello");

        await _repository.DeleteNoteIfExistsAsync(filePath);

        Assert.False(File.Exists(filePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }
}


