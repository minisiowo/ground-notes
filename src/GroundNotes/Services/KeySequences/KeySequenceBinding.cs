namespace GroundNotes.Services.KeySequences;

public sealed class KeySequenceBinding
{
    public KeySequenceBinding(
        string actionId,
        IEnumerable<KeyStroke> sequence,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(sequence);

        var strokes = sequence.ToArray();
        if (strokes.Length == 0)
        {
            throw new ArgumentException("A key sequence must contain at least one key stroke.", nameof(sequence));
        }

        if (strokes.Any(stroke => !stroke.IsValid))
        {
            throw new ArgumentException("A key sequence cannot contain an uninitialized key stroke.", nameof(sequence));
        }

        ActionId = actionId.Trim();
        Sequence = Array.AsReadOnly(strokes);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public string ActionId { get; }

    public IReadOnlyList<KeyStroke> Sequence { get; }

    public string? Description { get; }

    public string DisplaySequence => string.Join(" › ", Sequence.Select(stroke => stroke.Display));
}
