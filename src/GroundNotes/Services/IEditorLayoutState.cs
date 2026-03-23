using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IEditorLayoutState
{
    EditorLayoutSettings CurrentSettings { get; }

    event EventHandler<EditorLayoutSettings>? SettingsChanged;

    void Set(EditorLayoutSettings settings);
}
