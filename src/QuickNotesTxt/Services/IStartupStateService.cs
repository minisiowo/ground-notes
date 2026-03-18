using QuickNotesTxt.Models;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Services;

public interface IStartupStateService
{
    StartupStateSnapshot Load();
}
