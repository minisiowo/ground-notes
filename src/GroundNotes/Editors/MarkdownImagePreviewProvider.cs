using Avalonia;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;

using GroundNotes.Services;

namespace GroundNotes.Editors;

internal sealed class MarkdownImagePreviewProvider : IDisposable
{
    private const double HorizontalPadding = 24;
    private const double MaxRenderWidth = 960;

    private readonly MarkdownColorizingTransformer _colorizer;
    private readonly NoteAssetService _noteAssetService;
    private readonly Dictionary<string, Bitmap> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private string? _baseDirectoryPath;
    private double _availableWidth = MaxRenderWidth;

    public MarkdownImagePreviewProvider(MarkdownColorizingTransformer colorizer, NoteAssetService noteAssetService)
    {
        _colorizer = colorizer;
        _noteAssetService = noteAssetService;
    }

    public void SetBaseDirectoryPath(string? baseDirectoryPath)
    {
        _baseDirectoryPath = string.IsNullOrWhiteSpace(baseDirectoryPath)
            ? null
            : Path.GetFullPath(baseDirectoryPath);
    }

    public void SetAvailableWidth(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
        {
            return;
        }

        _availableWidth = availableWidth;
    }

    public MarkdownImagePreview? GetPreview(TextDocument document, DocumentLine line)
    {
        if (_colorizer.QueryIsFencedCodeLine(document, line.LineNumber))
        {
            return null;
        }

        var lineText = document.GetText(line.Offset, line.Length);
        var analysis = MarkdownLineParser.Analyze(lineText, MarkdownFenceState.None);
        foreach (var image in analysis.Images)
        {
            if (!image.IsStandalone)
            {
                continue;
            }

            var imagePath = lineText[image.Url.Start..image.Url.End];
            var bitmap = TryGetBitmap(imagePath);
            if (bitmap is null)
            {
                return null;
            }

            var scaledSize = ComputeScaledSize(bitmap, image.ScalePercent, _availableWidth);
            return new MarkdownImagePreview(image, bitmap, scaledSize.Width, scaledSize.Height);
        }

        return null;
    }

    public void Dispose()
    {
        foreach (var bitmap in _bitmapCache.Values)
        {
            bitmap.Dispose();
        }

        _bitmapCache.Clear();
    }

    private Size ComputeScaledSize(Bitmap bitmap, int? scalePercent, double availableWidth)
    {
        var maxWidth = Math.Max(1, Math.Min(MaxRenderWidth, availableWidth - HorizontalPadding));
        var percent = Math.Clamp(scalePercent ?? 100, 1, 400);
        var scaledWidth = bitmap.Size.Width * percent / 100d;
        var width = Math.Min(maxWidth, scaledWidth);
        var height = bitmap.Size.Width <= 0
            ? bitmap.Size.Height
            : bitmap.Size.Height * (width / bitmap.Size.Width);
        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    private Bitmap? TryGetBitmap(string imagePath)
    {
        var resolvedPath = _noteAssetService.ResolveImagePath(_baseDirectoryPath, imagePath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return null;
        }

        if (_bitmapCache.TryGetValue(resolvedPath, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        try
        {
            var bitmap = new Bitmap(resolvedPath);
            _bitmapCache[resolvedPath] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

internal readonly record struct MarkdownImagePreview(MarkdownImageMatch Image, Bitmap Bitmap, double Width, double Height);
