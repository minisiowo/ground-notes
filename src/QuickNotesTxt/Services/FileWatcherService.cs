namespace QuickNotesTxt.Services;

public sealed class FileWatcherService : IFileWatcherService
{
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(125);

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _pendingLock = new();
    private readonly Dictionary<string, NoteFileChangedEventArgs.NoteFileChange> _pendingEvents = [];
    private readonly List<string> _pendingOrder = [];
    private Timer? _flushTimer;

    public event EventHandler<NoteFileChangedEventArgs>? NoteChanged;

    public void Watch(string folderPath)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        foreach (var filter in new[] { "*.txt", "*.md" })
        {
            var watcher = new FileSystemWatcher(folderPath, filter)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
            };

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnCreated;
            watcher.Changed -= OnChanged;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
        }

        _watchers.Clear();

        lock (_pendingLock)
        {
            _pendingEvents.Clear();
            _flushTimer?.Dispose();
            _flushTimer = null;
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        Queue(new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Created, e.FullPath));
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        Queue(new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Changed, e.FullPath));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        Queue(new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Deleted, e.FullPath));
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Queue(new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Renamed, e.FullPath, e.OldFullPath));
    }

    private void Queue(NoteFileChangedEventArgs.NoteFileChange change)
    {
        lock (_pendingLock)
        {
            var key = BuildKey(change);

            if (_pendingEvents.TryGetValue(key, out var existing))
            {
                _pendingEvents[key] = Merge(existing, change);
            }
            else
            {
                _pendingEvents[key] = change;
                _pendingOrder.Add(key);
            }

            if (_flushTimer is null)
            {
                _flushTimer = new Timer(_ => Flush(), null, CoalesceWindow, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _flushTimer.Change(CoalesceWindow, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Flush()
    {
        List<NoteFileChangedEventArgs.NoteFileChange> pending;

        lock (_pendingLock)
        {
            pending = _pendingOrder.Select(key => _pendingEvents[key]).ToList();
            _pendingEvents.Clear();
            _pendingOrder.Clear();
            _flushTimer?.Dispose();
            _flushTimer = null;
        }

        if (pending.Count == 0)
        {
            return;
        }

        NoteChanged?.Invoke(this, new NoteFileChangedEventArgs(pending));
    }

    private static string BuildKey(NoteFileChangedEventArgs.NoteFileChange change)
    {
        return change.Kind == NoteFileChangeKind.Renamed
            ? $"{change.OldPath}->{change.Path}"
            : change.Path;
    }

    private static NoteFileChangedEventArgs.NoteFileChange Merge(NoteFileChangedEventArgs.NoteFileChange existing, NoteFileChangedEventArgs.NoteFileChange incoming)
    {
        if (incoming.Kind == NoteFileChangeKind.Renamed || existing.Kind == NoteFileChangeKind.Renamed)
        {
            return incoming.Kind == NoteFileChangeKind.Renamed ? incoming : existing;
        }

        if (incoming.Kind == NoteFileChangeKind.Deleted || existing.Kind == NoteFileChangeKind.Deleted)
        {
            return new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Deleted, incoming.Path);
        }

        if (incoming.Kind == NoteFileChangeKind.Created || existing.Kind == NoteFileChangeKind.Created)
        {
            return new NoteFileChangedEventArgs.NoteFileChange(NoteFileChangeKind.Created, incoming.Path);
        }

        return incoming;
    }
}
