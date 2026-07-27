using GroundNotes.Services.KeySequences;
using GroundNotes.Services.KeySequences.Defaults;
using Xunit;

namespace GroundNotes.Tests;

public sealed class KeySequenceResolverTests
{
    [Fact]
    public void Resolve_ValidSequenceReturnsPrefixesThenCommandAndResetsState()
    {
        var resolver = new KeySequenceResolver(
        [
            Binding("notes.find", "space", "f", "f", description: "Find note"),
            Binding("notes.search", "space", "f", "s", description: "Search notes")
        ]);

        var leader = resolver.Resolve(Key("space"));
        var group = resolver.Resolve(Key("f"));
        var command = resolver.Resolve(Key("f"));

        Assert.Equal(KeySequenceResolutionKind.Prefix, leader.Kind);
        Assert.Equal("Space", leader.DisplayBreadcrumb);
        var fGroup = Assert.Single(leader.Continuations);
        Assert.Equal("f", fGroup.Display);
        Assert.Equal(KeySequenceResolutionKind.Prefix, fGroup.Kind);

        Assert.Equal(KeySequenceResolutionKind.Prefix, group.Kind);
        Assert.Equal("Space › f", group.DisplayBreadcrumb);
        Assert.Collection(
            group.Continuations,
            continuation =>
            {
                Assert.Equal("f", continuation.Display);
                Assert.Equal(KeySequenceResolutionKind.Command, continuation.Kind);
                Assert.Equal("notes.find", continuation.ActionId);
                Assert.Equal("Find note", continuation.Description);
            },
            continuation =>
            {
                Assert.Equal("s", continuation.Display);
                Assert.Equal(KeySequenceResolutionKind.Command, continuation.Kind);
                Assert.Equal("notes.search", continuation.ActionId);
            });

        Assert.Equal(KeySequenceResolutionKind.Command, command.Kind);
        Assert.Equal("notes.find", command.ActionId);
        Assert.Equal("Find note", command.Description);
        Assert.Equal("Space › f › f", command.DisplayBreadcrumb);
        Assert.Empty(command.Continuations);
        Assert.False(resolver.IsPending);
        Assert.Empty(resolver.CurrentBreadcrumb);
    }

    [Fact]
    public void Resolve_NoMatchReturnsAttemptedBreadcrumbAndResetsState()
    {
        var resolver = new KeySequenceResolver([Binding("notes.find", "space", "f")]);
        resolver.Resolve(Key("space"));

        var noMatch = resolver.Resolve(Key("x"));

        Assert.Equal(KeySequenceResolutionKind.NoMatch, noMatch.Kind);
        Assert.Equal("Space › x", noMatch.DisplayBreadcrumb);
        Assert.Null(noMatch.ActionId);
        Assert.Empty(noMatch.Continuations);
        Assert.False(resolver.IsPending);

        Assert.Equal(KeySequenceResolutionKind.Prefix, resolver.Resolve(Key("space")).Kind);
    }

    [Fact]
    public void Reset_ClearsPendingSequenceAndRestoresRootContinuations()
    {
        var resolver = new KeySequenceResolver(
        [
            Binding("space.command", "space", "f"),
            Binding("control.command", "k", modifiers: KeyStrokeModifiers.Control)
        ]);
        resolver.Resolve(Key("space"));

        resolver.Reset();

        Assert.False(resolver.IsPending);
        Assert.Equal(string.Empty, resolver.DisplayBreadcrumb);
        Assert.Empty(resolver.CurrentBreadcrumb);
        Assert.Equal(2, resolver.Continuations.Count);
        Assert.Contains(resolver.Continuations, item => item.Display == "Space");
        Assert.Contains(resolver.Continuations, item => item.Display == "Ctrl+k");
    }

    [Fact]
    public void Backspace_RemovesLastStrokeAndReturnsPreviousPrefixState()
    {
        var resolver = new KeySequenceResolver(
        [
            Binding("notes.find", "space", "f", "f"),
            Binding("notes.new", "space", "n", "n")
        ]);
        resolver.Resolve(Key("space"));
        resolver.Resolve(Key("f"));

        var previous = resolver.Backspace();
        var cleared = resolver.Backspace();

        Assert.Equal(KeySequenceResolutionKind.Prefix, previous.Kind);
        Assert.Equal("Space", previous.DisplayBreadcrumb);
        Assert.Equal(2, previous.Continuations.Count);
        Assert.Contains(previous.Continuations, item => item.Display == "f");
        Assert.Contains(previous.Continuations, item => item.Display == "n");

        Assert.Equal(KeySequenceResolutionKind.NoMatch, cleared.Kind);
        Assert.Empty(cleared.Breadcrumb);
        Assert.False(resolver.IsPending);
        Assert.Equal(KeySequenceResolutionKind.NoMatch, resolver.Backspace().Kind);
    }

    [Fact]
    public void Constructor_ExistingCommandPrefixThrowsConflict()
    {
        var shortBinding = Binding("short", "space", "f");
        var longBinding = Binding("long", "space", "f", "f");

        var exception = Assert.Throws<KeySequenceConflictException>(
            () => new KeySequenceResolver([shortBinding, longBinding]));

        Assert.Equal(KeySequenceConflictKind.ExistingSequenceIsPrefix, exception.ConflictKind);
        Assert.Same(longBinding, exception.IncomingBinding);
        Assert.Same(shortBinding, exception.ExistingBinding);
    }

    [Fact]
    public void Constructor_NewCommandPrefixThrowsConflictRegardlessOfBindingOrder()
    {
        var longBinding = Binding("long", "space", "f", "f");
        var shortBinding = Binding("short", "space", "f");

        var exception = Assert.Throws<KeySequenceConflictException>(
            () => new KeySequenceResolver([longBinding, shortBinding]));

        Assert.Equal(KeySequenceConflictKind.NewSequenceIsPrefix, exception.ConflictKind);
        Assert.Same(shortBinding, exception.IncomingBinding);
        Assert.Same(longBinding, exception.ExistingBinding);
    }

    [Fact]
    public void Constructor_DuplicateSequenceThrowsConflict()
    {
        var first = Binding("first", "space", "f");
        var second = Binding("second", "SPACE", "F");

        var exception = Assert.Throws<KeySequenceConflictException>(
            () => new KeySequenceResolver([first, second]));

        Assert.Equal(KeySequenceConflictKind.Duplicate, exception.ConflictKind);
        Assert.Same(second, exception.IncomingBinding);
        Assert.Same(first, exception.ExistingBinding);
    }

    [Fact]
    public void KeyStroke_NormalizesLogicalKeyAndKeepsModifiersDistinct()
    {
        Assert.Equal(Key("f"), Key(" F "));
        Assert.Equal("Space", KeyStroke.FromCharacter(' ').Display);

        var resolver = new KeySequenceResolver(
        [
            new KeySequenceBinding("plain", [Key("k")]),
            new KeySequenceBinding("modified", [Key("k", KeyStrokeModifiers.Control | KeyStrokeModifiers.Shift)])
        ]);

        var modified = resolver.Resolve(Key("K", KeyStrokeModifiers.Control | KeyStrokeModifiers.Shift));

        Assert.Equal(KeySequenceResolutionKind.Command, modified.Kind);
        Assert.Equal("modified", modified.ActionId);
        Assert.Equal("Ctrl+Shift+k", modified.DisplayBreadcrumb);
    }

    [Fact]
    public void GroundNotesDefaultMap_ContainsAndResolvesExpectedLeaderSequences()
    {
        var expected = new Dictionary<string, string>
        {
            ["space f f"] = GroundNotesKeySequenceActionIds.OpenNotePicker,
            ["space f s"] = GroundNotesKeySequenceActionIds.SearchNotes,
            ["space f l"] = GroundNotesKeySequenceActionIds.FindLinks,
            ["space n n"] = GroundNotesKeySequenceActionIds.NewNote,
            ["space n w"] = GroundNotesKeySequenceActionIds.NewNoteWindow,
            ["space n d"] = GroundNotesKeySequenceActionIds.DeleteNote,
            ["space w w"] = GroundNotesKeySequenceActionIds.FocusNextPane,
            ["space w h"] = GroundNotesKeySequenceActionIds.FocusPaneLeft,
            ["space w l"] = GroundNotesKeySequenceActionIds.FocusPaneRight,
            ["space w c"] = GroundNotesKeySequenceActionIds.ClosePane,
            ["space w ="] = GroundNotesKeySequenceActionIds.EqualizePanes,
            ["space g e"] = GroundNotesKeySequenceActionIds.FocusEditor,
            ["space g t"] = GroundNotesKeySequenceActionIds.FocusTitle,
            ["space g m"] = GroundNotesKeySequenceActionIds.FocusMetadata,
            ["space g s"] = GroundNotesKeySequenceActionIds.FocusSidebar,
            ["space v s"] = GroundNotesKeySequenceActionIds.ToggleSidebar,
            ["space v z"] = GroundNotesKeySequenceActionIds.ToggleZenMode,
            ["space v y"] = GroundNotesKeySequenceActionIds.ToggleYaml,
            ["space v r"] = GroundNotesKeySequenceActionIds.ReloadNotes,
            ["space a c"] = GroundNotesKeySequenceActionIds.OpenAiChat,
            ["space a t"] = GroundNotesKeySequenceActionIds.GenerateTitleSuggestions,
            ["space ,"] = GroundNotesKeySequenceActionIds.OpenSettings,
            ["space ?"] = GroundNotesKeySequenceActionIds.ShowShortcuts
        };
        var resolver = GroundNotesKeySequenceMap.CreateResolver();

        Assert.Equal(expected.Count + 5, GroundNotesKeySequenceMap.Bindings.Count);
        foreach (var (sequence, actionId) in expected)
        {
            KeySequenceResolution? resolution = null;
            foreach (var key in sequence.Split(' '))
            {
                resolution = resolver.Resolve(Key(key));
            }

            Assert.NotNull(resolution);
            Assert.Equal(KeySequenceResolutionKind.Command, resolution.Kind);
            Assert.Equal(actionId, resolution.ActionId);
        }

        var ctrlWCommands = new Dictionary<string, string>
        {
            ["h"] = GroundNotesKeySequenceActionIds.FocusPaneLeft,
            ["l"] = GroundNotesKeySequenceActionIds.FocusPaneRight,
            ["w"] = GroundNotesKeySequenceActionIds.FocusNextPane,
            ["c"] = GroundNotesKeySequenceActionIds.ClosePane,
            ["="] = GroundNotesKeySequenceActionIds.EqualizePanes
        };
        foreach (var (key, actionId) in ctrlWCommands)
        {
            Assert.Equal(
                KeySequenceResolutionKind.Prefix,
                resolver.Resolve(new KeyStroke("w", KeyStrokeModifiers.Control)).Kind);
            var resolution = resolver.Resolve(new KeyStroke(key));
            Assert.Equal(KeySequenceResolutionKind.Command, resolution.Kind);
            Assert.Equal(actionId, resolution.ActionId);
        }
    }

    private static KeySequenceBinding Binding(
        string actionId,
        string firstKey,
        string? secondKey = null,
        string? thirdKey = null,
        string? description = null,
        KeyStrokeModifiers modifiers = KeyStrokeModifiers.None)
    {
        var keys = new[] { firstKey, secondKey, thirdKey }
            .Where(key => key is not null)
            .Select(key => Key(key!, modifiers));
        return new KeySequenceBinding(actionId, keys, description);
    }

    private static KeyStroke Key(
        string key,
        KeyStrokeModifiers modifiers = KeyStrokeModifiers.None)
    {
        return new KeyStroke(key, modifiers);
    }
}
