using Xunit;
using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.Tests;

public sealed class NotesRepositoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));
    private readonly NotesRepository _repository = new();

    [Fact]
    public void CreateDraftNote_UsesTimestampTitle()
    {
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 3, 9, 7, 33, 0));
        var note = _repository.CreateDraftNote(_tempRoot, new DateTimeOffset(2026, 3, 9, 7, 33, 0, localOffset));

        Assert.Equal("2026-03-09-0733", note.Title);
        Assert.EndsWith("2026-03-09-0733.md", note.FilePath);
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
    public async Task SaveAndLoadNoteAsync_PreservesPolishCharacters()
    {
        var draft = _repository.CreateDraftNote(_tempRoot, DateTimeOffset.Now);
        draft.Title = "zażółć gęślą jaźń";
        draft.OriginalTitle = draft.Title;
        draft.Body = "Pchnąć w tę łódź jeża lub ośm skrzyń fig.";
        draft.Tags = ["język", "źródło"];

        var saved = await _repository.SaveNoteAsync(_tempRoot, draft);
        var loaded = await _repository.LoadNoteAsync(saved.FilePath);

        Assert.NotNull(loaded);
        Assert.Equal(draft.Title, loaded.Title);
        Assert.Equal(draft.Body, loaded.Body);
        Assert.Equal(draft.Tags, loaded.Tags);
    }

    [Fact]
    public async Task LoadNoteAsync_UsesFileMetadataFromDisk()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "metadata.txt");
        await File.WriteAllTextAsync(filePath, "body");

        var updatedAt = new DateTime(2026, 3, 3, 12, 13, 0, DateTimeKind.Local);
        File.SetLastWriteTime(filePath, updatedAt);

        var loaded = await _repository.LoadNoteAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(updatedAt, loaded.UpdatedAt);
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
        Assert.EndsWith("renamed.md", renamed.FilePath);
        Assert.False(File.Exists(originalPath));
    }

    [Fact]
    public async Task SaveNoteAsync_MigratesLegacyTxtNoteToMarkdown()
    {
        Directory.CreateDirectory(_tempRoot);
        var legacyPath = Path.Combine(_tempRoot, "legacy.txt");
        await File.WriteAllTextAsync(legacyPath, "legacy body");

        var loaded = await _repository.LoadNoteAsync(legacyPath);
        Assert.NotNull(loaded);

        var saved = await _repository.SaveNoteAsync(_tempRoot, loaded);

        Assert.EndsWith("legacy.md", saved.FilePath);
        Assert.True(File.Exists(saved.FilePath));
        Assert.False(File.Exists(legacyPath));
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
    public async Task LoadSummariesAsync_LoadsMarkdownAndPrefersMarkdownOverLegacyStem()
    {
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "plain.txt"), "legacy body");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "plain.md"), "markdown body");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "fresh.md"), "fresh body");

        var summaries = await _repository.LoadSummariesAsync(_tempRoot);

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, note => note.Title == "plain" && note.Preview == "markdown body");
        Assert.Contains(summaries, note => note.Title == "fresh" && note.Preview == "fresh body");
    }

    [Fact]
    public async Task LoadSummariesAsync_CleansMarkdownNoiseFromPreview()
    {
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "preview.md"), "# Heading\n\n- [x] **done** with `code` and [link](https://example.com)");

        var summaries = await _repository.LoadSummariesAsync(_tempRoot);

        var summary = Assert.Single(summaries);
        Assert.Equal("Heading done with code and link", summary.Preview);
    }

    [Fact]
    public async Task LoadSummariesAsync_PreservesFrontMatterMetadata()
    {
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(
            Path.Combine(_tempRoot, "summary-frontmatter.md"),
            """
            ---
            title: summary title
            tags: ["alpha", "beta"]
            createdAt: 2026-03-09T07:33:00.0000000+00:00
            updatedAt: 2026-03-09T07:34:00.0000000+00:00
            ---
            body line
            """);

        var summaries = await _repository.LoadSummariesAsync(_tempRoot);

        var summary = Assert.Single(summaries);
        Assert.Equal("summary title", summary.Title);
        Assert.Equal(["alpha", "beta"], summary.Tags);
        Assert.Equal("body line", summary.Preview);
    }

    [Fact]
    public void ParseDocument_DoesNotTreatHorizontalRuleAsFrontMatter()
    {
        var parsed = NotesRepository.ParseDocument(Path.Combine(_tempRoot, "rule.md"), "---\nhello\n---\nbody");

        Assert.Equal("rule", parsed.Title);
        Assert.Equal("---\nhello\n---\nbody", parsed.Body);
    }

    [Fact]
    public void BuildEditableDocumentText_AndTryParseEditableDocumentText_RoundTripRawFrontMatter()
    {
        var filePath = Path.Combine(_tempRoot, "editable.md");
        var original = NotesRepository.ParseDocument(filePath, """
            ---
            title: editable
            tags: ["alpha"]
            custom: yes
            createdAt: 2026-03-09T07:33:00.0000000+00:00
            updatedAt: 2026-03-09T07:34:00.0000000+00:00
            ---
            body line
            """);

        var editable = NotesRepository.BuildEditableDocumentText(original);

        Assert.Contains("custom: yes", editable, StringComparison.Ordinal);
        Assert.True(NotesRepository.TryParseEditableDocumentText(original, editable, out var parsed, out var error));
        Assert.Equal(string.Empty, error);
        Assert.Equal("editable", parsed.Title);
        Assert.Equal(["alpha"], parsed.Tags);
        Assert.Equal("body line", parsed.Body);
        Assert.Contains("custom: yes", parsed.FrontMatterText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveNoteAsync_AvoidsOverwritingExistingMarkdownStemWhenMigratingLegacyNote()
    {
        Directory.CreateDirectory(_tempRoot);
        var legacyPath = Path.Combine(_tempRoot, "sample.txt");
        var markdownPath = Path.Combine(_tempRoot, "sample.md");
        await File.WriteAllTextAsync(legacyPath, "legacy body");
        await File.WriteAllTextAsync(markdownPath, "existing markdown body");

        var loaded = await _repository.LoadNoteAsync(legacyPath);
        Assert.NotNull(loaded);

        var saved = await _repository.SaveNoteAsync(_tempRoot, loaded);

        Assert.EndsWith("sample-1.md", saved.FilePath);
        Assert.True(File.Exists(markdownPath));
        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void QueryNotes_UsesPickerStyleMatchingWithTagFilter()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "deploy-checklist.txt"),
                Title = "deploy-checklist",
                Tags = ["prod"],
                CreatedAt = new DateTime(2026, 3, 1),
                UpdatedAt = new DateTime(2026, 3, 4)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "incident-log.txt"),
                Title = "incident-log",
                Tags = ["prod", "deploy"],
                CreatedAt = new DateTime(2026, 3, 3),
                UpdatedAt = new DateTime(2026, 3, 5)
            }
        };

        var queried = _repository.QueryNotes(notes, "deploy prod", ["prod"], false, null, SortOption.Title);

        Assert.Equal(new[] { "deploy-checklist", "incident-log" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotes_MatchesAnySelectedTagByDefault()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "alpha.txt"),
                Title = "alpha",
                Tags = ["ops"]
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "beta.txt"),
                Title = "beta",
                Tags = ["deploy"]
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "gamma.txt"),
                Title = "gamma",
                Tags = ["ops", "deploy"]
            }
        };

        var queried = _repository.QueryNotes(notes, string.Empty, ["ops", "deploy"], false, null, SortOption.Title);

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotes_CanRequireAllSelectedTags()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "alpha.txt"),
                Title = "alpha",
                Tags = ["ops"]
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "beta.txt"),
                Title = "beta",
                Tags = ["deploy"]
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "gamma.txt"),
                Title = "gamma",
                Tags = ["ops", "deploy"]
            }
        };

        var queried = _repository.QueryNotes(notes, string.Empty, ["ops", "deploy"], true, null, SortOption.Title);

        Assert.Equal(new[] { "gamma" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotes_UsesSidebarSortAsSearchTieBreak()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "deploy-zeta.txt"),
                Title = "deploy-zeta",
                CreatedAt = new DateTime(2026, 3, 1),
                UpdatedAt = new DateTime(2026, 3, 2)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "deploy-beta.txt"),
                Title = "deploy-beta",
                CreatedAt = new DateTime(2026, 3, 3),
                UpdatedAt = new DateTime(2026, 3, 4)
            }
        };

        var queried = _repository.QueryNotes(notes, "deploy", [], false, null, SortOption.Title);

        Assert.Equal(new[] { "deploy-beta", "deploy-zeta" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotes_UsesPickerStyleRankingForFuzzyMatches()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "roadmap.txt"),
                Title = "roadmap",
                UpdatedAt = new DateTime(2026, 3, 2)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "project-roadmap.txt"),
                Title = "project-roadmap",
                UpdatedAt = new DateTime(2026, 3, 6)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "weekly-roadmap-notes.txt"),
                Title = "weekly-roadmap-notes",
                UpdatedAt = new DateTime(2026, 3, 7)
            }
        };

        var queried = _repository.QueryNotes(notes, "roadmap", [], false, null, SortOption.LastModified);

        Assert.Equal(new[] { "roadmap", "weekly-roadmap-notes", "project-roadmap" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotes_EmptySearchStillUsesSelectedSort()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "zulu.txt"),
                Title = "Zulu",
                CreatedAt = new DateTime(2026, 3, 1),
                UpdatedAt = new DateTime(2026, 3, 4)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "alpha.txt"),
                Title = "Alpha",
                CreatedAt = new DateTime(2026, 3, 3),
                UpdatedAt = new DateTime(2026, 3, 5)
            }
        };

        var queried = _repository.QueryNotes(notes, string.Empty, [], false, null, SortOption.Title);

        Assert.Equal(new[] { "Alpha", "Zulu" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotes_DoesNotReturnBodyOnlyMatches()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "meeting-notes.txt"),
                Title = "meeting-notes",
                SearchText = "meeting-notes contains roadmap details",
                UpdatedAt = new DateTime(2026, 3, 5)
            }
        };

        var queried = _repository.QueryNotes(notes, "roadmap", [], false, null, SortOption.LastModified);

        Assert.Empty(queried);
    }

    [Fact]
    public void QueryNotes_FiltersByCreatedDateIgnoringTime()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "morning.txt"),
                Title = "morning",
                CreatedAt = new DateTime(2026, 3, 9, 7, 33, 0),
                UpdatedAt = new DateTime(2026, 3, 9, 8, 0, 0)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "evening.txt"),
                Title = "evening",
                CreatedAt = new DateTime(2026, 3, 10, 19, 10, 0),
                UpdatedAt = new DateTime(2026, 3, 10, 19, 20, 0)
            }
        };

        var queried = _repository.QueryNotes(notes, string.Empty, [], false, new DateTime(2026, 3, 9, 23, 59, 0), SortOption.Title);

        var match = Assert.Single(queried);
        Assert.Equal("morning", match.Title);
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

    [Fact]
    public void QueryNotesForPicker_ReturnsRecentNotesWhenSearchIsEmpty()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "older-note.txt"),
                Title = "older-note",
                UpdatedAt = new DateTime(2026, 3, 1)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "newer-note.txt"),
                Title = "newer-note",
                UpdatedAt = new DateTime(2026, 3, 5)
            }
        };

        var queried = _repository.QueryNotesForPicker(notes, string.Empty, 10);

        Assert.Equal(new[] { "newer-note", "older-note" }, queried.Select(note => note.DisplayName).ToArray());
    }

    [Fact]
    public void QueryNotesForPicker_PrioritizesExactAndPrefixMatches()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "roadmap.txt"),
                Title = "roadmap",
                UpdatedAt = new DateTime(2026, 3, 2)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "project-roadmap.txt"),
                Title = "project-roadmap",
                UpdatedAt = new DateTime(2026, 3, 6)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "weekly-roadmap-notes.txt"),
                Title = "weekly-roadmap-notes",
                UpdatedAt = new DateTime(2026, 3, 7)
            }
        };

        var queried = _repository.QueryNotesForPicker(notes, "roadmap", 10);

        Assert.Equal(new[] { "roadmap", "weekly-roadmap-notes", "project-roadmap" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotesForPicker_SortsSubsequenceMatchesByCompactness()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "notes-rust-guide.txt"),
                Title = "notes-rust-guide",
                UpdatedAt = new DateTime(2026, 3, 3)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "notes-archive-random-garden.txt"),
                Title = "notes-archive-random-garden",
                UpdatedAt = new DateTime(2026, 3, 8)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "release-notes.txt"),
                Title = "release-notes",
                UpdatedAt = new DateTime(2026, 3, 4)
            }
        };

        var queried = _repository.QueryNotesForPicker(notes, "nrg", 10);

        Assert.Equal(new[] { "notes-rust-guide", "notes-archive-random-garden" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotesForPicker_KeepsFilenameMatchesAheadOfTagOnlyMatches()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "ops-checklist.txt"),
                Title = "ops-checklist",
                Tags = ["deploy"],
                UpdatedAt = new DateTime(2026, 3, 8)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "deploy-notes.txt"),
                Title = "deploy-notes",
                Tags = ["ops"],
                UpdatedAt = new DateTime(2026, 3, 1)
            }
        };

        var queried = _repository.QueryNotesForPicker(notes, "deploy", 10);

        Assert.Equal(new[] { "deploy-notes", "ops-checklist" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotesForPicker_UsesTagsWhenFilenameDoesNotMatch()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "meeting-notes.txt"),
                Title = "meeting-notes",
                Tags = ["project", "roadmap"],
                UpdatedAt = new DateTime(2026, 3, 5)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "scratchpad.txt"),
                Title = "scratchpad",
                Tags = ["misc"],
                UpdatedAt = new DateTime(2026, 3, 7)
            }
        };

        var queried = _repository.QueryNotesForPicker(notes, "roadmap", 10);

        var note = Assert.Single(queried);
        Assert.Equal("meeting-notes", note.Title);
    }

    [Fact]
    public void QueryNotesForPicker_SupportsMultipleTokensAcrossFilenameAndTags()
    {
        var notes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "deploy-checklist.txt"),
                Title = "deploy-checklist",
                Tags = ["prod"],
                UpdatedAt = new DateTime(2026, 3, 2)
            },
            new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, "incident-log.txt"),
                Title = "incident-log",
                Tags = ["prod", "deploy"],
                UpdatedAt = new DateTime(2026, 3, 6)
            }
        };

        var queried = _repository.QueryNotesForPicker(notes, "deploy prod", 10);

        Assert.Equal(new[] { "deploy-checklist", "incident-log" }, queried.Select(note => note.Title).ToArray());
    }

    [Fact]
    public void QueryNotesForPicker_RespectsRequestedResultLimit()
    {
        var notes = Enumerable.Range(1, 12)
            .Select(index => new NoteSummary
            {
                FilePath = Path.Combine(_tempRoot, $"note-{index:00}.txt"),
                Title = $"note-{index:00}",
                UpdatedAt = new DateTime(2026, 3, 1).AddDays(index)
            })
            .ToArray();

        var queried = _repository.QueryNotesForPicker(notes, "note", 10);

        Assert.Equal(10, queried.Count);
        Assert.Equal("note-12", queried[0].Title);
        Assert.Equal("note-03", queried[^1].Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }
}
