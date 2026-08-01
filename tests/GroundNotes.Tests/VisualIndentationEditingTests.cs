using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class VisualIndentationEditingTests
{
    [AvaloniaFact]
    public void Backspace_BlankFencedLine_DeletesPrecedingNewline()
    {
        const string text = "before\n```\n\n```\nafter";
        using var fixture = CreateFixture(text);
        var blankOffset = text.IndexOf("\n\n", StringComparison.Ordinal) + 1;
        fixture.Editor.CaretOffset = blankOffset;

        EditingCommands.Backspace.Execute(null, fixture.Editor.TextArea);

        Assert.Equal("before\n```\n```\nafter", fixture.Editor.Document.Text);
        Assert.Equal(blankOffset - 1, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);
    }

    [AvaloniaFact]
    public void Backspace_SecondConsecutiveBlankFencedLine_DeletesOnlyPrecedingNewline()
    {
        const string text = "before\n```\n\n\n```\nafter";
        using var fixture = CreateFixture(text);
        var secondBlankOffset = text.IndexOf("\n\n\n", StringComparison.Ordinal) + 2;
        fixture.Editor.CaretOffset = secondBlankOffset;

        EditingCommands.Backspace.Execute(null, fixture.Editor.TextArea);

        Assert.Equal("before\n```\n\n```\nafter", fixture.Editor.Document.Text);
        Assert.Equal(secondBlankOffset - 1, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);
        AssertCaretIsAtOrAfterCodeInset(fixture.Editor);
    }

    [AvaloniaFact]
    public void Backspace_NonblankFencedLineAtColumnZero_DeletesPrecedingNewline()
    {
        const string text = "before\n```\ncode\n```\nafter";
        using var fixture = CreateFixture(text);
        var codeOffset = text.IndexOf("code", StringComparison.Ordinal);
        fixture.Editor.CaretOffset = codeOffset;

        EditingCommands.Backspace.Execute(null, fixture.Editor.TextArea);

        Assert.Equal("before\n```code\n```\nafter", fixture.Editor.Document.Text);
        Assert.Equal(codeOffset - 1, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);
    }

    [AvaloniaFact]
    public void Backspace_AtDocumentOffsetZero_IsNoOp()
    {
        using var fixture = CreateFixture("text");
        fixture.Editor.CaretOffset = 0;

        EditingCommands.Backspace.Execute(null, fixture.Editor.TextArea);

        Assert.Equal("text", fixture.Editor.Document.Text);
        Assert.Equal(0, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);
    }

    [AvaloniaFact]
    public void Delete_BlankFencedLine_DeletesFollowingNewline()
    {
        const string text = "before\n```\n\n```\nafter";
        using var fixture = CreateFixture(text);
        var blankOffset = text.IndexOf("\n\n", StringComparison.Ordinal) + 1;
        fixture.Editor.CaretOffset = blankOffset;

        EditingCommands.Delete.Execute(null, fixture.Editor.TextArea);

        Assert.Equal("before\n```\n```\nafter", fixture.Editor.Document.Text);
        Assert.Equal(blankOffset, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);
    }

    [AvaloniaFact]
    public void Backspace_OrdinaryLine_DeletesPrecedingNewline()
    {
        const string text = "before\nordinary";
        using var fixture = CreateFixture(text);
        fixture.Editor.CaretOffset = text.IndexOf("ordinary", StringComparison.Ordinal);

        EditingCommands.Backspace.Execute(null, fixture.Editor.TextArea);

        Assert.Equal("beforeordinary", fixture.Editor.Document.Text);
        Assert.Equal("before".Length, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);
    }

    [AvaloniaFact]
    public void CaretNavigation_BlankFencedLineCrossesVisualIndentationToDocumentNeighbor()
    {
        const string text = "before\n```\n\n```\nafter";
        using var fixture = CreateFixture(text);
        var blankOffset = text.IndexOf("\n\n", StringComparison.Ordinal) + 1;
        fixture.Editor.CaretOffset = blankOffset;

        EditingCommands.MoveLeftByCharacter.Execute(null, fixture.Editor.TextArea);

        Assert.Equal(blankOffset - 1, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);

        EditingCommands.MoveRightByCharacter.Execute(null, fixture.Editor.TextArea);

        Assert.Equal(blankOffset, fixture.Editor.CaretOffset);
        Assert.Equal(0, fixture.Editor.SelectionLength);
    }

    private static void AssertCaretIsAtOrAfterCodeInset(TextEditor editor)
    {
        var line = editor.Document.GetLineByOffset(editor.CaretOffset);
        var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(line);
        var documentColumn = editor.CaretOffset - line.Offset;
        var contentColumn = visualLine.GetVisualColumn(documentColumn);
        var caretX = editor.TextArea.Caret.CalculateCaretRectangle().Left;
        var insetX = visualLine.GetVisualPosition(contentColumn, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle).X;

        Assert.True(caretX >= insetX - 1.0, $"Caret X {caretX} should be at or after code inset {insetX}.");
    }

    private static Fixture CreateFixture(string text)
    {
        var editor = new TextEditor
        {
            Document = new TextDocument(text),
            WordWrap = true,
            Width = 520,
            Height = 240
        };
        var colorizer = new MarkdownColorizingTransformer();
        var controller = new EditorThemeController(editor, colorizer);
        var window = new Window
        {
            Width = editor.Width,
            Height = editor.Height,
            Content = editor
        };
        window.Show();
        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        editor.ApplyTemplate();
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));
        var textView = editor.TextArea.TextView;
        textView.Measure(new Size(editor.Width, editor.Height));
        textView.Arrange(new Rect(0, 0, editor.Width, editor.Height));
        textView.EnsureVisualLines();
        return new Fixture(editor, colorizer, controller, window);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(TextEditor editor, MarkdownColorizingTransformer colorizer, EditorThemeController controller, Window window)
        {
            Editor = editor;
            _colorizer = colorizer;
            _controller = controller;
            _window = window;
        }

        public TextEditor Editor { get; }

        private readonly MarkdownColorizingTransformer _colorizer;
        private readonly EditorThemeController _controller;
        private readonly Window _window;

        public void Dispose()
        {
            _window.Close();
            _controller.Dispose();
            _colorizer.Dispose();
        }
    }
}
