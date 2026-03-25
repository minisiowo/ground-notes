using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class NoteSearchServiceTests
{
    [Fact]
    public void Search_ReturnsMatchingNotes()
    {
        var repository = new NotesRepository();
        var notes = new List<NoteSummary>
        {
            CreateSummary("meeting-notes.md", "Meeting Notes"),
            CreateSummary("shopping-list.md", "Shopping List"),
            CreateSummary("meeting-agenda.md", "Meeting Agenda"),
        };

        var service = new NoteSearchService(repository, () => notes);

        var results = service.Search("meeting");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("meeting", r.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_RespectsMaxResults()
    {
        var repository = new NotesRepository();
        var notes = Enumerable.Range(1, 20)
            .Select(i => CreateSummary($"note-{i}.md", $"Note {i}"))
            .ToList();

        var service = new NoteSearchService(repository, () => notes);

        var results = service.Search("Note", maxResults: 5);

        Assert.True(results.Count <= 5);
    }

    [Fact]
    public void Search_ReturnsEmptyForNoMatch()
    {
        var repository = new NotesRepository();
        var notes = new List<NoteSummary>
        {
            CreateSummary("hello.md", "Hello World"),
        };

        var service = new NoteSearchService(repository, () => notes);

        var results = service.Search("zzz_no_match");

        Assert.Empty(results);
    }

    private static NoteSummary CreateSummary(string fileName, string title)
    {
        return NoteSummary.FromContent(fileName, $"/tmp/{fileName}", title, [], DateTime.UtcNow, DateTime.UtcNow, "body");
    }
}
