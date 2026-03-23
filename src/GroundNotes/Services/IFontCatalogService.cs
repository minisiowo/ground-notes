using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IFontCatalogService
{
    IReadOnlyList<BundledFontFamilyOption> LoadBundledFonts();
}
