namespace QuickNotesTxt.Services;

public interface IFileWatcherService : IDisposable
{
    event EventHandler? NotesChanged;

    void Watch(string folderPath);

    void Stop();
}
