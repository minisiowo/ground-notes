using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class SettingsWindowLayoutService : IWindowLayoutService
{
    private readonly ISettingsService _settingsService;

    public SettingsWindowLayoutService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public WindowLayout? GetWindowLayoutSync()
    {
        return _settingsService.GetSettingsSync().WindowLayout;
    }

    public async Task<WindowLayout?> GetWindowLayoutAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        return settings.WindowLayout;
    }

    public async Task SaveWindowLayoutAsync(WindowLayout layout, CancellationToken cancellationToken = default)
    {
        await _settingsService.UpdateSettingsAsync(settings => settings with { WindowLayout = layout }, cancellationToken);
    }

    public void SaveWindowLayoutSync(WindowLayout layout)
    {
        var settings = _settingsService.GetSettingsSync() with { WindowLayout = layout };
        _settingsService.SaveSettingsSync(settings);
    }
}
