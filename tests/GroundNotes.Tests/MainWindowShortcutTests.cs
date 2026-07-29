using Avalonia.Input;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MainWindowShortcutTests
{


    [Fact]
    public void FormatSidebarDragLabel_UsesNameAndSelectionCount()
    {
        var first = new GroundNotes.ViewModels.NoteListItemViewModel(new GroundNotes.Models.NoteSummary
        {
            FilePath = "/notes/alpha.md",
            Title = "alpha"
        });
        var second = new GroundNotes.ViewModels.NoteListItemViewModel(new GroundNotes.Models.NoteSummary
        {
            FilePath = "/notes/beta.md",
            Title = "beta"
        });

        Assert.Equal("alpha", MainWindow.FormatSidebarDragLabel([first]));
        Assert.Equal("alpha +1", MainWindow.FormatSidebarDragLabel([first, second]));
    }

    [Theory]
    [InlineData(Key.Z, KeyModifiers.Control, true)]
    [InlineData(Key.Z, KeyModifiers.Meta, true)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.Y, KeyModifiers.Control, false)]
    [InlineData(Key.Space, KeyModifiers.Control, false)]
    public void IsUndoShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsUndoShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Y, KeyModifiers.Control, true)]
    [InlineData(Key.Y, KeyModifiers.Meta, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift, true)]
    [InlineData(Key.Z, KeyModifiers.Control, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, false)]
    [InlineData(Key.Z, KeyModifiers.Shift, false)]
    [InlineData(Key.Space, KeyModifiers.Control | KeyModifiers.Shift, false)]
    public void IsRedoShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsRedoShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Control, true)]
    [InlineData(Key.Enter, KeyModifiers.Meta, true)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Enter, KeyModifiers.Meta | KeyModifiers.Shift, false)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Meta, false)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.Space, KeyModifiers.Control, false)]
    public void IsOpenNoteInNewWindowGesture_MatchesExactModifiers(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsOpenNoteInNewWindowGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(Key.Enter, KeyModifiers.Meta | KeyModifiers.Shift, true)]
    [InlineData(Key.Enter, KeyModifiers.Control, false)]
    [InlineData(Key.Enter, KeyModifiers.Meta, false)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Shift, false)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, false)]
    [InlineData(Key.Space, KeyModifiers.Control | KeyModifiers.Shift, false)]
    public void IsOpenNoteInZenWindowGesture_MatchesExactModifiers(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsOpenNoteInZenWindowGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Control, true)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.Space, KeyModifiers.Control, false)]
    public void IsToggleTaskShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsToggleTaskShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.OemComma, KeyModifiers.Control, true)]
    [InlineData(Key.OemComma, KeyModifiers.Meta, true)]
    [InlineData(Key.OemComma, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.OemComma, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.OemPeriod, KeyModifiers.Control, false)]
    [InlineData(Key.OemComma, KeyModifiers.None, false)]
    public void IsOpenSettingsGesture_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsOpenSettingsGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.F1, KeyModifiers.None, true)]
    [InlineData(Key.F1, KeyModifiers.Control, false)]
    [InlineData(Key.Oem2, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.OemComma, KeyModifiers.None, false)]
    public void IsShowShortcutsHelpGesture_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsShowShortcutsHelpGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.W, KeyModifiers.Control, true)]
    [InlineData(Key.W, KeyModifiers.Meta, true)]
    [InlineData(Key.W, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.W, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.O, KeyModifiers.Control, false)]
    public void IsClosePaneGesture_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsClosePaneGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Y, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(Key.Y, KeyModifiers.Meta | KeyModifiers.Shift, true)]
    [InlineData(Key.Y, KeyModifiers.Control, false)]
    [InlineData(Key.Y, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, false)]
    public void IsToggleYamlEditorShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsToggleYamlEditorShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Up, KeyModifiers.Alt, true, false)]
    [InlineData(Key.Down, KeyModifiers.Alt, true, true)]
    [InlineData(Key.Up, KeyModifiers.Control, false, false)]
    [InlineData(Key.Down, KeyModifiers.Shift, false, false)]
    [InlineData(Key.Up, KeyModifiers.Control | KeyModifiers.Alt, false, false)]
    [InlineData(Key.Left, KeyModifiers.Alt, false, false)]
    public void IsMoveLineShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected, bool expectedMoveDown)
    {
        var result = MainWindow.IsMoveLineShortcut(key, modifiers, out var moveDown);

        Assert.Equal(expected, result);
        Assert.Equal(expectedMoveDown, moveDown);
    }

    [Theory]
    [InlineData(Key.Enter, true)]
    [InlineData(Key.Escape, false)]
    [InlineData(Key.Space, false)]
    [InlineData(Key.Tab, false)]
    public void IsRenameTextBoxSubmitKey_MatchesExpectedKeys(Key key, bool expected)
    {
        var result = MainWindow.IsRenameTextBoxSubmitKey(key);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Escape, true)]
    [InlineData(Key.Enter, false)]
    [InlineData(Key.Space, false)]
    [InlineData(Key.Tab, false)]
    public void IsRenameTextBoxCancelKey_MatchesExpectedKeys(Key key, bool expected)
    {
        var result = MainWindow.IsRenameTextBoxCancelKey(key);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.None, true)]
    [InlineData(Key.Up, KeyModifiers.None, true)]
    [InlineData(Key.Down, KeyModifiers.None, true)]
    [InlineData(Key.Escape, KeyModifiers.Control, true)]
    [InlineData(Key.Enter, KeyModifiers.Control, false)]
    [InlineData(Key.Up, KeyModifiers.Shift, false)]
    [InlineData(Key.Space, KeyModifiers.None, false)]
    public void SlashCommandNavigation_DoesNotConsumeModifiedShortcuts(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, SlashCommandPopupController.ShouldHandleNavigationKey(key, modifiers));
    }

    [Fact]
    public void SlashCommandPopupLayout_KeepsPlacementWhileFilteringResults()
    {
        var initial = SlashCommandPopupController.CalculateLayout(
            editorWidth: 800,
            editorHeight: 600,
            anchorLeft: 300,
            anchorTop: 280,
            anchorWidth: 2,
            anchorHeight: 18,
            desiredPopupHeight: 274);
        var filtered = SlashCommandPopupController.CalculateLayout(
            editorWidth: 800,
            editorHeight: 600,
            anchorLeft: 300,
            anchorTop: 280,
            anchorWidth: 2,
            anchorHeight: 18,
            desiredPopupHeight: 96,
            initial.VerticalPlacement,
            initial.HorizontalPlacement);

        Assert.Equal(initial.VerticalPlacement, filtered.VerticalPlacement);
        Assert.Equal(initial.HorizontalPlacement, filtered.HorizontalPlacement);
        Assert.Equal(initial.PopupWidth, filtered.PopupWidth);
    }

    [Fact]
    public void SlashCommandPopupLayout_SwitchesWhenCurrentSideNoLongerFitsHeader()
    {
        var layout = SlashCommandPopupController.CalculateLayout(
            editorWidth: 800,
            editorHeight: 600,
            anchorLeft: 300,
            anchorTop: 570,
            anchorWidth: 2,
            anchorHeight: 18,
            desiredPopupHeight: 274,
            currentVerticalPlacement: SlashPopupVerticalPlacement.Below,
            currentHorizontalPlacement: SlashPopupHorizontalPlacement.Right);

        Assert.Equal(SlashPopupVerticalPlacement.Above, layout.VerticalPlacement);
        Assert.True(layout.ListMaxHeight > 0);
    }

    [Fact]
    public void SlashCommandPopupLayout_UsesActualSpaceInSmallEditors()
    {
        var layout = SlashCommandPopupController.CalculateLayout(
            editorWidth: 100,
            editorHeight: 120,
            anchorLeft: 45,
            anchorTop: 50,
            anchorWidth: 2,
            anchorHeight: 18,
            desiredPopupHeight: 274);

        Assert.Equal(76, layout.PopupWidth);
        Assert.InRange(layout.ListMaxHeight, 0, 220);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Control, true)]
    [InlineData(Key.Enter, KeyModifiers.Meta, true)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Enter, KeyModifiers.Alt, false)]
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.Tab, KeyModifiers.Control, false)]
    public void IsAiSendGesture_MatchesExpectedKeys(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = AiSendShortcut.IsSendGesture(key, modifiers);

        Assert.Equal(expected, result);
    }
}
