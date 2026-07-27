namespace GroundNotes.Services.KeySequences.Defaults;

public static class GroundNotesKeySequenceMap
{
    private static readonly IReadOnlyList<KeySequenceBinding> s_bindings = Array.AsReadOnly(
        new KeySequenceBinding[]
        {
            Bind(GroundNotesKeySequenceActionIds.OpenNotePicker, "Find note", "space", "f", "f"),
            Bind(GroundNotesKeySequenceActionIds.SearchNotes, "Search notes", "space", "f", "s"),
            Bind(GroundNotesKeySequenceActionIds.FindLinks, "Find links", "space", "f", "l"),

            Bind(GroundNotesKeySequenceActionIds.NewNote, "New note", "space", "n", "n"),
            Bind(GroundNotesKeySequenceActionIds.NewNoteWindow, "New note window", "space", "n", "w"),
            Bind(GroundNotesKeySequenceActionIds.DeleteNote, "Delete note", "space", "n", "d"),

            Bind(GroundNotesKeySequenceActionIds.FocusNextPane, "Focus next pane", "space", "w", "w"),
            Bind(GroundNotesKeySequenceActionIds.FocusPaneLeft, "Focus pane left", "space", "w", "h"),
            Bind(GroundNotesKeySequenceActionIds.FocusPaneRight, "Focus pane right", "space", "w", "l"),
            Bind(GroundNotesKeySequenceActionIds.ClosePane, "Close pane", "space", "w", "c"),
            Bind(GroundNotesKeySequenceActionIds.EqualizePanes, "Equalize panes", "space", "w", "="),

            Bind(GroundNotesKeySequenceActionIds.FocusEditor, "Focus editor", "space", "g", "e"),
            Bind(GroundNotesKeySequenceActionIds.FocusTitle, "Focus title", "space", "g", "t"),
            Bind(GroundNotesKeySequenceActionIds.FocusMetadata, "Focus metadata", "space", "g", "m"),
            Bind(GroundNotesKeySequenceActionIds.FocusSidebar, "Focus sidebar", "space", "g", "s"),

            Bind(GroundNotesKeySequenceActionIds.ToggleSidebar, "Toggle sidebar", "space", "v", "s"),
            Bind(GroundNotesKeySequenceActionIds.ToggleZenMode, "Toggle zen mode", "space", "v", "z"),
            Bind(GroundNotesKeySequenceActionIds.ToggleYaml, "Toggle YAML", "space", "v", "y"),
            Bind(GroundNotesKeySequenceActionIds.ReloadNotes, "Reload notes", "space", "v", "r"),

            Bind(GroundNotesKeySequenceActionIds.OpenAiChat, "Open AI chat", "space", "a", "c"),
            Bind(GroundNotesKeySequenceActionIds.GenerateTitleSuggestions, "Generate title suggestions", "space", "a", "t"),

            Bind(GroundNotesKeySequenceActionIds.OpenSettings, "Open settings", "space", ","),
            Bind(GroundNotesKeySequenceActionIds.ShowShortcuts, "Show shortcuts", "space", "?"),

            BindStrokes(GroundNotesKeySequenceActionIds.FocusPaneLeft, "Focus pane left", new KeyStroke("w", KeyStrokeModifiers.Control), new KeyStroke("h")),
            BindStrokes(GroundNotesKeySequenceActionIds.FocusPaneRight, "Focus pane right", new KeyStroke("w", KeyStrokeModifiers.Control), new KeyStroke("l")),
            BindStrokes(GroundNotesKeySequenceActionIds.FocusNextPane, "Focus next pane", new KeyStroke("w", KeyStrokeModifiers.Control), new KeyStroke("w")),
            BindStrokes(GroundNotesKeySequenceActionIds.ClosePane, "Close pane", new KeyStroke("w", KeyStrokeModifiers.Control), new KeyStroke("c")),
            BindStrokes(GroundNotesKeySequenceActionIds.EqualizePanes, "Equalize panes", new KeyStroke("w", KeyStrokeModifiers.Control), new KeyStroke("="))
        });

    public static IReadOnlyList<KeySequenceBinding> Bindings => s_bindings;

    public static KeySequenceResolver CreateResolver()
    {
        return new KeySequenceResolver(Bindings);
    }

    private static KeySequenceBinding Bind(string actionId, string description, params string[] keys)
    {
        return BindStrokes(actionId, description, keys.Select(key => new KeyStroke(key)).ToArray());
    }

    private static KeySequenceBinding BindStrokes(string actionId, string description, params KeyStroke[] keys)
    {
        return new KeySequenceBinding(actionId, keys, description);
    }
}
