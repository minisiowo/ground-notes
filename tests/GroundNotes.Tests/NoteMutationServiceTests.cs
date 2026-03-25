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

    public void Dispose() => _temp.Dispose();
}
