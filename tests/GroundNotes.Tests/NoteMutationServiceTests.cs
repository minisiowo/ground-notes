using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Tests.Helpers;
using Xunit;

namespace GroundNotes.Tests;

public sealed class NoteMutationServiceTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private readonly NotesRepository _repository = new();

    [Fact]
    public async Task SaveAsync_RaisesNoteMutatedEvent()
    {
        Directory.CreateDirectory(_temp.Root);
        var service = new NoteMutationService(_repository);
        NoteMutationEventArgs? captured = null;
        service.NoteMutated += (_, e) => captured = e;

        var doc = _repository.CreateDraftNote(_temp.Root, DateTimeOffset.Now);
        await service.SaveAsync(_temp.Root, doc);

        Assert.NotNull(captured);
        Assert.Equal(NoteMutationKind.Saved, captured.Kind);
        Assert.NotNull(captured.Document);
        Assert.Equal(_temp.Root, captured.FolderPath);
    }

    [Fact]
    public async Task DeleteIfExistsAsync_RaisesNoteMutatedEvent()
    {
        Directory.CreateDirectory(_temp.Root);
        var service = new NoteMutationService(_repository);
        var doc = _repository.CreateDraftNote(_temp.Root, DateTimeOffset.Now);
        var saved = await _repository.SaveNoteAsync(_temp.Root, doc);

        NoteMutationEventArgs? captured = null;
        service.NoteMutated += (_, e) => captured = e;
        await service.DeleteIfExistsAsync(saved.FilePath);

        Assert.NotNull(captured);
        Assert.Equal(NoteMutationKind.Deleted, captured.Kind);
        Assert.Equal(_temp.Root, captured.FolderPath);
    }

    [Fact]
    public async Task BeginMutationScope_SetsOriginId()
    {
        Directory.CreateDirectory(_temp.Root);
        var service = new NoteMutationService(_repository);
        var originId = Guid.NewGuid();
        NoteMutationEventArgs? captured = null;
        service.NoteMutated += (_, e) => captured = e;

        using (NoteMutationService.BeginMutationScope(originId))
        {
            await service.SaveAsync(_temp.Root, _repository.CreateDraftNote(_temp.Root, DateTimeOffset.Now));
        }

        Assert.NotNull(captured);
        Assert.Equal(originId, captured.OriginId);
    }

    [Fact]
    public async Task BeginMutationScope_RestoresAfterDispose()
    {
        Directory.CreateDirectory(_temp.Root);
        var service = new NoteMutationService(_repository);
        NoteMutationEventArgs? captured = null;
        service.NoteMutated += (_, e) => captured = e;

        using (NoteMutationService.BeginMutationScope(Guid.NewGuid()))
        {
        }

        await service.SaveAsync(_temp.Root, _repository.CreateDraftNote(_temp.Root, DateTimeOffset.Now));

        Assert.NotNull(captured);
        Assert.Null(captured.OriginId);
    }

    [Fact]
    public async Task SaveAsync_RejectsDocumentLoadedBeforeAnotherSave()
    {
        Directory.CreateDirectory(_temp.Root);
        var service = new NoteMutationService(_repository);
        var original = _repository.CreateDraftNote(_temp.Root, DateTimeOffset.Now);
        original.Body = "original";
        var persisted = await _repository.SaveNoteAsync(_temp.Root, original);
        var first = await _repository.LoadNoteAsync(persisted.FilePath);
        var stale = await _repository.LoadNoteAsync(persisted.FilePath);
        Assert.NotNull(first);
        Assert.NotNull(stale);
        first.Body = "first save";
        stale.Body = "stale save";
        await Task.Delay(20);

        await service.SaveAsync(_temp.Root, first, preserveTimestamp: true);

        await Assert.ThrowsAsync<NoteSaveConflictException>(() => service.SaveAsync(_temp.Root, stale));
        var reloaded = await _repository.LoadNoteAsync(persisted.FilePath);
        Assert.Equal("first save", reloaded?.Body);
    }

    [Fact]
    public async Task SaveAsync_ForwardsPreserveTimestampToRepository()
    {
        Directory.CreateDirectory(_temp.Root);
        var service = new NoteMutationService(_repository);
        var doc = _repository.CreateDraftNote(_temp.Root, DateTimeOffset.Now);
        doc.Body = "body";
        var originalUpdatedAt = doc.UpdatedAt;

        await Task.Delay(50);
        var saved = await service.SaveAsync(_temp.Root, doc, preserveTimestamp: true);

        Assert.Equal(originalUpdatedAt, saved.UpdatedAt);
    }

    public void Dispose() => _temp.Dispose();
}
