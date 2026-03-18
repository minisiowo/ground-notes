namespace QuickNotesTxt.Services;

public sealed class FileWatcherService : IFileWatcherService
{
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(125);

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _pendingLock = new();
    private readonly Dictionary<string, NoteFileChangedEventArgs> _pendingEvents = [];
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
        Queue(new NoteFileChangedEventArgs(NoteFileChangeKind.Created, e.FullPath));
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        Queue(new NoteFileChangedEventArgs(NoteFileChangeKind.Changed, e.FullPath));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        Queue(new NoteFileChangedEventArgs(NoteFileChangeKind.Deleted, e.FullPath));
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Queue(new NoteFileChangedEventArgs(NoteFileChangeKind.Renamed, e.FullPath, e.OldFullPath));
    }

    private void Queue(NoteFileChangedEventArgs change)
    {
        lock (_pendingLock)
        {
            _pendingEvents[BuildKey(change)] = Merge(_pendingEvents.GetValueOrDefault(BuildKey(change)), change);

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
        List<NoteFileChangedEventArgs> pending;

        lock (_pendingLock)
        {
            pending = _pendingEvents.Values.ToList();
            _pendingEvents.Clear();
            _flushTimer?.Dispose();
            _flushTimer = null;
        }

        foreach (var change in pending)
        {
            NoteChanged?.Invoke(this, change);
        }
    }

    private static string BuildKey(NoteFileChangedEventArgs change)
    {
        return change.Kind == NoteFileChangeKind.Renamed
            ? $"{change.OldPath}->{change.Path}"
            : change.Path;
    }

    private static NoteFileChangedEventArgs Merge(NoteFileChangedEventArgs? existing, NoteFileChangedEventArgs incoming)
    {
        if (existing is null)
        {
            return incoming;
        }

        if (incoming.Kind == NoteFileChangeKind.Renamed || existing.Kind == NoteFileChangeKind.Renamed)
        {
            return incoming.Kind == NoteFileChangeKind.Renamed ? incoming : existing;
        }

        if (incoming.Kind == NoteFileChangeKind.Deleted || existing.Kind == NoteFileChangeKind.Deleted)
        {
            return new NoteFileChangedEventArgs(NoteFileChangeKind.Deleted, incoming.Path);
        }

        if (incoming.Kind == NoteFileChangeKind.Created || existing.Kind == NoteFileChangeKind.Created)
        {
            return new NoteFileChangedEventArgs(NoteFileChangeKind.Created, incoming.Path);
        }

        return incoming;
    }
}
