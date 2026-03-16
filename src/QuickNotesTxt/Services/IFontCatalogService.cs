using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface IFontCatalogService
{
    IReadOnlyList<BundledFontFamilyOption> LoadBundledFonts();
}
