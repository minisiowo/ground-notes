using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class SettingsNoteWindowLayoutService
{
    private readonly ISettingsService _settingsService;

    public SettingsNoteWindowLayoutService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public NoteWindowLayout? GetLayout(NoteWindowMode mode)
    {
        var settings = _settingsService.GetSettingsSync();
        return NoteWindowLayout.Normalize(mode == NoteWindowMode.Zen
            ? settings.ZenNoteWindowLayout
            : settings.StandardNoteWindowLayout);
    }

    public void SaveLayout(NoteWindowMode mode, NoteWindowLayout layout)
    {
        var normalized = NoteWindowLayout.Normalize(layout);
        if (normalized is null)
        {
            return;
        }

        _settingsService.UpdateSettingsSync(settings => mode == NoteWindowMode.Zen
            ? settings with { ZenNoteWindowLayout = normalized }
            : settings with { StandardNoteWindowLayout = normalized });
    }
}
