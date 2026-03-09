using Avalonia.Controls;
using Avalonia.Interactivity;

namespace QuickNotesTxt.Views;

public partial class ConfirmDeleteWindow : Window
{
    public ConfirmDeleteWindow()
    {
        InitializeComponent();
        DataContext = new ConfirmDeleteViewModel();
    }

    public ConfirmDeleteWindow(string noteName) : this()
    {
        DataContext = new ConfirmDeleteViewModel
        {
            Message = $"Delete '{noteName}' permanently?"
        };
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private sealed class ConfirmDeleteViewModel
    {
        public string Message { get; init; } = string.Empty;
    }
}
