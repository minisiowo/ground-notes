using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface IEditorLayoutState
{
    EditorLayoutSettings CurrentSettings { get; }

    event EventHandler<EditorLayoutSettings>? SettingsChanged;

    void Set(EditorLayoutSettings settings);
}
