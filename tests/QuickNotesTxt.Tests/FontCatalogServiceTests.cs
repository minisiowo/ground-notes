using System.Text;
using Avalonia.Media;
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

        var variant = Assert.Single(font.StandardVariants);
        Assert.Equal(FontCatalogService.DefaultVariantKey, variant.DisplayName);
        Assert.Equal(FontWeight.Normal, variant.FontWeight);
        Assert.Equal(FontStyle.Normal, variant.FontStyle);
    }

    [Fact]
    public void LoadBundledFonts_LoadsFamiliesInDeterministicOrder()
    {
        CreateFontFile("IosevkaSerif", "Iosevka-Regular.ttf", "Iosevka Serif", "Regular", "Iosevka Serif Regular");
        CreateFontFile("IosevkaSlab", "IosevkaSlab-Regular.ttf", "Iosevka Slab", "Regular", "Iosevka Slab Regular");

        var fonts = _service.LoadBundledFonts();

        Assert.Collection(fonts,
            font => Assert.Equal("Iosevka Serif", font.DisplayName),
            font => Assert.Equal("Iosevka Slab", font.DisplayName));
    }

    [Fact]
    public void LoadBundledFonts_IgnoresFoldersWithoutSupportedFontFiles()
    {
        Directory.CreateDirectory(Path.Combine(_fontsDir, "EmptyFont"));
        File.WriteAllText(Path.Combine(_fontsDir, "EmptyFont", "readme.txt"), "not a font");
        CreateFontFile("IosevkaSlab", "IosevkaSlab-Regular.ttf", "Iosevka Slab", "Regular", "Iosevka Slab Regular");

        var fonts = _service.LoadBundledFonts();

        var font = Assert.Single(fonts);
        Assert.Equal("IosevkaSlab", font.Key);
    }

    [Fact]
    public void LoadBundledFonts_UsesEmbeddedFamilyMetadata()
    {
        CreateFontFile("IosevkaSerif", "Custom-Regular.ttf", "Ignored Folder Name", "Regular", "Ignored Folder Name Regular", typographicFamilyName: "Iosevka");

        var fonts = _service.LoadBundledFonts();

        var font = Assert.Single(fonts);
        Assert.Equal("Iosevka", font.DisplayName);
    }

    [Fact]
    public void LoadBundledFonts_FiltersAdvancedVariantsAndSortsStandardVariants()
    {
        CreateFontFile("MonaspaceKrypton", "MonaspaceKrypton-ExtraBold.otf", "Monaspace Krypton", "ExtraBold", "Monaspace Krypton ExtraBold");
        CreateFontFile("MonaspaceKrypton", "MonaspaceKrypton-Regular.otf", "Monaspace Krypton", "Regular", "Monaspace Krypton Regular");
        CreateFontFile("MonaspaceKrypton", "MonaspaceKrypton-Italic.otf", "Monaspace Krypton", "Italic", "Monaspace Krypton Italic");
        CreateFontFile("MonaspaceKrypton", "MonaspaceKrypton-WideRegular.otf", "Monaspace Krypton", "Regular", "Monaspace Krypton Wide Regular", typographicSubfamilyName: "Wide Regular");
        CreateFontFile("MonaspaceKrypton", "MonaspaceKrypton-SemiWideBold.otf", "Monaspace Krypton", "Bold", "Monaspace Krypton SemiWide Bold", typographicSubfamilyName: "SemiWide Bold");

        var fonts = _service.LoadBundledFonts();

        var family = Assert.Single(fonts);
        Assert.Equal("Monaspace Krypton", family.DisplayName);
        Assert.Equal(new[] { "Regular", "Italic", "ExtraBold" }, family.StandardVariants.Select(variant => variant.DisplayName).ToArray());
        Assert.All(family.StandardVariants, variant => Assert.DoesNotContain("Wide", variant.DisplayName, StringComparison.Ordinal));
        Assert.Equal(FontWeight.Normal, family.StandardVariants[0].FontWeight);
        Assert.Equal(FontStyle.Normal, family.StandardVariants[0].FontStyle);
        Assert.Equal(FontWeight.Normal, family.StandardVariants[1].FontWeight);
        Assert.Equal(FontStyle.Italic, family.StandardVariants[1].FontStyle);
        Assert.Equal(FontWeight.ExtraBold, family.StandardVariants[2].FontWeight);
        Assert.Equal(FontStyle.Normal, family.StandardVariants[2].FontStyle);
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
        File.WriteAllBytes(filePath, BuildMinimalTtf("Test Family", "Regular", "Test Family Regular"));

        var result = FontCatalogService.TryReadFontFamilyNameFromFile(filePath);

        Assert.Equal("Test Family", result);
    }

    private void CreateFontFile(
        string folderName,
        string fileName,
        string familyName,
        string subfamilyName,
        string fullName,
        string? typographicFamilyName = null,
        string? typographicSubfamilyName = null)
    {
        var folderPath = Path.Combine(_fontsDir, folderName);
        Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, fileName);
        File.WriteAllBytes(filePath, BuildMinimalTtf(familyName, subfamilyName, fullName, typographicFamilyName, typographicSubfamilyName));
    }

    private static byte[] BuildMinimalTtf(
        string familyName,
        string subfamilyName,
        string fullName,
        string? typographicFamilyName = null,
        string? typographicSubfamilyName = null)
    {
        var records = new List<(ushort NameId, byte[] Value)>
        {
            ((ushort)1, Encoding.BigEndianUnicode.GetBytes(familyName)),
            ((ushort)2, Encoding.BigEndianUnicode.GetBytes(subfamilyName)),
            ((ushort)4, Encoding.BigEndianUnicode.GetBytes(fullName))
        };

        if (!string.IsNullOrWhiteSpace(typographicFamilyName))
        {
            records.Add(((ushort)16, Encoding.BigEndianUnicode.GetBytes(typographicFamilyName)));
        }

        if (!string.IsNullOrWhiteSpace(typographicSubfamilyName))
        {
            records.Add(((ushort)17, Encoding.BigEndianUnicode.GetBytes(typographicSubfamilyName)));
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(BeToBe((uint)0x00010000));
        bw.Write(BeToBe((ushort)1));
        bw.Write(BeToBe((ushort)16));
        bw.Write(BeToBe((ushort)0));
        bw.Write(BeToBe((ushort)16));

        uint nameTag = 0x6E616D65;
        bw.Write(BeToBe(nameTag));
        bw.Write(BeToBe((uint)0));
        uint tableOffsetPos = (uint)ms.Position;
        bw.Write(BeToBe((uint)0));
        bw.Write(BeToBe((uint)0));

        uint nameTableStart = (uint)ms.Position;
        bw.Write(BeToBe((ushort)0));
        bw.Write(BeToBe((ushort)records.Count));
        ushort stringOffsetInTable = (ushort)(6 + (records.Count * 12));
        bw.Write(BeToBe(stringOffsetInTable));

        ushort stringStorageOffset = 0;
        foreach (var (nameId, value) in records)
        {
            bw.Write(BeToBe((ushort)3));
            bw.Write(BeToBe((ushort)1));
            bw.Write(BeToBe((ushort)0x0409));
            bw.Write(BeToBe(nameId));
            bw.Write(BeToBe((ushort)value.Length));
            bw.Write(BeToBe(stringStorageOffset));
            stringStorageOffset += (ushort)value.Length;
        }

        foreach (var (_, value) in records)
        {
            bw.Write(value);
        }

        uint nameTableEnd = (uint)ms.Position;

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
