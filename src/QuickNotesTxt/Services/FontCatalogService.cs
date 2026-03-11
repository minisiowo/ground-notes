using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class FontCatalogService : IFontCatalogService
{
    public const string DefaultFontKey = "IosevkaSlab";
    private const string AssetUriPrefix = "avares://QuickNotesTxt/Assets/Fonts";

    private static readonly Regex s_displayNameRegex = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);
    private readonly string _fontsDirectory;

    public FontCatalogService()
        : this(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts"))
    {
    }

    public FontCatalogService(string fontsDirectory)
    {
        _fontsDirectory = fontsDirectory;
    }

    public IReadOnlyList<BundledFontOption> LoadBundledFonts()
    {
        if (!Directory.Exists(_fontsDirectory))
        {
            return [CreateFontOption(DefaultFontKey, displayName: null)];
        }

        var fonts = Directory.EnumerateDirectories(_fontsDirectory)
            .Select(Path.GetFileName)
            .Where(static folderName => !string.IsNullOrWhiteSpace(folderName))
            .Select(static folderName => folderName!)
            .Where(ContainsSupportedFontFiles)
            .Select(folderName => CreateFontOption(folderName, ReadFontFamilyName(folderName)))
            .OrderBy(static option => option.DisplayName, StringComparer.Ordinal)
            .ToList();

        if (fonts.Count == 0)
        {
            fonts.Add(CreateFontOption(DefaultFontKey, displayName: null));
        }

        return fonts;
    }

    private bool ContainsSupportedFontFiles(string folderName)
    {
        var folderPath = Path.Combine(_fontsDirectory, folderName);
        return Directory.EnumerateFiles(folderPath)
            .Any(static file =>
                file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads the font family name (Name ID 1) from the first supported font file
    /// in the given folder. Returns <c>null</c> when parsing fails or the folder
    /// does not exist on disk.
    /// </summary>
    private string? ReadFontFamilyName(string folderName)
    {
        var folderPath = Path.Combine(_fontsDirectory, folderName);
        if (!Directory.Exists(folderPath))
        {
            return null;
        }

        var fontFile = Directory.EnumerateFiles(folderPath)
            .FirstOrDefault(static file =>
                file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

        if (fontFile is null)
        {
            return null;
        }

        return TryReadFontFamilyNameFromFile(fontFile);
    }

    /// <summary>
    /// Parses the TrueType/OpenType 'name' table to extract the font family name
    /// (Name ID 1). Prefers a Windows/Unicode platform entry; falls back to
    /// Macintosh/Roman when no Windows record is found.
    /// </summary>
    internal static string? TryReadFontFamilyNameFromFile(string filePath)
    {
        const uint nameTag = 0x6E616D65; // 'name'
        const int NameIdFontFamily = 1;
        const int PlatformWindows = 3;
        const int PlatformMac = 1;

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            // ── Offset Table ─────────────────────────
            stream.Seek(4, SeekOrigin.Begin); // skip sfVersion
            ushort numTables = ReadUInt16BigEndian(reader);
            stream.Seek(6, SeekOrigin.Current); // skip searchRange, entrySelector, rangeShift

            // ── Find 'name' table ────────────────────
            uint nameTableOffset = 0;
            for (int i = 0; i < numTables; i++)
            {
                uint tag = ReadUInt32BigEndian(reader);
                stream.Seek(4, SeekOrigin.Current); // checkSum
                uint offset = ReadUInt32BigEndian(reader);
                stream.Seek(4, SeekOrigin.Current); // length

                if (tag == nameTag)
                {
                    nameTableOffset = offset;
                    break;
                }
            }

            if (nameTableOffset == 0)
            {
                return null;
            }

            // ── Parse 'name' table header ────────────
            stream.Seek(nameTableOffset, SeekOrigin.Begin);
            stream.Seek(2, SeekOrigin.Current); // format
            ushort count = ReadUInt16BigEndian(reader);
            ushort stringOffset = ReadUInt16BigEndian(reader);
            long stringsStart = nameTableOffset + stringOffset;

            // ── Scan name records ────────────────────
            string? windowsName = null;
            string? macName = null;

            for (int i = 0; i < count; i++)
            {
                ushort platformId = ReadUInt16BigEndian(reader);
                ushort encodingId = ReadUInt16BigEndian(reader);
                stream.Seek(2, SeekOrigin.Current); // languageId
                ushort nameId = ReadUInt16BigEndian(reader);
                ushort length = ReadUInt16BigEndian(reader);
                ushort nameOffset = ReadUInt16BigEndian(reader);

                if (nameId != NameIdFontFamily)
                {
                    continue;
                }

                long savedPos = stream.Position;
                stream.Seek(stringsStart + nameOffset, SeekOrigin.Begin);
                byte[] nameBytes = reader.ReadBytes(length);
                stream.Seek(savedPos, SeekOrigin.Begin);

                if (platformId == PlatformWindows && encodingId == 1)
                {
                    windowsName = Encoding.BigEndianUnicode.GetString(nameBytes);
                    break; // Windows entry is preferred; stop scanning.
                }

                if (platformId == PlatformMac && macName is null)
                {
                    macName = Encoding.ASCII.GetString(nameBytes);
                }
            }

            var familyName = windowsName ?? macName;
            return string.IsNullOrWhiteSpace(familyName) ? null : familyName;
        }
        catch
        {
            return null;
        }
    }

    private static BundledFontOption CreateFontOption(string key, string? displayName)
    {
        displayName ??= s_displayNameRegex.Replace(key, "$1 $2");
        var resourceUri = $"{AssetUriPrefix}/{key}#{displayName}";
        return new BundledFontOption(key, displayName, resourceUri);
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        Span<byte> buf = stackalloc byte[2];
        reader.BaseStream.ReadExactly(buf);
        return BinaryPrimitives.ReadUInt16BigEndian(buf);
    }

    private static uint ReadUInt32BigEndian(BinaryReader reader)
    {
        Span<byte> buf = stackalloc byte[4];
        reader.BaseStream.ReadExactly(buf);
        return BinaryPrimitives.ReadUInt32BigEndian(buf);
    }
}
