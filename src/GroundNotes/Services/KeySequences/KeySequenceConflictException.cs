namespace GroundNotes.Services.KeySequences;

public enum KeySequenceConflictKind
{
    Duplicate,
    ExistingSequenceIsPrefix,
    NewSequenceIsPrefix
}

public sealed class KeySequenceConflictException : ArgumentException
{
    internal KeySequenceConflictException(
        KeySequenceConflictKind conflictKind,
        KeySequenceBinding incomingBinding,
        KeySequenceBinding existingBinding)
        : base(CreateMessage(conflictKind, incomingBinding, existingBinding), "bindings")
    {
        ConflictKind = conflictKind;
        IncomingBinding = incomingBinding;
        ExistingBinding = existingBinding;
    }

    public KeySequenceConflictKind ConflictKind { get; }

    public KeySequenceBinding IncomingBinding { get; }

    public KeySequenceBinding ExistingBinding { get; }

    private static string CreateMessage(
        KeySequenceConflictKind conflictKind,
        KeySequenceBinding incomingBinding,
        KeySequenceBinding existingBinding)
    {
        var reason = conflictKind switch
        {
            KeySequenceConflictKind.Duplicate => "duplicates",
            KeySequenceConflictKind.ExistingSequenceIsPrefix => "extends prefix",
            KeySequenceConflictKind.NewSequenceIsPrefix => "is a prefix of",
            _ => "conflicts with"
        };

        return $"Key sequence '{incomingBinding.DisplaySequence}' for '{incomingBinding.ActionId}' " +
               $"{reason} '{existingBinding.DisplaySequence}' for '{existingBinding.ActionId}'.";
    }
}
