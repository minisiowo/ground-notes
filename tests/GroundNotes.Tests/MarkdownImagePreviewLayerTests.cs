using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

using GroundNotes.Editors;
using GroundNotes.Services;

using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownImagePreviewLayerTests : IDisposable
{
    private static readonly Lock ApplicationLock = new();
    private static bool _applicationInitialized;

    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void Refresh_RepositionsPreviewOnHorizontalScrollWithoutNewPreviewRequest()
    {
        EnsureApplication();
        Directory.CreateDirectory(_tempDirectory);

        var imagePath = CreateImageAsset(_tempDirectory, "photo.png", 6, 3);
        var relativeImagePath = Path.GetRelativePath(_tempDirectory, imagePath).Replace('\\', '/');
        var document = new TextDocument(string.Join(
            Environment.NewLine,
            new string('x', 400),
            $"![]({relativeImagePath})|100"));

        using var colorizer = new MarkdownColorizingTransformer();
        using var previewProvider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        previewProvider.SetBaseDirectoryPath(_tempDirectory);
        previewProvider.SetAvailableWidth(240);

        var textView = new TextView
        {
            Document = document,
            Width = 240,
            Height = 200
        };
        textView.LineTransformers.Add(new MarkdownImageVisualLineTransformer(previewProvider));
        textView.LineTransformers.Add(colorizer);

        var window = new Window
        {
            Width = textView.Width,
            Height = textView.Height,
            Content = textView
        };

        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        ApplyTextViewLayout(textView, textView.Width, textView.Height);

        using var previewLayer = new MarkdownImagePreviewLayer(textView, previewProvider, subscribeToTextViewEvents: false);
        previewLayer.Refresh();
        var initialBounds = GetRenderedPreviewBounds(previewLayer, 2);

        MarkdownDiagnostics.Reset();
        ((IScrollable)textView).Offset = new Vector(48, 0);
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        previewLayer.Refresh();
        var scrolledBounds = GetRenderedPreviewBounds(previewLayer, 2);
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.True(scrolledBounds.X < initialBounds.X, $"Initial X={initialBounds.X}, Scrolled X={scrolledBounds.X}");
        Assert.Equal(initialBounds.Y, scrolledBounds.Y);
        Assert.Equal(0, diagnostics.ImagePreviewRequests);
        Assert.True(diagnostics.PreviewLayerLineStateReuses > 0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
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

    private static void ApplyTextViewLayout(TextView textView, double width, double height)
    {
        textView.Measure(new Size(width, height));
        textView.Arrange(new Rect(0, 0, width, height));
        textView.InvalidateMeasure();
        textView.InvalidateArrange();
        textView.InvalidateVisual();
        textView.Redraw();
        textView.EnsureVisualLines();
    }

    private static Rect GetRenderedPreviewBounds(MarkdownImagePreviewLayer previewLayer, int lineNumber)
    {
        var field = typeof(MarkdownImagePreviewLayer).GetField("_renderedPreviews", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var previews = field.GetValue(previewLayer);
        Assert.NotNull(previews);

        var indexer = previews.GetType().GetProperty("Item");
        Assert.NotNull(indexer);

        var renderedPreview = indexer.GetValue(previews, [lineNumber]);
        Assert.NotNull(renderedPreview);

        var boundsProperty = renderedPreview.GetType().GetProperty("Bounds");
        Assert.NotNull(boundsProperty);

        return Assert.IsType<Rect>(boundsProperty.GetValue(renderedPreview));
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
}
