namespace GroundNotes.Services.KeySequences;

public enum KeySequenceResolutionKind
{
    NoMatch,
    Prefix,
    Command
}

public sealed record KeySequenceContinuation(
    KeyStroke KeyStroke,
    KeySequenceResolutionKind Kind,
    string? ActionId,
    string? Description)
{
    public string Display => KeyStroke.Display;
}

public sealed class KeySequenceResolution
{
    internal KeySequenceResolution(
        KeySequenceResolutionKind kind,
        string? actionId,
        string? description,
        IEnumerable<KeyStroke> breadcrumb,
        IEnumerable<KeySequenceContinuation> continuations)
    {
        Kind = kind;
        ActionId = actionId;
        Description = description;
        Breadcrumb = Array.AsReadOnly(breadcrumb.ToArray());
        Continuations = Array.AsReadOnly(continuations.ToArray());
    }

    public KeySequenceResolutionKind Kind { get; }

    public string? ActionId { get; }

    public string? Description { get; }

    public IReadOnlyList<KeyStroke> Breadcrumb { get; }

    public string DisplayBreadcrumb => string.Join(" › ", Breadcrumb.Select(stroke => stroke.Display));

    public IReadOnlyList<KeySequenceContinuation> Continuations { get; }
}
