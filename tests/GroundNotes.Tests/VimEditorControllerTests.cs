using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using GroundNotes.Editors.Vim;
using GroundNotes.Models;
using GroundNotes.Services.KeySequences.Defaults;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class VimEditorControllerTests
{
    private static readonly object ApplicationLock = new();
    private static bool s_applicationInitialized;

    [Fact]
    public void NormalAndInsertModes_RouteTextThroughExpectedLayer()
    {
        EnsureApplication();
        var editor = new TextEditor
        {
            Document = new TextDocument("one two")
        };
        using var controller = new VimEditorController(editor, new VimWorkspaceState());
        controller.SetSettings(VimModeSettings.Default with { IsEnabled = true });

        Assert.Equal(CaretShape.Block, editor.TextArea.CaretShape);
        Assert.False(editor.TextArea.OverstrikeMode);

        editor.TextArea.PerformTextInput("l");

        Assert.Equal("one two", editor.Document.Text);
        Assert.Equal(1, editor.CaretOffset);
        Assert.Equal(VimMode.Normal, controller.Mode);

        editor.TextArea.PerformTextInput("i");
        editor.TextArea.PerformTextInput("X");

        Assert.Equal("oXne two", editor.Document.Text);
        Assert.Equal(VimMode.Insert, controller.Mode);
        Assert.Equal(CaretShape.Bar, editor.TextArea.CaretShape);
        Assert.False(editor.TextArea.OverstrikeMode);

        Assert.True(controller.ProcessSpecialKey(VimKey.Escape));
        Assert.Equal(VimMode.Normal, controller.Mode);
        Assert.Equal(CaretShape.Block, editor.TextArea.CaretShape);

        controller.SetSettings(VimModeSettings.Default);
        Assert.Equal(CaretShape.Bar, editor.TextArea.CaretShape);
    }

    [Fact]
    public void InsertSession_IsSingleUndoStep()
    {
        EnsureApplication();
        var editor = new TextEditor { Document = new TextDocument("base") };
        using var controller = new VimEditorController(editor, new VimWorkspaceState());
        controller.SetSettings(VimModeSettings.Default with { IsEnabled = true });

        editor.TextArea.PerformTextInput("i");
        editor.TextArea.PerformTextInput("A");
        editor.TextArea.PerformTextInput("B");
        controller.ProcessSpecialKey(VimKey.Escape);

        Assert.Equal("ABbase", editor.Document.Text);
        editor.Undo();
        Assert.Equal("base", editor.Document.Text);
    }

    [Fact]
    public void LeaderSequence_EmitsGroundNotesCommandWithoutEditingDocument()
    {
        EnsureApplication();
        var editor = new TextEditor
        {
            Document = new TextDocument("note")
        };
        using var controller = new VimEditorController(editor, new VimWorkspaceState());
        controller.SetSettings(VimModeSettings.Default with
        {
            IsEnabled = true,
            WhichKeyDelayMilliseconds = 0
        });
        string? actionId = null;
        controller.SetLeaderCommandHandler(action =>
        {
            actionId = action;
            return Task.CompletedTask;
        });

        editor.TextArea.PerformTextInput(" ");
        editor.TextArea.PerformTextInput("f");
        editor.TextArea.PerformTextInput("f");

        Assert.Equal(GroundNotesKeySequenceActionIds.OpenNotePicker, actionId);
        Assert.Equal("note", editor.Document.Text);
        Assert.Equal(VimMode.Normal, controller.Mode);
    }

    [Fact]
    public void CustomLeader_UsesConfiguredCharacterInsteadOfSpace()
    {
        EnsureApplication();
        var editor = new TextEditor { Document = new TextDocument("note") };
        using var controller = new VimEditorController(editor, new VimWorkspaceState());
        controller.SetSettings(VimModeSettings.Default with
        {
            IsEnabled = true,
            LeaderKey = ",",
            WhichKeyDelayMilliseconds = 0
        });
        string? actionId = null;
        controller.SetLeaderCommandHandler(action =>
        {
            actionId = action;
            return Task.CompletedTask;
        });

        editor.TextArea.PerformTextInput(",");
        editor.TextArea.PerformTextInput("f");
        editor.TextArea.PerformTextInput("f");

        Assert.Equal(GroundNotesKeySequenceActionIds.OpenNotePicker, actionId);
        Assert.Equal("note", editor.Document.Text);
    }

    [Fact]
    public void OperatorPending_DoesNotStartApplicationLeader()
    {
        EnsureApplication();
        var editor = new TextEditor { Document = new TextDocument("one two") };
        using var controller = new VimEditorController(editor, new VimWorkspaceState());
        controller.SetSettings(VimModeSettings.Default with { IsEnabled = true });
        string? actionId = null;
        controller.SetLeaderCommandHandler(action =>
        {
            actionId = action;
            return Task.CompletedTask;
        });

        editor.TextArea.PerformTextInput("d");
        editor.TextArea.PerformTextInput(" ");
        editor.TextArea.PerformTextInput("f");
        editor.TextArea.PerformTextInput("f");

        Assert.Null(actionId);
        Assert.Equal("one two", editor.Document.Text);
        Assert.Equal(VimMode.Normal, controller.Mode);
    }

    [Fact]
    public void VisualMode_UpdatesAvaloniaSelectionAndStatus()
    {
        EnsureApplication();
        var editor = new TextEditor { Document = new TextDocument("alpha") };
        using var controller = new VimEditorController(editor, new VimWorkspaceState());
        controller.SetSettings(VimModeSettings.Default with { IsEnabled = true });
        string? status = null;
        controller.StatusChanged += (_, e) => status = e.Text;

        editor.TextArea.PerformTextInput("v");
        editor.TextArea.PerformTextInput("l");

        Assert.Equal(VimMode.Visual, controller.Mode);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(2, editor.SelectionLength);
        Assert.Contains("VISUAL", status);

        controller.ProcessSpecialKey(VimKey.Escape);
        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal(VimMode.Normal, controller.Mode);
    }

    [Fact]
    public void SharedWorkspaceRegister_AllowsPasteInAnotherEditor()
    {
        EnsureApplication();
        var workspace = new VimWorkspaceState();
        var source = new TextEditor { Document = new TextDocument("alpha\nbeta") };
        var target = new TextEditor { Document = new TextDocument("gamma") };
        using var sourceController = new VimEditorController(source, workspace);
        using var targetController = new VimEditorController(target, workspace);
        var settings = VimModeSettings.Default with { IsEnabled = true };
        sourceController.SetSettings(settings);
        targetController.SetSettings(settings);

        source.TextArea.PerformTextInput("y");
        source.TextArea.PerformTextInput("y");
        target.TextArea.PerformTextInput("p");

        Assert.Equal("gamma\nalpha", target.Document.Text);
    }

    private static void EnsureApplication()
    {
        lock (ApplicationLock)
        {
            if (s_applicationInitialized || Application.Current is not null)
            {
                s_applicationInitialized = true;
                return;
            }

            try
            {
                GroundNotes.Program.BuildAvaloniaApp().SetupWithoutStarting();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Setup was already called", StringComparison.Ordinal))
            {
            }

            s_applicationInitialized = true;
        }
    }
}
