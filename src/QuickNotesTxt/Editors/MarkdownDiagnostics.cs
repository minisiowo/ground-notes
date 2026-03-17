namespace QuickNotesTxt.Editors;

internal static class MarkdownDiagnostics
{
    private static int _linesAnalyzed;
    private static int _analysisCacheHits;
    private static int _analysisCacheMisses;
    private static int _fenceCacheHits;
    private static int _fenceCacheMisses;
    private static int _fenceInvalidations;

    public static MarkdownDiagnosticsSnapshot Snapshot()
        => new(
            _linesAnalyzed,
            _analysisCacheHits,
            _analysisCacheMisses,
            _fenceCacheHits,
            _fenceCacheMisses,
            _fenceInvalidations);

    public static void Reset()
    {
        _linesAnalyzed = 0;
        _analysisCacheHits = 0;
        _analysisCacheMisses = 0;
        _fenceCacheHits = 0;
        _fenceCacheMisses = 0;
        _fenceInvalidations = 0;
    }

    public static void RecordLineAnalyzed() => Interlocked.Increment(ref _linesAnalyzed);

    public static void RecordAnalysisCacheHit() => Interlocked.Increment(ref _analysisCacheHits);

    public static void RecordAnalysisCacheMiss() => Interlocked.Increment(ref _analysisCacheMisses);

    public static void RecordFenceCacheHit() => Interlocked.Increment(ref _fenceCacheHits);

    public static void RecordFenceCacheMiss() => Interlocked.Increment(ref _fenceCacheMisses);

    public static void RecordFenceInvalidation() => Interlocked.Increment(ref _fenceInvalidations);
}

internal readonly record struct MarkdownDiagnosticsSnapshot(
    int LinesAnalyzed,
    int AnalysisCacheHits,
    int AnalysisCacheMisses,
    int FenceCacheHits,
    int FenceCacheMisses,
    int FenceInvalidations);
