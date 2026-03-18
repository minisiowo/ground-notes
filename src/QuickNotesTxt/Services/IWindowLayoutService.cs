using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface IWindowLayoutService
{
    WindowLayout? GetWindowLayoutSync();

    Task<WindowLayout?> GetWindowLayoutAsync(CancellationToken cancellationToken = default);

    Task SaveWindowLayoutAsync(WindowLayout layout, CancellationToken cancellationToken = default);

    void SaveWindowLayoutSync(WindowLayout layout);
}
