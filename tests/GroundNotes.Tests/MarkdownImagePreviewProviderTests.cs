using Avalonia;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownImagePreviewProviderTests : IDisposable
{
    private static readonly Lock ApplicationLock = new();
    private static bool _applicationInitialized;

    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetPreview_ReturnsStandaloneImageAndReusesPreviewCaches()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePath = CreateImageAsset(_tempDirectory, "photo.png", 2, 1);
        var document = new TextDocument($"![]({Path.GetRelativePath(_tempDirectory, imagePath).Replace('\\', '/')})|400");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(100);
        MarkdownDiagnostics.Reset();

        var firstPreview = provider.GetPreview(document, document.GetLineByNumber(1));
        var secondPreview = provider.GetPreview(document, document.GetLineByNumber(1));
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Single(firstPreview);
        Assert.Single(secondPreview);
        Assert.Equal(8, firstPreview[0].Width);
        Assert.Equal(8, secondPreview[0].Width);
        Assert.Equal(2, diagnostics.ImagePreviewRequests);
        Assert.Equal(1, diagnostics.ImagePreviewCacheHits);
        Assert.Equal(1, diagnostics.ImagePreviewCacheMisses);
        Assert.Equal(1, diagnostics.ImagePreviewRenderCacheHits);
        Assert.Equal(1, diagnostics.ImagePreviewRenderCacheMisses);
        Assert.Equal(0, diagnostics.BitmapCacheHits);
        Assert.Equal(1, diagnostics.BitmapCacheMisses);
    }

    [Fact]
    public void GetPreview_RecomputesScaledSizeWhenAvailableWidthChanges()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePath = CreateImageAsset(_tempDirectory, "photo.png", 2, 1);
        var document = new TextDocument($"![]({Path.GetRelativePath(_tempDirectory, imagePath).Replace('\\', '/')})|400");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(25);
        MarkdownDiagnostics.Reset();

        var narrowPreview = provider.GetPreview(document, document.GetLineByNumber(1));
        provider.SetAvailableWidth(100);
        var widePreview = provider.GetPreview(document, document.GetLineByNumber(1));
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Single(narrowPreview);
        Assert.Single(widePreview);
        Assert.Equal(1, narrowPreview[0].Width);
        Assert.Equal(8, widePreview[0].Width);
        Assert.Equal(1, diagnostics.ImagePreviewCacheHits);
        Assert.Equal(1, diagnostics.ImagePreviewCacheMisses);
        Assert.Equal(0, diagnostics.ImagePreviewRenderCacheHits);
        Assert.Equal(2, diagnostics.ImagePreviewRenderCacheMisses);
    }

    [Fact]
    public void GetPreview_ReturnsNullForImageInsideFence()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePath = CreateImageAsset(_tempDirectory, "photo.png", 2, 1);
        var document = new TextDocument($"before\n```\n![]({Path.GetRelativePath(_tempDirectory, imagePath).Replace('\\', '/')})|400\n```\nafter");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);

        var preview = provider.GetPreview(document, document.GetLineByNumber(3));

        Assert.Empty(preview);
    }

    [Fact]
    public void GetPreview_InvalidatesPreviewAndBitmapCachesWhenImageFileChanges()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePath = CreateImageAsset(_tempDirectory, "photo.png", 2, 1);
        var document = new TextDocument($"![]({Path.GetRelativePath(_tempDirectory, imagePath).Replace('\\', '/')})");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(100);
        MarkdownDiagnostics.Reset();

        var firstPreview = provider.GetPreview(document, document.GetLineByNumber(1));
        var updatedLastWrite = File.GetLastWriteTimeUtc(imagePath).AddSeconds(2);
        File.WriteAllBytes(imagePath, CreatePngBytes(4, 2));
        File.SetLastWriteTimeUtc(imagePath, updatedLastWrite);
        var secondPreview = provider.GetPreview(document, document.GetLineByNumber(1));
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Single(firstPreview);
        Assert.Single(secondPreview);
        Assert.Equal(2, firstPreview[0].Width);
        Assert.Equal(4, secondPreview[0].Width);
        Assert.Equal(1, diagnostics.ImagePreviewCacheHits);
        Assert.Equal(1, diagnostics.ImagePreviewCacheMisses);
        Assert.Equal(0, diagnostics.ImagePreviewRenderCacheHits);
        Assert.Equal(2, diagnostics.ImagePreviewRenderCacheMisses);
        Assert.Equal(0, diagnostics.BitmapCacheHits);
        Assert.Equal(2, diagnostics.BitmapCacheMisses);
    }

    [Fact]
    public void InvalidateImage_RemovesPreviewAndBitmapCachesForPath()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePath = CreateImageAsset(_tempDirectory, "photo.png", 2, 1);
        var document = new TextDocument($"![]({Path.GetRelativePath(_tempDirectory, imagePath).Replace('\\', '/')})");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(100);
        MarkdownDiagnostics.Reset();

        _ = provider.GetPreview(document, document.GetLineByNumber(1));
        _ = provider.GetPreview(document, document.GetLineByNumber(1));
        provider.InvalidateImage(imagePath);
        _ = provider.GetPreview(document, document.GetLineByNumber(1));
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Equal(1, diagnostics.ImagePreviewRenderCacheHits);
        Assert.Equal(2, diagnostics.ImagePreviewRenderCacheMisses);
        Assert.Equal(0, diagnostics.BitmapCacheHits);
        Assert.Equal(2, diagnostics.BitmapCacheMisses);
    }

    [Fact]
    public void GetPreview_EvictsLeastRecentlyUsedBitmapWhenCacheLimitIsExceeded()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var firstImagePath = CreateImageAsset(_tempDirectory, "first.png", 2, 1);
        var secondImagePath = CreateImageAsset(_tempDirectory, "second.png", 3, 1);
        var thirdImagePath = CreateImageAsset(_tempDirectory, "third.png", 4, 1);
        var document = new TextDocument(string.Join(
            Environment.NewLine,
            $"![]({Path.GetRelativePath(_tempDirectory, firstImagePath).Replace('\\', '/')})",
            $"![]({Path.GetRelativePath(_tempDirectory, secondImagePath).Replace('\\', '/')})",
            $"![]({Path.GetRelativePath(_tempDirectory, thirdImagePath).Replace('\\', '/')})"));
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService(), maxBitmapCacheEntries: 2);
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(100);
        MarkdownDiagnostics.Reset();

        _ = provider.GetPreview(document, document.GetLineByNumber(1));
        _ = provider.GetPreview(document, document.GetLineByNumber(2));
        _ = provider.GetPreview(document, document.GetLineByNumber(3));
        provider.SetAvailableWidth(99);
        _ = provider.GetPreview(document, document.GetLineByNumber(1));
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Equal(0, diagnostics.BitmapCacheHits);
        Assert.Equal(4, diagnostics.BitmapCacheMisses);
        Assert.Equal(2, diagnostics.BitmapCacheEvictions);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static string CreateImageAsset(string baseDirectory, string fileName, int width, int height)
    {
        var imagePath = Path.Combine(baseDirectory, "assets", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllBytes(imagePath, CreatePngBytes(width, height));
        return imagePath;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        WriteChunk(output, "IHDR", CreateHeaderData(width, height));
        WriteChunk(output, "IDAT", CreateImageData(width, height));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] CreateHeaderData(int width, int height)
    {
        var header = new byte[13];
        WriteInt32BigEndian(header.AsSpan(0, 4), width);
        WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        return header;
    }

    private static byte[] CreateImageData(int width, int height)
    {
        var stride = width * 4 + 1;
        var raw = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            raw[rowStart] = 0;
            for (var x = 0; x < width; x++)
            {
                var pixelStart = rowStart + 1 + x * 4;
                raw[pixelStart] = 255;
                raw[pixelStart + 1] = 255;
                raw[pixelStart + 2] = 255;
                raw[pixelStart + 3] = 255;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string chunkType, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(chunkType);
        var lengthBuffer = new byte[4];
        WriteInt32BigEndian(lengthBuffer, data.Length);
        output.Write(lengthBuffer);
        output.Write(typeBytes);
        output.Write(data);

        var crcBuffer = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcBuffer, 0);
        data.CopyTo(crcBuffer, typeBytes.Length);

        var crcBytes = new byte[4];
        WriteUInt32BigEndian(crcBytes, ComputeCrc32(crcBuffer));
        output.Write(crcBytes);
    }

    private static uint ComputeCrc32(byte[] bytes)
    {
        const uint polynomial = 0xEDB88320u;
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ polynomial;
            }
        }

        return ~crc;
    }

    private static void WriteInt32BigEndian(Span<byte> destination, int value)
    {
        destination[0] = (byte)((value >> 24) & 0xFF);
        destination[1] = (byte)((value >> 16) & 0xFF);
        destination[2] = (byte)((value >> 8) & 0xFF);
        destination[3] = (byte)(value & 0xFF);
    }

    private static void WriteUInt32BigEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)((value >> 24) & 0xFF);
        destination[1] = (byte)((value >> 16) & 0xFF);
        destination[2] = (byte)((value >> 8) & 0xFF);
        destination[3] = (byte)(value & 0xFF);
    }

    [Fact]
    public void GetPreview_ReturnsColumnLayoutWithTwoImages()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePathA = CreateImageAsset(_tempDirectory, "a.png", 1000, 1);
        var imagePathB = CreateImageAsset(_tempDirectory, "b.png", 1000, 1);
        var relA = Path.GetRelativePath(_tempDirectory, imagePathA).Replace('\\', '/');
        var relB = Path.GetRelativePath(_tempDirectory, imagePathB).Replace('\\', '/');
        var document = new TextDocument($"![]({relA})|100||![]({relB})|100");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(240);

        var preview = provider.GetPreview(document, document.GetLineByNumber(1));

        Assert.Equal(2, preview.Count);
        Assert.True(preview[0].Width > 0);
        Assert.True(preview[0].Width <= 102);
        Assert.True(preview[1].Width > 0);
        Assert.True(preview[1].Width <= 102);
    }

    [Fact]
    public void GetPreview_ColumnLayoutSkipsBrokenImage()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePath = CreateImageAsset(_tempDirectory, "valid.png", 1000, 1);
        var relValid = Path.GetRelativePath(_tempDirectory, imagePath).Replace('\\', '/');
        var document = new TextDocument($"![]({relValid})|100||![](missing.png)|100");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(240);

        var preview = provider.GetPreview(document, document.GetLineByNumber(1));

        Assert.Single(preview);
        Assert.Equal(imagePath, preview[0].ResolvedPath);
    }

    [Fact]
    public void GetPreview_ColumnLayoutRecomputesWhenWidthChanges()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);
        var imagePathA = CreateImageAsset(_tempDirectory, "a.png", 1000, 1);
        var imagePathB = CreateImageAsset(_tempDirectory, "b.png", 1000, 1);
        var relA = Path.GetRelativePath(_tempDirectory, imagePathA).Replace('\\', '/');
        var relB = Path.GetRelativePath(_tempDirectory, imagePathB).Replace('\\', '/');
        var document = new TextDocument($"![]({relA})|100||![]({relB})|100");
        using var colorizer = new MarkdownColorizingTransformer();
        using var provider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        provider.SetBaseDirectoryPath(_tempDirectory);
        provider.SetAvailableWidth(240);

        var widePreview = provider.GetPreview(document, document.GetLineByNumber(1));
        provider.SetAvailableWidth(120);
        var narrowPreview = provider.GetPreview(document, document.GetLineByNumber(1));

        Assert.Equal(2, widePreview.Count);
        Assert.Equal(2, narrowPreview.Count);
        Assert.True(narrowPreview[0].Width < widePreview[0].Width);
        Assert.True(narrowPreview[1].Width < widePreview[1].Width);
    }

    private static void EnsureApplication()
    {
        lock (ApplicationLock)
        {
            if (_applicationInitialized || Application.Current is not null)
            {
                _applicationInitialized = true;
                return;
            }

            try
            {
                GroundNotes.Program.BuildAvaloniaApp().SetupWithoutStarting();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Setup was already called", StringComparison.Ordinal))
            {
            }

            _applicationInitialized = true;
        }
    }
}
