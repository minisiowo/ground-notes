using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaEdit.Document;

using GroundNotes.Services;

namespace GroundNotes.Editors;

internal sealed class MarkdownImagePreviewProvider : IDisposable
{
    private const double HorizontalPadding = 24;
    private const double MaxRenderWidth = 960;
    private const double ColumnGap = 12;
    private const int DefaultMaxBitmapCacheEntries = 32;

    private readonly MarkdownColorizingTransformer _colorizer;
    private readonly NoteAssetService _noteAssetService;
    private readonly Dictionary<string, BitmapCacheEntry> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _bitmapCacheLru = [];
    private readonly Dictionary<int, PreviewLineCacheEntry> _previewLineCache = [];
    private readonly Dictionary<int, PreviewRenderCacheEntry> _previewRenderCache = [];
    private readonly Dictionary<string, FileStamp> _deferredBitmapLoads = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxBitmapCacheEntries;
    private TextDocument? _document;
    private string? _baseDirectoryPath;
    private double _availableWidth = MaxRenderWidth;
    private bool _deferColdBitmapLoads;
    private bool _deferredDrainQueued;
    private int _deferredLoadGeneration;
    private bool _isDisposed;

    public MarkdownImagePreviewProvider(MarkdownColorizingTransformer colorizer, NoteAssetService noteAssetService)
        : this(colorizer, noteAssetService, DefaultMaxBitmapCacheEntries)
    {
    }

    internal MarkdownImagePreviewProvider(MarkdownColorizingTransformer colorizer, NoteAssetService noteAssetService, int maxBitmapCacheEntries)
    {
        _colorizer = colorizer;
        _noteAssetService = noteAssetService;
        _maxBitmapCacheEntries = Math.Max(1, maxBitmapCacheEntries);
    }

    public event EventHandler? DeferredBitmapLoadsCompleted;

    public void BeginDeferredColdBitmapLoads()
    {
        _deferColdBitmapLoads = true;
        _deferredLoadGeneration++;
        _deferredBitmapLoads.Clear();
        _deferredDrainQueued = false;
    }

    public void EndDeferredColdBitmapLoads()
    {
        _deferColdBitmapLoads = false;
    }

    public void SetBaseDirectoryPath(string? baseDirectoryPath)
    {
        _baseDirectoryPath = string.IsNullOrWhiteSpace(baseDirectoryPath)
            ? null
            : Path.GetFullPath(baseDirectoryPath);
        _previewLineCache.Clear();
        _previewRenderCache.Clear();
        _deferredBitmapLoads.Clear();
        _deferredLoadGeneration++;
    }

    public void SetAvailableWidth(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
        {
            return;
        }

        _availableWidth = availableWidth;
        _previewRenderCache.Clear();
    }

    public void InvalidateImage(string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(resolvedPath);
        if (_bitmapCache.TryGetValue(fullPath, out var cachedBitmap))
        {
            RemoveBitmapCacheEntry(fullPath, cachedBitmap);
        }

        foreach (var pair in _previewRenderCache.ToArray())
        {
            var previews = pair.Value.Previews;
            if (previews is not null && previews.Any(p => string.Equals(p.ResolvedPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                _previewRenderCache.Remove(pair.Key);
            }
        }
    }

    public IReadOnlyList<MarkdownImagePreview> GetPreview(TextDocument document, DocumentLine line)
    {
        MarkdownDiagnostics.RecordImagePreviewRequest();
        Attach(document);

        if (_colorizer.QueryIsFencedCodeLine(document, line.LineNumber))
        {
            _previewLineCache.Remove(line.LineNumber);
            _previewRenderCache.Remove(line.LineNumber);
            return [];
        }

        var previewLines = GetPreviewLines(document, line);
        var lineText = document.GetText(line.Offset, line.Length);
        if (_previewRenderCache.TryGetValue(line.LineNumber, out var cachedPreview)
            && string.Equals(cachedPreview.LineText, lineText, StringComparison.Ordinal)
            && string.Equals(cachedPreview.BaseDirectoryPath, _baseDirectoryPath, StringComparison.Ordinal)
            && Math.Abs(cachedPreview.AvailableWidth - _availableWidth) < double.Epsilon
            && HasMatchingFileStamps(previewLines, cachedPreview.FileStamps))
        {
            MarkdownDiagnostics.RecordImagePreviewRenderCacheHit();
            return cachedPreview.Previews ?? [];
        }

        MarkdownDiagnostics.RecordImagePreviewRenderCacheMiss();

        if (previewLines is null || previewLines.Count == 0)
        {
            _previewRenderCache[line.LineNumber] = new PreviewRenderCacheEntry(lineText, _baseDirectoryPath, _availableWidth, null, null);
            return [];
        }

        var previews = new List<MarkdownImagePreview>();
        var fileStamps = new List<FileStamp>();
        double maxWidthPerImage;
        if (previewLines.Count == 1)
        {
            maxWidthPerImage = Math.Max(1, Math.Min(MaxRenderWidth, _availableWidth - HorizontalPadding));
        }
        else
        {
            maxWidthPerImage = Math.Max(1, Math.Min(MaxRenderWidth, (_availableWidth - HorizontalPadding - (previewLines.Count - 1) * ColumnGap) / previewLines.Count));
        }

        foreach (var previewLine in previewLines)
        {
            var fileStamp = TryGetFileStamp(previewLine.ResolvedPath);
            if (fileStamp is null)
            {
                continue;
            }

            var bitmap = TryGetBitmap(previewLine.ResolvedPath, fileStamp.Value);
            if (bitmap is null)
            {
                continue;
            }

            var scaledSize = ComputeScaledSize(bitmap, previewLine.Image.ScalePercent, maxWidthPerImage);
            previews.Add(new MarkdownImagePreview(previewLine.Image, previewLine.ResolvedPath, bitmap, scaledSize.Width, scaledSize.Height));
            fileStamps.Add(fileStamp.Value);
        }

        if (previews.Count == 0)
        {
            _previewRenderCache[line.LineNumber] = new PreviewRenderCacheEntry(lineText, _baseDirectoryPath, _availableWidth, null, null);
            return [];
        }

        _previewRenderCache[line.LineNumber] = new PreviewRenderCacheEntry(lineText, _baseDirectoryPath, _availableWidth, previews, fileStamps);
        return previews;
    }

    private Size ComputeScaledSize(Bitmap bitmap, int? scalePercent, double maxWidth)
    {
        var percent = Math.Clamp(scalePercent ?? 100, 1, 400);
        var scaledWidth = bitmap.Size.Width * percent / 100d;
        var width = Math.Min(maxWidth, scaledWidth);
        var height = bitmap.Size.Width <= 0
            ? bitmap.Size.Height
            : bitmap.Size.Height * (width / bitmap.Size.Width);
        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    private IReadOnlyList<PreviewLine>? GetPreviewLines(TextDocument document, DocumentLine line)
    {
        var lineText = document.GetText(line.Offset, line.Length);
        if (_previewLineCache.TryGetValue(line.LineNumber, out var cachedEntry)
            && string.Equals(cachedEntry.LineText, lineText, StringComparison.Ordinal)
            && string.Equals(cachedEntry.BaseDirectoryPath, _baseDirectoryPath, StringComparison.Ordinal))
        {
            MarkdownDiagnostics.RecordImagePreviewCacheHit();
            return cachedEntry.PreviewLines;
        }

        MarkdownDiagnostics.RecordImagePreviewCacheMiss();

        var analysis = MarkdownLineParser.Analyze(lineText, MarkdownFenceState.None);
        List<PreviewLine>? previewLines = null;

        if (analysis.IsColumnLayout && analysis.ColumnImages.Count > 0)
        {
            previewLines = [];
            foreach (var image in analysis.ColumnImages)
            {
                var imagePath = lineText[image.Url.Start..image.Url.End];
                var resolvedPath = _noteAssetService.ResolveImagePath(_baseDirectoryPath, imagePath);
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    continue;
                }

                previewLines.Add(new PreviewLine(image, resolvedPath));
            }
        }
        else
        {
            foreach (var image in analysis.Images)
            {
                if (!image.IsStandalone)
                {
                    continue;
                }

                var imagePath = lineText[image.Url.Start..image.Url.End];
                var resolvedPath = _noteAssetService.ResolveImagePath(_baseDirectoryPath, imagePath);
                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    break;
                }

                previewLines = [new PreviewLine(image, resolvedPath)];
                break;
            }
        }

        _previewLineCache[line.LineNumber] = new PreviewLineCacheEntry(lineText, _baseDirectoryPath, previewLines);
        return previewLines;
    }

    private Bitmap? TryGetBitmap(string resolvedPath, FileStamp fileStamp)
    {
        if (_bitmapCache.TryGetValue(resolvedPath, out var cachedBitmap))
        {
            if (cachedBitmap.FileStamp.Equals(fileStamp))
            {
                TouchBitmapCacheEntry(cachedBitmap.LruNode);
                MarkdownDiagnostics.RecordBitmapCacheHit();
                return cachedBitmap.Bitmap;
            }

            RemoveBitmapCacheEntry(resolvedPath, cachedBitmap);
        }

        if (_deferColdBitmapLoads)
        {
            _deferredBitmapLoads[resolvedPath] = fileStamp;
            MarkdownDiagnostics.RecordDeferredBitmapLoadRequest();
            RequestDeferredBitmapLoadDrain();
            return null;
        }

        MarkdownDiagnostics.RecordBitmapCacheMiss();
        return TryLoadBitmapIntoCache(resolvedPath, fileStamp);
    }

    private Bitmap? TryLoadBitmapIntoCache(string resolvedPath, FileStamp fileStamp)
    {
        try
        {
            var bitmap = new Bitmap(resolvedPath);
            var lruNode = _bitmapCacheLru.AddLast(resolvedPath);
            _bitmapCache[resolvedPath] = new BitmapCacheEntry(bitmap, fileStamp, lruNode);
            TrimBitmapCache();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void RequestDeferredBitmapLoadDrain()
    {
        if (_deferredDrainQueued)
        {
            return;
        }

        _deferredDrainQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _deferredDrainQueued = false;
            if (_isDisposed)
            {
                return;
            }

            ProcessDeferredBitmapLoads();
        }, DispatcherPriority.Background);
    }

    private void ProcessDeferredBitmapLoads()
    {
        var generation = _deferredLoadGeneration;
        var loads = _deferredBitmapLoads.ToArray();
        _deferredBitmapLoads.Clear();

        bool anyLoaded = false;
        foreach (var (resolvedPath, fileStamp) in loads)
        {
            if (generation != _deferredLoadGeneration)
            {
                MarkdownDiagnostics.RecordDeferredBitmapLoadSkip();
                continue;
            }

            if (_bitmapCache.ContainsKey(resolvedPath))
            {
                MarkdownDiagnostics.RecordDeferredBitmapLoadSkip();
                continue;
            }

            var currentStamp = TryGetFileStamp(resolvedPath);
            if (currentStamp is null || !currentStamp.Value.Equals(fileStamp))
            {
                MarkdownDiagnostics.RecordDeferredBitmapLoadSkip();
                continue;
            }

            if (TryLoadBitmapIntoCache(resolvedPath, fileStamp) is not null)
            {
                anyLoaded = true;
                MarkdownDiagnostics.RecordDeferredBitmapLoad();
            }
        }

        if (anyLoaded)
        {
            DeferredBitmapLoadsCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Attach(TextDocument document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        Detach();
        _document = document;
        _document.Changed += OnDocumentChanged;
        _previewLineCache.Clear();
        _previewRenderCache.Clear();
    }

    private void Detach()
    {
        if (_document is null)
        {
            return;
        }

        _document.Changed -= OnDocumentChanged;
        _document = null;
        _previewLineCache.Clear();
        _previewRenderCache.Clear();
        _deferredBitmapLoads.Clear();
        _deferredLoadGeneration++;
    }

    public void Dispose()
    {
        _isDisposed = true;
        Detach();

        foreach (var bitmap in _bitmapCache.Values)
        {
            bitmap.Bitmap.Dispose();
        }

        _bitmapCache.Clear();
        _bitmapCacheLru.Clear();
        _previewLineCache.Clear();
        _previewRenderCache.Clear();
        _deferredBitmapLoads.Clear();
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_document is null)
        {
            _previewLineCache.Clear();
            _previewRenderCache.Clear();
            return;
        }

        var changedLine = _document.GetLineByOffset(Math.Min(e.Offset, _document.TextLength)).LineNumber;
        var keysToRemove = _previewLineCache.Keys.Where(key => key >= changedLine).ToArray();
        foreach (var key in keysToRemove)
        {
            _previewLineCache.Remove(key);
        }

        keysToRemove = _previewRenderCache.Keys.Where(key => key >= changedLine).ToArray();
        foreach (var key in keysToRemove)
        {
            _previewRenderCache.Remove(key);
        }
    }

    private readonly record struct PreviewLine(MarkdownImageMatch Image, string ResolvedPath);

    private readonly record struct PreviewLineCacheEntry(string LineText, string? BaseDirectoryPath, IReadOnlyList<PreviewLine>? PreviewLines);

    private readonly record struct PreviewRenderCacheEntry(string LineText, string? BaseDirectoryPath, double AvailableWidth, IReadOnlyList<MarkdownImagePreview>? Previews, IReadOnlyList<FileStamp>? FileStamps);

    private sealed record BitmapCacheEntry(Bitmap Bitmap, FileStamp FileStamp, LinkedListNode<string> LruNode);

    private readonly record struct FileStamp(DateTime LastWriteTimeUtc, long Length);

    private static FileStamp? TryGetFileStamp(string resolvedPath)
    {
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        var fileInfo = new FileInfo(resolvedPath);
        return new FileStamp(fileInfo.LastWriteTimeUtc, fileInfo.Length);
    }

    private static bool HasMatchingFileStamps(IReadOnlyList<PreviewLine>? previewLines, IReadOnlyList<FileStamp>? expectedStamps)
    {
        if (previewLines is null || expectedStamps is null || previewLines.Count != expectedStamps.Count)
        {
            return false;
        }

        for (var i = 0; i < previewLines.Count; i++)
        {
            if (!HasMatchingFileStamp(previewLines[i].ResolvedPath, expectedStamps[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasMatchingFileStamp(string? resolvedPath, FileStamp? expectedStamp)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath) || expectedStamp is null)
        {
            return false;
        }

        var currentStamp = TryGetFileStamp(resolvedPath);
        return currentStamp is not null && currentStamp.Value.Equals(expectedStamp.Value);
    }

    private void TouchBitmapCacheEntry(LinkedListNode<string> lruNode)
    {
        if (lruNode.List is null || ReferenceEquals(_bitmapCacheLru.Last, lruNode))
        {
            return;
        }

        _bitmapCacheLru.Remove(lruNode);
        _bitmapCacheLru.AddLast(lruNode);
    }

    private void TrimBitmapCache()
    {
        while (_bitmapCache.Count > _maxBitmapCacheEntries && _bitmapCacheLru.First is not null)
        {
            var oldestPath = _bitmapCacheLru.First.Value;
            if (_bitmapCache.Remove(oldestPath, out var oldestEntry))
            {
                _bitmapCacheLru.Remove(oldestEntry.LruNode);
                oldestEntry.Bitmap.Dispose();
                MarkdownDiagnostics.RecordBitmapCacheEviction();
                continue;
            }

            _bitmapCacheLru.RemoveFirst();
        }
    }

    private void RemoveBitmapCacheEntry(string resolvedPath, BitmapCacheEntry cacheEntry)
    {
        cacheEntry.Bitmap.Dispose();
        _bitmapCache.Remove(resolvedPath);
        if (cacheEntry.LruNode.List is not null)
        {
            _bitmapCacheLru.Remove(cacheEntry.LruNode);
        }
    }
}

internal readonly record struct MarkdownImagePreview(MarkdownImageMatch Image, string ResolvedPath, Bitmap Bitmap, double Width, double Height);
