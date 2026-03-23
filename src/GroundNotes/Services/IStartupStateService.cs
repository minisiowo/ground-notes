using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Services;

public interface IStartupStateService
{
    StartupStateSnapshot Load();
}
