using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class TagFolderDialogWindow : Window
{
    private readonly DialogWindowController _dialogController;

    public TagFolderDialogWindow()
    {
        InitializeComponent();
        DataContext = new TagFolderDialogViewModel(string.Empty, string.Empty, string.Empty, string.Empty);
        _dialogController = new DialogWindowController(this, () => Close(null), GetInitialFocusControl);
        _dialogController.Attach();
        Closed += (_, _) => _dialogController.Detach();
    }

    public static TagFolderDialogWindow Create() => new()
    {
        DataContext = new TagFolderDialogViewModel(
            "Create tag folder",
            "Create tag folder",
            "Enter a tag folder path. Use '/' to create nested folders.",
            "Create")
    };

    public static TagFolderDialogWindow Rename(string currentPath) => new()
    {
        DataContext = new TagFolderDialogViewModel(
            "Rename tag folder",
            "Rename tag folder",
            "Enter the new tag folder path.",
            "Rename",
            currentPath)
    };

    public static TagFolderDialogWindow ChooseDestination(IReadOnlyList<string> folderPaths) => new()
    {
        DataContext = new TagFolderDialogViewModel(
            "Choose destination",
            "Choose destination folder",
            "Select an existing tag folder.",
            "Choose",
            folderPaths: folderPaths)
    };

    private Control GetInitialFocusControl()
    {
        if (DataContext is TagFolderDialogViewModel { IsFolderChoice: true })
        {
            return FolderComboBox;
        }

        PathTextBox.SelectAll();
        return PathTextBox;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnTitleBarPointerPressed(e);

    private void OnTitleBarCloseRequested(object? sender, EventArgs e) => _dialogController.OnCloseRequested();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _dialogController.OnCloseRequested();

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Submit();

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dialogController.HandleEscape(e))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Submit();
        }
    }

    private void Submit()
    {
        if (DataContext is not TagFolderDialogViewModel model)
        {
            return;
        }

        var value = model.IsFolderChoice ? FolderComboBox.SelectedItem as string : PathTextBox.Text;
        if (!string.IsNullOrWhiteSpace(value))
        {
            Close(value.Trim());
        }
    }
}
