namespace GroundNotes.Services;

public interface IFileWatcherService : IDisposable
{
    event EventHandler<NoteFileChangedEventArgs>? NoteChanged;

    void Watch(string folderPath);

    void Stop();
}
