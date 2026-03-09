namespace QuickNotesTxt.Services;

public sealed class FileWatcherService : IFileWatcherService
{
    private FileSystemWatcher? _watcher;

    public event EventHandler? NotesChanged;

    public void Watch(string folderPath)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        _watcher = new FileSystemWatcher(folderPath, "*.txt")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnChanged;
        _watcher.Changed -= OnChanged;
        _watcher.Deleted -= OnChanged;
        _watcher.Renamed -= OnChanged;
        _watcher.Dispose();
        _watcher = null;
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
