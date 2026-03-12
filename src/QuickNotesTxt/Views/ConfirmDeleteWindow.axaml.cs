using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace QuickNotesTxt.Views;

public partial class ConfirmDeleteWindow : Window
{
    public ConfirmDeleteWindow()
    {
        InitializeComponent();
        DataContext = new ConfirmDeleteViewModel();
        Opened += (_, _) => this.FindControl<Button>("DeleteButton")?.Focus();
    }

    public ConfirmDeleteWindow(string noteName) : this()
    {
        DataContext = new ConfirmDeleteViewModel
        {
            Message = $"Delete '{noteName}' permanently?"
        };
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Close(true);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(false);
        }
    }

    private sealed class ConfirmDeleteViewModel
    {
        public string Message { get; init; } = string.Empty;
    }
}
