using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using GroundNotes.Models;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class SlashCommandPopupControllerTests
{
    [AvaloniaFact]
    public void ApplyCustomCommandPreservesExactTemplateAndCaret()
    {
        const string document = "prefix /literal";
        const string template = "  first line  \nsecond line  ";
        var editor = new TextEditor { Document = new TextDocument(document), CaretOffset = document.Length };
        using var controller = CreateController(editor, () => [new CustomSlashCommandDefinition("literal", "Literal", template)]);

        controller.ScheduleRefresh();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        MarkdownEditResult? applied = null;
        var applyCount = 0;

        controller.ApplySelectedCommand(edit =>
        {
            applyCount++;
            applied = edit;
        });

        var result = Assert.NotNull(applied);
        Assert.Equal(1, applyCount);
        var triggerStart = document.IndexOf("/literal", StringComparison.Ordinal);
        Assert.Equal(triggerStart, result.Start);
        Assert.Equal("/literal".Length, result.Length);
        Assert.Equal(template, result.Replacement);
        Assert.Equal(triggerStart + template.Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [AvaloniaTheory]
    [InlineData("{cursor}before", "before", 0)]
    [InlineData("before{cursor}after", "beforeafter", 6)]
    [InlineData("before{cursor}", "before", 6)]
    [InlineData("a{cursor}b{cursor}c", "ab{cursor}c", 1)]
    [InlineData("  exact  ", "  exact  ", 9)]
    public void ApplyCustomCommandHandlesFirstCursorMarker(
        string template,
        string expectedReplacement,
        int expectedSelectionOffset)
    {
        const string document = "prefix /literal";
        var editor = new TextEditor { Document = new TextDocument(document), CaretOffset = document.Length };
        using var controller = CreateController(editor, () => [new CustomSlashCommandDefinition("literal", "Literal", template)]);

        controller.ScheduleRefresh();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        editor.CaretOffset = document.Length;
        MarkdownEditResult? applied = null;
        var applyCount = 0;

        controller.ApplySelectedCommand(edit =>
        {
            applyCount++;
            applied = edit;
        });

        var result = Assert.NotNull(applied);
        var triggerStart = document.IndexOf("/literal", StringComparison.Ordinal);
        Assert.Equal(1, applyCount);
        Assert.Equal(expectedReplacement, result.Replacement);
        Assert.Equal(triggerStart + expectedSelectionOffset, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [AvaloniaFact]
    public void ApplyBuiltInCommandStillProducesEdit()
    {
        const string document = "prefix /bold";
        var editor = new TextEditor { Document = new TextDocument(document), CaretOffset = document.Length };
        using var controller = CreateController(editor);

        controller.ScheduleRefresh();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        MarkdownEditResult? applied = null;

        controller.ApplySelectedCommand(edit => applied = edit);

        var result = Assert.NotNull(applied);
        Assert.Equal("prefix ".Length, result.Start);
        Assert.Equal(0, result.Length);
        Assert.Equal("****", result.Replacement);
    }

    private static SlashCommandPopupController CreateController(
        TextEditor editor,
        Func<IReadOnlyList<CustomSlashCommandDefinition>>? customCommands = null)
    {
        return new SlashCommandPopupController(
            editor,
            new Border(),
            new Popup(),
            new Border(),
            new ListBox(),
            new TextBlock(),
            customCommands);
    }
}
