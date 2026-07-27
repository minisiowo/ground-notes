using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit;
using GroundNotes.Editors.Vim;
using GroundNotes.Services.KeySequences.Defaults;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class MainWindow
{
    private readonly VimWorkspaceState _vimWorkspaceState = new();

    private void ConfigureVimHost(EditorHostController host, TextEditor editor, TextBlock? statusText)
    {
        host.VimStatusChanged += (_, e) =>
        {
            if (statusText is null)
            {
                return;
            }

            statusText.Text = e.Text;
            statusText.IsVisible = e.IsVisible;
            if (statusText.Parent is Control container)
            {
                container.IsVisible = e.IsVisible;
            }
        };
        host.SetVimLeaderCommandHandler(ExecuteVimLeaderCommandAsync);
        host.SetPreVimKeyHandler(e => _slashCommandPopup.HandleKeyDown(e, edit => ApplyEditorEdit(editor, edit)));

        if (DataContext is MainViewModel vm)
        {
            host.SetVimModeSettings(vm.VimModeSettings);
        }
    }

    private void ApplyVimSettings(MainViewModel viewModel)
    {
        _editorHost.SetVimModeSettings(viewModel.VimModeSettings);
        foreach (var host in _secondaryEditorHosts.Values)
        {
            host.SetVimModeSettings(viewModel.VimModeSettings);
        }
    }

    private TextBlock? FindSecondaryVimStatus(Guid paneId)
    {
        return _secondaryPaneRoots.TryGetValue(paneId, out var root)
            ? root.FindControl<TextBlock>("SecondaryVimStatusText")
            : null;
    }

    private TextBox? FindSecondaryTitleTextBox(Guid paneId)
    {
        return _secondaryPaneRoots.TryGetValue(paneId, out var root)
            ? root.FindControl<TextBox>("SecondaryTitleTextBox")
            : null;
    }

    private async Task ExecuteVimLeaderCommandAsync(string actionId)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        switch (actionId)
        {
            case GroundNotesKeySequenceActionIds.OpenNotePicker:
                vm.OpenNotePickerCommand.Execute(null);
                break;
            case GroundNotesKeySequenceActionIds.SearchNotes:
                EnsureSidebarVisible(vm);
                Dispatcher.UIThread.Post(() => SidebarSearchTextBox.Focus(), DispatcherPriority.Input);
                break;
            case GroundNotesKeySequenceActionIds.FindLinks:
            case GroundNotesKeySequenceActionIds.FocusSidebar:
                EnsureSidebarVisible(vm);
                FocusNotesListBox();
                break;
            case GroundNotesKeySequenceActionIds.NewNote:
                await vm.NewNoteCommand.ExecuteAsync(null);
                break;
            case GroundNotesKeySequenceActionIds.NewNoteWindow:
                await InvokeOpenNewNoteInWindowAsync();
                break;
            case GroundNotesKeySequenceActionIds.DeleteNote:
                await vm.DeleteCurrentNoteCommand.ExecuteAsync(null);
                break;
            case GroundNotesKeySequenceActionIds.FocusNextPane:
            case GroundNotesKeySequenceActionIds.FocusPaneRight:
                FocusPaneByDelta(1);
                break;
            case GroundNotesKeySequenceActionIds.FocusPaneLeft:
                FocusPaneByDelta(-1);
                break;
            case GroundNotesKeySequenceActionIds.ClosePane:
                if (IsStandaloneWindow)
                {
                    await RequestCloseAsync();
                }
                else
                {
                    await vm.CloseActivePaneAsync();
                }
                break;
            case GroundNotesKeySequenceActionIds.EqualizePanes:
                EqualizePaneWidthsToActivePane(vm);
                break;
            case GroundNotesKeySequenceActionIds.FocusEditor:
                Dispatcher.UIThread.Post(() => GetActiveTextEditor().Focus(), DispatcherPriority.Input);
                break;
            case GroundNotesKeySequenceActionIds.FocusTitle:
                FocusActiveTitle(vm);
                break;
            case GroundNotesKeySequenceActionIds.FocusMetadata:
                Dispatcher.UIThread.Post(() => GetActiveTagsTextBox().Focus(), DispatcherPriority.Input);
                break;
            case GroundNotesKeySequenceActionIds.ToggleSidebar:
                vm.ToggleSidebarCommand.Execute(null);
                break;
            case GroundNotesKeySequenceActionIds.ToggleZenMode:
                ToggleZenMode();
                break;
            case GroundNotesKeySequenceActionIds.ToggleYaml:
                await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
                break;
            case GroundNotesKeySequenceActionIds.ReloadNotes:
                await vm.ReloadCommand.ExecuteAsync(null);
                break;
            case GroundNotesKeySequenceActionIds.OpenAiChat:
                await vm.OpenChatCommand.ExecuteAsync(null);
                break;
            case GroundNotesKeySequenceActionIds.GenerateTitleSuggestions:
                await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);
                break;
            case GroundNotesKeySequenceActionIds.OpenSettings:
                await vm.OpenSettingsCommand.ExecuteAsync(null);
                break;
            case GroundNotesKeySequenceActionIds.ShowShortcuts:
                await vm.ShowKeyboardShortcutsHelpCommand.ExecuteAsync(null);
                break;
        }
    }

    private static void EnsureSidebarVisible(MainViewModel viewModel)
    {
        if (viewModel.SidebarCollapsed && viewModel.ToggleSidebarCommand.CanExecute(null))
        {
            viewModel.ToggleSidebarCommand.Execute(null);
        }
    }

    private void FocusActiveTitle(MainViewModel viewModel)
    {
        TextBox? titleTextBox = viewModel.ActiveSecondaryPane is { } pane
            ? FindSecondaryTitleTextBox(pane.Id)
            : EditorTitleTextBox;
        if (titleTextBox is not null)
        {
            Dispatcher.UIThread.Post(() => titleTextBox.Focus(), DispatcherPriority.Input);
        }
    }

    private void CycleMainFocus(bool reverse)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var controls = new List<Control>();
        if (!vm.SidebarCollapsed && !_isZenMode)
        {
            controls.Add(SidebarSearchTextBox);
            controls.Add(NotesListBox);
        }

        var activeTitle = vm.ActiveSecondaryPane is { } activePane
            ? FindSecondaryTitleTextBox(activePane.Id)
            : EditorTitleTextBox;
        if (activeTitle is not null)
        {
            controls.Add(activeTitle);
        }

        controls.Add(GetActiveTagsTextBox());
        controls.Add(GetActiveTextEditor());
        if (controls.Count == 0)
        {
            return;
        }

        var currentIndex = controls.FindIndex(control => control.IsKeyboardFocusWithin || control.IsFocused);
        var step = reverse ? -1 : 1;
        var targetIndex = currentIndex < 0
            ? (reverse ? controls.Count - 1 : 0)
            : (currentIndex + step + controls.Count) % controls.Count;
        controls[targetIndex].Focus();
    }

    private void FocusPaneByDelta(int delta)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var paneCount = vm.SecondaryPanes.Count + 1;
        if (paneCount <= 1)
        {
            EditorTextEditor.Focus();
            return;
        }

        var currentIndex = vm.ActiveSecondaryPane is null
            ? 0
            : vm.SecondaryPanes.IndexOf(vm.ActiveSecondaryPane) + 1;
        var targetIndex = (currentIndex + delta) % paneCount;
        if (targetIndex < 0)
        {
            targetIndex += paneCount;
        }

        if (targetIndex == 0)
        {
            ActivatePrimaryPane();
            Dispatcher.UIThread.Post(() => EditorTextEditor.Focus(), DispatcherPriority.Input);
            return;
        }

        var targetPane = vm.SecondaryPanes[targetIndex - 1];
        SetSecondaryPaneActive(targetPane);
        if (_secondaryEditorControls.TryGetValue(targetPane.Id, out var editor))
        {
            Dispatcher.UIThread.Post(() => editor.Focus(), DispatcherPriority.Input);
        }
    }
}
