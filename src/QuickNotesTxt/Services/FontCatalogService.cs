using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class FontCatalogService : IFontCatalogService
{
    public const string DefaultFontKey = "IosevkaSlab";
    public const string DefaultCodeFontKey = "JetBrainsMono";
    public const string DefaultVariantKey = "Regular";

    private const string AssetUriPrefix = "avares://QuickNotesTxt/Assets/Fonts";
    private const int NameIdFontFamily = 1;
    private const int NameIdFontSubfamily = 2;
    private const int NameIdFullName = 4;
    private const int NameIdTypographicFamily = 16;
    private const int NameIdTypographicSubfamily = 17;
    private const int PlatformWindows = 3;
    private const int WindowsUnicodeBmpEncoding = 1;
    private const int PlatformMac = 1;

    private static readonly Regex s_displayNameRegex = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);
    private static readonly string[] s_standardVariantOrder =
    [
        "Regular",
        "Italic",
        "ExtraLight",
        "ExtraLight Italic",
        "Light",
        "Light Italic",
        "Medium",
        "Medium Italic",
        "SemiBold",
        "SemiBold Italic",
        "Bold",
        "Bold Italic",
        "ExtraBold",
        "ExtraBold Italic"
    ];

    private readonly string _fontsDirectory;

    public FontCatalogService()
        : this(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts"))
    {
    }

    public FontCatalogService(string fontsDirectory)
    {
        _fontsDirectory = fontsDirectory;
    }

    public IReadOnlyList<BundledFontFamilyOption> LoadBundledFonts()
    {
        if (!Directory.Exists(_fontsDirectory))
        {
            return [CreateDefaultFamily()];
        }

        var fonts = Directory.EnumerateDirectories(_fontsDirectory)
            .Select(Path.GetFileName)
            .Where(static folderName => !string.IsNullOrWhiteSpace(folderName))
            .Select(static folderName => folderName!)
            .Select(BuildFontFamily)
            .Where(static family => family is not null)
            .Cast<BundledFontFamilyOption>()
            .OrderBy(static family => family.DisplayName, StringComparer.Ordinal)
            .ToList();

        if (fonts.Count == 0)
        {
            fonts.Add(CreateDefaultFamily());
        }

        return fonts;
    }

    private BundledFontFamilyOption? BuildFontFamily(string folderName)
    {
        var folderPath = Path.Combine(_fontsDirectory, folderName);
        var supportedFiles = Directory.EnumerateFiles(folderPath)
            .Where(IsSupportedFontFile)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (supportedFiles.Count == 0)
        {
            return null;
        }

        var variants = supportedFiles
            .Select(filePath => BuildVariant(folderName, filePath))
            .Where(static variant => variant is not null)
            .Cast<FontVariantDescriptor>()
            .Where(static variant => IsStandardVariant(variant.Option.DisplayName))
            .OrderBy(variant => GetVariantSortIndex(variant.Option.DisplayName))
            .ThenBy(variant => variant.Option.DisplayName, StringComparer.Ordinal)
            .ToList();

        if (variants.Count == 0)
        {
            return null;
        }

        var familyDisplayName = variants[0].FamilyDisplayName;
        var familyResourceUri = $"{AssetUriPrefix}/{folderName}#{familyDisplayName}";
        return new BundledFontFamilyOption(
            folderName,
            familyDisplayName,
            familyResourceUri,
            variants.Select(static variant => variant.Option).ToList());
    }

    private FontVariantDescriptor? BuildVariant(string folderName, string filePath)
    {
        var metadata = TryReadFontMetadataFromFile(filePath);
        var familyDisplayName = metadata?.TypographicFamilyName
            ?? metadata?.FamilyName
            ?? s_displayNameRegex.Replace(folderName, "$1 $2");

        var variantDisplayName = NormalizeVariantName(
            metadata?.TypographicSubfamilyName
            ?? metadata?.SubfamilyName
            ?? InferVariantNameFromFileName(filePath));

        if (string.IsNullOrWhiteSpace(variantDisplayName))
        {
            return null;
        }

        var (fontWeight, fontStyle) = ParseVariantStyle(variantDisplayName);
        return new FontVariantDescriptor(
            familyDisplayName,
            new BundledFontVariantOption(variantDisplayName, variantDisplayName, fontWeight, fontStyle));
    }

    internal static string? TryReadFontFamilyNameFromFile(string filePath)
    {
        var metadata = TryReadFontMetadataFromFile(filePath);
        return metadata?.TypographicFamilyName ?? metadata?.FamilyName;
    }

    private static FontMetadata? TryReadFontMetadataFromFile(string filePath)
    {
        const uint nameTag = 0x6E616D65;

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            stream.Seek(4, SeekOrigin.Begin);
            ushort numTables = ReadUInt16BigEndian(reader);
            stream.Seek(6, SeekOrigin.Current);

            uint nameTableOffset = 0;
            for (int i = 0; i < numTables; i++)
            {
                uint tag = ReadUInt32BigEndian(reader);
                stream.Seek(4, SeekOrigin.Current);
                uint offset = ReadUInt32BigEndian(reader);
                stream.Seek(4, SeekOrigin.Current);

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

            stream.Seek(nameTableOffset, SeekOrigin.Begin);
            stream.Seek(2, SeekOrigin.Current);
            ushort count = ReadUInt16BigEndian(reader);
            ushort stringOffset = ReadUInt16BigEndian(reader);
            long stringsStart = nameTableOffset + stringOffset;

            string? familyName = null;
            string? subfamilyName = null;
            string? fullName = null;
            string? typographicFamilyName = null;
            string? typographicSubfamilyName = null;
            string? macFamilyName = null;
            string? macSubfamilyName = null;
            string? macFullName = null;
            string? macTypographicFamilyName = null;
            string? macTypographicSubfamilyName = null;

            for (int i = 0; i < count; i++)
            {
                ushort platformId = ReadUInt16BigEndian(reader);
                ushort encodingId = ReadUInt16BigEndian(reader);
                stream.Seek(2, SeekOrigin.Current);
                ushort nameId = ReadUInt16BigEndian(reader);
                ushort length = ReadUInt16BigEndian(reader);
                ushort nameOffset = ReadUInt16BigEndian(reader);

                long savedPos = stream.Position;
                stream.Seek(stringsStart + nameOffset, SeekOrigin.Begin);
                byte[] nameBytes = reader.ReadBytes(length);
                stream.Seek(savedPos, SeekOrigin.Begin);

                string? decodedName = platformId switch
                {
                    PlatformWindows when encodingId == WindowsUnicodeBmpEncoding => Encoding.BigEndianUnicode.GetString(nameBytes),
                    PlatformMac => Encoding.ASCII.GetString(nameBytes),
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(decodedName))
                {
                    continue;
                }

                if (platformId == PlatformWindows)
                {
                    AssignName(nameId, decodedName, ref familyName, ref subfamilyName, ref fullName, ref typographicFamilyName, ref typographicSubfamilyName);
                }
                else if (platformId == PlatformMac)
                {
                    AssignName(nameId, decodedName, ref macFamilyName, ref macSubfamilyName, ref macFullName, ref macTypographicFamilyName, ref macTypographicSubfamilyName);
                }
            }

            return new FontMetadata(
                typographicFamilyName ?? familyName ?? macTypographicFamilyName ?? macFamilyName,
                typographicSubfamilyName ?? subfamilyName ?? macTypographicSubfamilyName ?? macSubfamilyName,
                fullName ?? macFullName,
                typographicFamilyName ?? macTypographicFamilyName,
                typographicSubfamilyName ?? macTypographicSubfamilyName);
        }
        catch
        {
            return null;
        }
    }

    private static void AssignName(
        ushort nameId,
        string value,
        ref string? familyName,
        ref string? subfamilyName,
        ref string? fullName,
        ref string? typographicFamilyName,
        ref string? typographicSubfamilyName)
    {
        switch (nameId)
        {
            case NameIdFontFamily when familyName is null:
                familyName = value;
                break;
            case NameIdFontSubfamily when subfamilyName is null:
                subfamilyName = value;
                break;
            case NameIdFullName when fullName is null:
                fullName = value;
                break;
            case NameIdTypographicFamily when typographicFamilyName is null:
                typographicFamilyName = value;
                break;
            case NameIdTypographicSubfamily when typographicSubfamilyName is null:
                typographicSubfamilyName = value;
                break;
        }
    }

    private static bool IsSupportedFontFile(string filePath)
    {
        return filePath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
            || filePath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStandardVariant(string variantName)
    {
        return s_standardVariantOrder.Contains(variantName, StringComparer.Ordinal);
    }

    private static int GetVariantSortIndex(string variantName)
    {
        var index = Array.FindIndex(s_standardVariantOrder, candidate => string.Equals(candidate, variantName, StringComparison.Ordinal));
        return index >= 0 ? index : int.MaxValue;
    }

    private static string NormalizeVariantName(string value)
    {
        var normalized = value.Replace("-", " ", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return DefaultVariantKey;
        }

        normalized = Regex.Replace(normalized, "\\s+", " ");
        normalized = normalized switch
        {
            "Roman" => DefaultVariantKey,
            _ => normalized
        };

        return normalized;
    }

    private static string InferVariantNameFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var hyphenIndex = fileName.LastIndexOf('-');
        if (hyphenIndex < 0 || hyphenIndex == fileName.Length - 1)
        {
            return DefaultVariantKey;
        }

        return fileName[(hyphenIndex + 1)..];
    }

    private static BundledFontFamilyOption CreateDefaultFamily()
    {
        var displayName = s_displayNameRegex.Replace(DefaultFontKey, "$1 $2");
        var resourceUri = $"{AssetUriPrefix}/{DefaultFontKey}#{displayName}";
        return new BundledFontFamilyOption(
            DefaultFontKey,
            displayName,
            resourceUri,
            [new BundledFontVariantOption(DefaultVariantKey, DefaultVariantKey, FontWeight.Normal, FontStyle.Normal)]);
    }

    private static (FontWeight FontWeight, FontStyle FontStyle) ParseVariantStyle(string variantName)
    {
        var fontStyle = variantName.Contains("Italic", StringComparison.Ordinal) ? FontStyle.Italic : FontStyle.Normal;
        var weightToken = variantName.Replace(" Italic", string.Empty, StringComparison.Ordinal);

        var fontWeight = weightToken switch
        {
            "ExtraLight" => FontWeight.ExtraLight,
            "Light" => FontWeight.Light,
            "Medium" => FontWeight.Medium,
            "SemiBold" => FontWeight.SemiBold,
            "Bold" => FontWeight.Bold,
            "ExtraBold" => FontWeight.ExtraBold,
            _ => FontWeight.Normal
        };

        return (fontWeight, fontStyle);
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

    private sealed record FontMetadata(
        string? FamilyName,
        string? SubfamilyName,
        string? FullName,
        string? TypographicFamilyName,
        string? TypographicSubfamilyName);

    private sealed record FontVariantDescriptor(string FamilyDisplayName, BundledFontVariantOption Option);
}
