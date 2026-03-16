namespace QuickNotesTxt.Services;

public sealed class FileWatcherService : IFileWatcherService
{
    private readonly List<FileSystemWatcher> _watchers = [];

    public event EventHandler? NotesChanged;

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

            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    public void Stop()
    {
        if (_watchers.Count == 0)
        {
            return;
        }

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnChanged;
            watcher.Changed -= OnChanged;
            watcher.Deleted -= OnChanged;
            watcher.Renamed -= OnChanged;
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }
}
