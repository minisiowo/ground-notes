using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GroundNotes.Views;

public partial class TitleBarControl : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TitleBarControl, string>(nameof(Title), string.Empty);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public event EventHandler? CloseRequested;
    public event EventHandler<PointerPressedEventArgs>? TitleBarPointerPressed;

    public TitleBarControl()
    {
        InitializeComponent();
    }

    private void OnBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        TitleBarPointerPressed?.Invoke(this, e);
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
