using System.Reflection;
using QuickNotesTxt.Services;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class FileWatcherServiceTests
{
    [Fact]
    public async Task Flush_RaisesSingleBatchForMultiplePaths()
    {
        using var service = new FileWatcherService();
        var received = await CaptureBatchAsync(service, watcher =>
        {
            Queue(watcher, new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Created, "/notes/a.txt"));
            Queue(watcher, new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Changed, "/notes/b.txt"));
        });

        Assert.True(received.IsBatch);
        Assert.Equal(2, received.Changes.Count);
        Assert.Equal(NoteFileChangeKind.Created, received.Changes[0].Kind);
        Assert.Equal("/notes/a.txt", received.Changes[0].Path);
        Assert.Equal(NoteFileChangeKind.Changed, received.Changes[1].Kind);
        Assert.Equal("/notes/b.txt", received.Changes[1].Path);
    }

    [Fact]
    public async Task Flush_CoalescesRepeatedChangesForSamePath()
    {
        using var service = new FileWatcherService();
        var received = await CaptureBatchAsync(service, watcher =>
        {
            Queue(watcher, new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Created, "/notes/a.txt"));
            Queue(watcher, new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Changed, "/notes/a.txt"));
        });

        Assert.Single(received.Changes);
        Assert.Equal(NoteFileChangeKind.Created, received.Kind);
        Assert.Equal("/notes/a.txt", received.Path);
    }

    private static async Task<NoteFileChangedEventArgs> CaptureBatchAsync(FileWatcherService service, Action<FileWatcherService> arrange)
    {
        var batchTask = new TaskCompletionSource<NoteFileChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.NoteChanged += (_, e) => batchTask.TrySetResult(e);

        arrange(service);
        Flush(service);

        var completed = await Task.WhenAny(batchTask.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        if (completed != batchTask.Task)
        {
            throw new TimeoutException("Timed out waiting for watcher batch.");
        }

        return await batchTask.Task;
    }

    private static void Queue(FileWatcherService service, NoteFileChangedEventArgs.NoteFileChange change)
    {
        var queueMethod = typeof(FileWatcherService).GetMethod(
            "Queue",
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(NoteFileChangedEventArgs.NoteFileChange)]);

        if (queueMethod is null)
        {
            throw new MissingMethodException(nameof(FileWatcherService), "Queue");
        }

        queueMethod.Invoke(service, [change]);
    }

    private static void Flush(FileWatcherService service)
    {
        var flushMethod = typeof(FileWatcherService).GetMethod("Flush", BindingFlags.Instance | BindingFlags.NonPublic);

        if (flushMethod is null)
        {
            throw new MissingMethodException(nameof(FileWatcherService), "Flush");
        }

        flushMethod.Invoke(service, null);
    }
}
