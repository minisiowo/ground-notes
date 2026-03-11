using QuickNotesTxt.Services;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class FontCatalogServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "QuickNotesTxt.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _fontsDir;
    private readonly FontCatalogService _service;

    public FontCatalogServiceTests()
    {
        _fontsDir = Path.Combine(_tempRoot, "Assets", "Fonts");
        _service = new FontCatalogService(_fontsDir);
    }

    [Fact]
    public void LoadBundledFonts_ReturnsDefaultWhenDirectoryDoesNotExist()
    {
        var fonts = _service.LoadBundledFonts();

        var font = Assert.Single(fonts);
        Assert.Equal(FontCatalogService.DefaultFontKey, font.Key);
        Assert.Equal("Iosevka Slab", font.DisplayName);
    }

    [Fact]
    public void LoadBundledFonts_LoadsValidFontFoldersInDeterministicOrder()
    {
        CreateFontFolder("IosevkaSerif", "IosevkaSerif-Regular.ttf");
        CreateFontFolder("IosevkaSlab", "IosevkaSlab-Regular.ttf");

        var fonts = _service.LoadBundledFonts();

        Assert.Collection(fonts,
            font =>
            {
                Assert.Equal("Iosevka Serif", font.DisplayName);
                Assert.Equal("avares://QuickNotesTxt/Assets/Fonts/IosevkaSerif#Iosevka Serif", font.ResourceUri);
            },
            font =>
            {
                Assert.Equal("Iosevka Slab", font.DisplayName);
                Assert.Equal("avares://QuickNotesTxt/Assets/Fonts/IosevkaSlab#Iosevka Slab", font.ResourceUri);
            });
    }

    [Fact]
    public void LoadBundledFonts_IgnoresFoldersWithoutSupportedFontFiles()
    {
        Directory.CreateDirectory(Path.Combine(_fontsDir, "EmptyFont"));
        CreateFontFolder("IosevkaSlab", "IosevkaSlab-Regular.ttf");
        File.WriteAllText(Path.Combine(_fontsDir, "EmptyFont", "readme.txt"), "not a font");

        var fonts = _service.LoadBundledFonts();

        var font = Assert.Single(fonts);
        Assert.Equal("IosevkaSlab", font.Key);
    }

    [Fact]
    public void LoadBundledFonts_UsesActualFontFamilyNameFromTtfFile()
    {
        // Create a folder named "IosevkaSerif" but with a font file whose
        // embedded family name is "Iosevka" — the service should use the
        // real embedded name, not the folder name.
        var folderPath = Path.Combine(_fontsDir, "IosevkaSerif");
        Directory.CreateDirectory(folderPath);
        var fontFile = Path.Combine(folderPath, "Iosevka-Regular.ttf");
        File.WriteAllBytes(fontFile, BuildMinimalTtfWithFamilyName("Iosevka"));

        var fonts = _service.LoadBundledFonts();

        var font = Assert.Single(fonts);
        Assert.Equal("Iosevka", font.DisplayName);
        Assert.Equal("avares://QuickNotesTxt/Assets/Fonts/IosevkaSerif#Iosevka", font.ResourceUri);
    }

    [Fact]
    public void TryReadFontFamilyNameFromFile_ReturnsNull_ForInvalidFile()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "bad.ttf");
        File.WriteAllText(filePath, "not a font");

        var result = FontCatalogService.TryReadFontFamilyNameFromFile(filePath);

        Assert.Null(result);
    }

    [Fact]
    public void TryReadFontFamilyNameFromFile_ReadsEmbeddedName()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "test.ttf");
        File.WriteAllBytes(filePath, BuildMinimalTtfWithFamilyName("Test Family"));

        var result = FontCatalogService.TryReadFontFamilyNameFromFile(filePath);

        Assert.Equal("Test Family", result);
    }

    private void CreateFontFolder(string folderName, string fileName)
    {
        var folderPath = Path.Combine(_fontsDir, folderName);
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, fileName), "font");
    }

    /// <summary>
    /// Builds a minimal TrueType file with a single 'name' table containing
    /// only a Font Family (Name ID 1) record using Windows/Unicode BMP encoding.
    /// </summary>
    private static byte[] BuildMinimalTtfWithFamilyName(string familyName)
    {
        var nameBytes = System.Text.Encoding.BigEndianUnicode.GetBytes(familyName);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // ── Offset Table ──────────────────
        bw.Write(BeToBe((uint)0x00010000)); // sfVersion
        bw.Write(BeToBe((ushort)1));        // numTables = 1
        bw.Write(BeToBe((ushort)16));       // searchRange
        bw.Write(BeToBe((ushort)0));        // entrySelector
        bw.Write(BeToBe((ushort)16));       // rangeShift

        // ── Table Record for 'name' ──────
        uint nameTag = 0x6E616D65;
        bw.Write(BeToBe(nameTag));          // tag
        bw.Write(BeToBe((uint)0));          // checkSum (not validated)
        uint tableOffsetPos = (uint)ms.Position;
        bw.Write(BeToBe((uint)0));          // offset (placeholder)
        bw.Write(BeToBe((uint)0));          // length (placeholder)

        // ── 'name' table ─────────────────
        uint nameTableStart = (uint)ms.Position;

        // Name table header
        bw.Write(BeToBe((ushort)0));        // format
        bw.Write(BeToBe((ushort)1));        // count = 1 record
        ushort stringOffsetInTable = 6 + 12; // header(6) + 1 record(12)
        bw.Write(BeToBe(stringOffsetInTable));

        // Name record: platformID=3 (Windows), encodingID=1 (Unicode BMP),
        // languageID=0x0409 (en-US), nameID=1 (Font Family)
        bw.Write(BeToBe((ushort)3));        // platformID
        bw.Write(BeToBe((ushort)1));        // encodingID
        bw.Write(BeToBe((ushort)0x0409));   // languageID
        bw.Write(BeToBe((ushort)1));        // nameID
        bw.Write(BeToBe((ushort)nameBytes.Length));
        bw.Write(BeToBe((ushort)0));        // offset into string storage

        // String storage
        bw.Write(nameBytes);

        uint nameTableEnd = (uint)ms.Position;

        // Patch table record offset & length
        ms.Seek(tableOffsetPos, SeekOrigin.Begin);
        bw.Write(BeToBe(nameTableStart));
        bw.Write(BeToBe(nameTableEnd - nameTableStart));

        return ms.ToArray();
    }

    private static ushort BeToBe(ushort value)
    {
        return (ushort)((value >> 8) | (value << 8));
    }

    private static uint BeToBe(uint value)
    {
        return ((value & 0xFF) << 24)
             | ((value & 0xFF00) << 8)
             | ((value & 0xFF0000) >> 8)
             | ((value & 0xFF000000) >> 24);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
