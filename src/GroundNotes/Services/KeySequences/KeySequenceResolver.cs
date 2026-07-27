namespace GroundNotes.Services.KeySequences;

/// <summary>
/// Deterministically resolves key strokes against a trie of command bindings.
/// </summary>
public sealed class KeySequenceResolver
{
    private readonly Node _root = new();
    private readonly List<KeyStroke> _breadcrumb = [];
    private readonly List<Node> _nodePath;
    private Node _current;

    public KeySequenceResolver(IEnumerable<KeySequenceBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        foreach (var binding in bindings)
        {
            ArgumentNullException.ThrowIfNull(binding);
            Add(binding);
        }

        _current = _root;
        _nodePath = [_root];
    }

    public bool IsPending => _breadcrumb.Count > 0;

    public IReadOnlyList<KeyStroke> CurrentBreadcrumb => Array.AsReadOnly(_breadcrumb.ToArray());

    public string DisplayBreadcrumb => FormatBreadcrumb(_breadcrumb);

    public IReadOnlyList<KeySequenceContinuation> Continuations => GetContinuations(_current);

    public KeySequenceResolution Resolve(KeyStroke keyStroke)
    {
        if (!keyStroke.IsValid)
        {
            throw new ArgumentException("The key stroke must be initialized.", nameof(keyStroke));
        }

        if (!_current.Children.TryGetValue(keyStroke, out var next))
        {
            var attemptedBreadcrumb = _breadcrumb.Append(keyStroke).ToArray();
            Reset();
            return CreateResolution(
                KeySequenceResolutionKind.NoMatch,
                binding: null,
                attemptedBreadcrumb,
                []);
        }

        _current = next;
        _breadcrumb.Add(keyStroke);
        _nodePath.Add(next);

        if (next.Binding is not null)
        {
            var completedBreadcrumb = _breadcrumb.ToArray();
            var binding = next.Binding;
            Reset();
            return CreateResolution(
                KeySequenceResolutionKind.Command,
                binding,
                completedBreadcrumb,
                []);
        }

        return CreatePrefixResolution();
    }

    public KeySequenceResolution Backspace()
    {
        if (_breadcrumb.Count == 0)
        {
            return CreateResolution(KeySequenceResolutionKind.NoMatch, null, [], []);
        }

        _breadcrumb.RemoveAt(_breadcrumb.Count - 1);
        _nodePath.RemoveAt(_nodePath.Count - 1);
        _current = _nodePath[^1];

        return _breadcrumb.Count == 0
            ? CreateResolution(KeySequenceResolutionKind.NoMatch, null, [], [])
            : CreatePrefixResolution();
    }

    public void Reset()
    {
        _breadcrumb.Clear();
        _nodePath.Clear();
        _nodePath.Add(_root);
        _current = _root;
    }

    private void Add(KeySequenceBinding binding)
    {
        var node = _root;

        foreach (var keyStroke in binding.Sequence)
        {
            if (node.Binding is not null)
            {
                throw new KeySequenceConflictException(
                    KeySequenceConflictKind.ExistingSequenceIsPrefix,
                    binding,
                    node.Binding);
            }

            if (!node.Children.TryGetValue(keyStroke, out var child))
            {
                child = new Node();
                node.Children.Add(keyStroke, child);
            }

            node = child;
        }

        if (node.Binding is not null)
        {
            throw new KeySequenceConflictException(
                KeySequenceConflictKind.Duplicate,
                binding,
                node.Binding);
        }

        if (node.Children.Count > 0)
        {
            var descendant = FindFirstBinding(node)
                ?? throw new InvalidOperationException("A populated key-sequence branch has no command.");
            throw new KeySequenceConflictException(
                KeySequenceConflictKind.NewSequenceIsPrefix,
                binding,
                descendant);
        }

        node.Binding = binding;
    }

    private KeySequenceResolution CreatePrefixResolution()
    {
        return CreateResolution(
            KeySequenceResolutionKind.Prefix,
            binding: null,
            _breadcrumb,
            GetContinuations(_current));
    }

    private static KeySequenceResolution CreateResolution(
        KeySequenceResolutionKind kind,
        KeySequenceBinding? binding,
        IEnumerable<KeyStroke> breadcrumb,
        IEnumerable<KeySequenceContinuation> continuations)
    {
        return new KeySequenceResolution(
            kind,
            binding?.ActionId,
            binding?.Description,
            breadcrumb,
            continuations);
    }

    private static IReadOnlyList<KeySequenceContinuation> GetContinuations(Node node)
    {
        return node.Children
            .Select(pair => new KeySequenceContinuation(
                pair.Key,
                pair.Value.Binding is null
                    ? KeySequenceResolutionKind.Prefix
                    : KeySequenceResolutionKind.Command,
                pair.Value.Binding?.ActionId,
                pair.Value.Binding?.Description))
            .OrderBy(continuation => continuation.Display, StringComparer.Ordinal)
            .ToArray();
    }

    private static KeySequenceBinding? FindFirstBinding(Node node)
    {
        if (node.Binding is not null)
        {
            return node.Binding;
        }

        foreach (var child in node.Children.Values)
        {
            var binding = FindFirstBinding(child);
            if (binding is not null)
            {
                return binding;
            }
        }

        return null;
    }

    private static string FormatBreadcrumb(IEnumerable<KeyStroke> breadcrumb)
    {
        return string.Join(" › ", breadcrumb.Select(stroke => stroke.Display));
    }

    private sealed class Node
    {
        public Dictionary<KeyStroke, Node> Children { get; } = [];

        public KeySequenceBinding? Binding { get; set; }
    }
}
