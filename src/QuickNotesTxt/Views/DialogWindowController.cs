using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace QuickNotesTxt.Views;

internal sealed class DialogWindowController
{
    private readonly Window _window;
    private readonly WindowChromeController _windowChrome;
    private readonly Action _closeAction;
    private readonly Func<IInputElement?>? _initialFocus;

    public DialogWindowController(Window window, Action closeAction, Func<IInputElement?>? initialFocus = null)
    {
        _window = window;
        _closeAction = closeAction;
        _initialFocus = initialFocus;
        _windowChrome = new WindowChromeController(
            window,
            new WindowChromeController.Options
            {
                IdleCursor = Cursor.Default,
                CheckCanResizeOnHover = false,
                CheckWindowStateOnHover = false,
                CheckWindowStateOnResizePressed = false
            });
    }

    public void Attach()
    {
        _window.Opened += OnOpened;
    }

    public void Detach()
    {
        _window.Opened -= OnOpened;
    }

    public void OnTitleBarPointerPressed(PointerPressedEventArgs e) => _windowChrome.OnTitleBarPointerPressed(e);

    public void OnCloseRequested() => _closeAction();

    public bool HandleEscape(KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return false;
        }

        e.Handled = true;
        _closeAction();
        return true;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_initialFocus is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _initialFocus()?.Focus(), DispatcherPriority.Input);
    }
}
