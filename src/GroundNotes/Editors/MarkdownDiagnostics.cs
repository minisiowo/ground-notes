namespace GroundNotes.Editors;

internal static class MarkdownDiagnostics
{
    private static int _linesAnalyzed;
    private static int _analysisCacheHits;
    private static int _analysisCacheMisses;
    private static int _fenceCacheHits;
    private static int _fenceCacheMisses;
    private static int _fenceInvalidations;
    private static int _imagePreviewRequests;
    private static int _imagePreviewCacheHits;
    private static int _imagePreviewCacheMisses;
    private static int _imagePreviewRenderCacheHits;
    private static int _imagePreviewRenderCacheMisses;
    private static int _bitmapCacheHits;
    private static int _bitmapCacheMisses;
    private static int _bitmapCacheEvictions;
    private static int _previewLayerRefreshes;
    private static int _previewLayerRefreshSkips;
    private static int _previewLayerRefreshRequests;
    private static int _previewLayerRefreshPosts;
    private static int _previewLayerLineStateReuses;
    private static int _previewLayerDraws;
    private static int _previewLayerDrawnPreviews;

    public static MarkdownDiagnosticsSnapshot Snapshot()
        => new(
            _linesAnalyzed,
            _analysisCacheHits,
            _analysisCacheMisses,
            _fenceCacheHits,
            _fenceCacheMisses,
            _fenceInvalidations,
            _imagePreviewRequests,
            _imagePreviewCacheHits,
            _imagePreviewCacheMisses,
            _imagePreviewRenderCacheHits,
            _imagePreviewRenderCacheMisses,
            _bitmapCacheHits,
            _bitmapCacheMisses,
            _bitmapCacheEvictions,
            _previewLayerRefreshes,
            _previewLayerRefreshSkips,
            _previewLayerRefreshRequests,
            _previewLayerRefreshPosts,
            _previewLayerLineStateReuses,
            _previewLayerDraws,
            _previewLayerDrawnPreviews);

    public static void Reset()
    {
        _linesAnalyzed = 0;
        _analysisCacheHits = 0;
        _analysisCacheMisses = 0;
        _fenceCacheHits = 0;
        _fenceCacheMisses = 0;
        _fenceInvalidations = 0;
        _imagePreviewRequests = 0;
        _imagePreviewCacheHits = 0;
        _imagePreviewCacheMisses = 0;
        _imagePreviewRenderCacheHits = 0;
        _imagePreviewRenderCacheMisses = 0;
        _bitmapCacheHits = 0;
        _bitmapCacheMisses = 0;
        _bitmapCacheEvictions = 0;
        _previewLayerRefreshes = 0;
        _previewLayerRefreshSkips = 0;
        _previewLayerRefreshRequests = 0;
        _previewLayerRefreshPosts = 0;
        _previewLayerLineStateReuses = 0;
        _previewLayerDraws = 0;
        _previewLayerDrawnPreviews = 0;
    }

    public static void RecordLineAnalyzed() => Interlocked.Increment(ref _linesAnalyzed);

    public static void RecordAnalysisCacheHit() => Interlocked.Increment(ref _analysisCacheHits);

    public static void RecordAnalysisCacheMiss() => Interlocked.Increment(ref _analysisCacheMisses);

    public static void RecordFenceCacheHit() => Interlocked.Increment(ref _fenceCacheHits);

    public static void RecordFenceCacheMiss() => Interlocked.Increment(ref _fenceCacheMisses);

    public static void RecordFenceInvalidation() => Interlocked.Increment(ref _fenceInvalidations);

    public static void RecordImagePreviewRequest() => Interlocked.Increment(ref _imagePreviewRequests);

    public static void RecordImagePreviewCacheHit() => Interlocked.Increment(ref _imagePreviewCacheHits);

    public static void RecordImagePreviewCacheMiss() => Interlocked.Increment(ref _imagePreviewCacheMisses);

    public static void RecordImagePreviewRenderCacheHit() => Interlocked.Increment(ref _imagePreviewRenderCacheHits);

    public static void RecordImagePreviewRenderCacheMiss() => Interlocked.Increment(ref _imagePreviewRenderCacheMisses);

    public static void RecordBitmapCacheHit() => Interlocked.Increment(ref _bitmapCacheHits);

    public static void RecordBitmapCacheMiss() => Interlocked.Increment(ref _bitmapCacheMisses);

    public static void RecordBitmapCacheEviction() => Interlocked.Increment(ref _bitmapCacheEvictions);

    public static void RecordPreviewLayerRefresh() => Interlocked.Increment(ref _previewLayerRefreshes);

    public static void RecordPreviewLayerRefreshSkip() => Interlocked.Increment(ref _previewLayerRefreshSkips);

    public static void RecordPreviewLayerRefreshRequest() => Interlocked.Increment(ref _previewLayerRefreshRequests);

    public static void RecordPreviewLayerRefreshPost() => Interlocked.Increment(ref _previewLayerRefreshPosts);

    public static void RecordPreviewLayerLineStateReuse() => Interlocked.Increment(ref _previewLayerLineStateReuses);

    public static void RecordPreviewLayerDraw() => Interlocked.Increment(ref _previewLayerDraws);

    public static void RecordPreviewLayerDrawnPreview() => Interlocked.Increment(ref _previewLayerDrawnPreviews);
}

internal readonly record struct MarkdownDiagnosticsSnapshot(
    int LinesAnalyzed,
    int AnalysisCacheHits,
    int AnalysisCacheMisses,
    int FenceCacheHits,
    int FenceCacheMisses,
    int FenceInvalidations,
    int ImagePreviewRequests,
    int ImagePreviewCacheHits,
    int ImagePreviewCacheMisses,
    int ImagePreviewRenderCacheHits,
    int ImagePreviewRenderCacheMisses,
    int BitmapCacheHits,
    int BitmapCacheMisses,
    int BitmapCacheEvictions,
    int PreviewLayerRefreshes,
    int PreviewLayerRefreshSkips,
    int PreviewLayerRefreshRequests,
    int PreviewLayerRefreshPosts,
    int PreviewLayerLineStateReuses,
    int PreviewLayerDraws,
    int PreviewLayerDrawnPreviews);
