using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class EditorLayoutState : IEditorLayoutState
{
    private EditorLayoutSettings _currentSettings;

    public EditorLayoutState(EditorLayoutSettings initialSettings)
    {
        _currentSettings = EditorLayoutSettings.Normalize(initialSettings);
    }

    public EditorLayoutSettings CurrentSettings => _currentSettings;

    public event EventHandler<EditorLayoutSettings>? SettingsChanged;

    public void Set(EditorLayoutSettings settings)
    {
        var normalized = EditorLayoutSettings.Normalize(settings);
        if (normalized.Equals(_currentSettings))
        {
            return;
        }

        _currentSettings = normalized;
        SettingsChanged?.Invoke(this, normalized);
    }
}
