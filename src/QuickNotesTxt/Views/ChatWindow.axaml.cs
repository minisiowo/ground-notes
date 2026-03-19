using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using AvaloniaEdit;
using QuickNotesTxt.Editors;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Views;

public partial class ChatWindow : Window
{
    private const double AutoScrollNearBottomThreshold = 48;

    private bool _isUpdatingEditorFromViewModel;
    private bool _isUpdatingViewModelFromEditor;
    private bool _isProgrammaticAutoScroll;
    private bool _isAutoScrollQueued;
    private bool _isUserNearBottom = true;
    private int _autoScrollRequestVersion;
    private int _queuedAutoScrollVersion;
    private int _queuedAutoScrollOffset;
    private readonly MarkdownColorizingTransformer _markdownColorizer = new();
    private readonly EditorHostController _editorHost;
    private readonly WindowChromeController _windowChrome;
    private IEditorLayoutState? _editorLayoutState;
    private bool _hasAppliedInitialEditorLayout;
    private ChatViewModel? _boundViewModel;

    public ChatWindow()
    {
        InitializeComponent();

        _windowChrome = new WindowChromeController(
            this,
            new WindowChromeController.Options
            {
                IdleCursor = Cursor.Default,
                CheckCanResizeOnHover = false,
                CheckWindowStateOnHover = false,
                CheckWindowStateOnResizePressed = false
            });
        _editorHost = new EditorHostController(ChatTextEditor, _markdownColorizer);

        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        ChatTextEditor.TextArea.TextView.ScrollOffsetChanged += OnEditorTextViewScrollOffsetChanged;
        ChatTextEditor.TextArea.TextView.VisualLinesChanged += OnEditorTextViewVisualLinesChanged;
        ChatTextEditor.SizeChanged += OnChatEditorSizeChanged;
        
        ChatTextEditor.TextChanged += OnEditorTextChanged;
        InputTextBox.TextChanged += OnInputTextChanged;
        
        DataContextChanged += OnDataContextChanged;
        
        Opened += (s, e) =>
        {
            InputTextBox.Focus();
            _editorHost.ApplySelectionTheme();
            if (DataContext is ChatViewModel vm)
            {
                SyncEditorText(vm.EditorBody);
            }

            if (_editorLayoutState is not null)
            {
                _editorHost.ApplyInitialLayout(_editorLayoutState.CurrentSettings);
                _hasAppliedInitialEditorLayout = true;
            }
        };

        Activated += (_, _) =>
        {
            _editorHost.RefreshTypographyResources();
        };

        Closed += (_, _) =>
        {
            if (_boundViewModel is not null)
            {
                _boundViewModel.PropertyChanged -= OnChatViewModelPropertyChanged;
                _boundViewModel = null;
            }

            if (_editorLayoutState is not null)
            {
                _editorLayoutState.SettingsChanged -= OnEditorLayoutSettingsChanged;
            }

            _editorHost.Dispose();
        };
    }

    public void SetEditorLayoutState(IEditorLayoutState editorLayoutState)
    {
        if (_editorLayoutState is not null)
        {
            _editorLayoutState.SettingsChanged -= OnEditorLayoutSettingsChanged;
        }

        _editorLayoutState = editorLayoutState;
        _editorLayoutState.SettingsChanged += OnEditorLayoutSettingsChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged -= OnChatViewModelPropertyChanged;
        }

        _boundViewModel = DataContext as ChatViewModel;
        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged += OnChatViewModelPropertyChanged;
        }
    }

    private void OnChatViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ChatViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(ChatViewModel.EditorBody))
        {
            SyncEditorText(vm.EditorBody);
        }
    }

    private void OnEditorLayoutSettingsChanged(object? sender, EditorLayoutSettings settings)
    {
        if (!_hasAppliedInitialEditorLayout)
        {
            return;
        }

        _editorHost.ApplyRuntimeLayout(settings);
    }

    private void OnEditorTextViewScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (_isProgrammaticAutoScroll || _isUpdatingEditorFromViewModel)
        {
            return;
        }

        _isUserNearBottom = IsEditorNearBottom();
    }

    private void OnEditorTextViewVisualLinesChanged(object? sender, EventArgs e)
    {
        if (_isAutoScrollQueued)
        {
            ScheduleAutoScrollToEnd(_queuedAutoScrollOffset, _queuedAutoScrollVersion);
        }
    }

    private void OnChatEditorSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isAutoScrollQueued)
        {
            ScheduleAutoScrollToEnd(_queuedAutoScrollOffset, _queuedAutoScrollVersion);
        }
    }

    private bool IsEditorNearBottom()
    {
        var textView = ChatTextEditor.TextArea.TextView;
        var viewportHeight = GetEditorViewportHeight(textView);
        var documentHeight = textView.DocumentHeight;

        if (viewportHeight <= 0 || documentHeight <= 0)
        {
            return true;
        }

        var remainingDistance = Math.Max(0, documentHeight - (textView.ScrollOffset.Y + viewportHeight));
        return remainingDistance <= AutoScrollNearBottomThreshold;
    }

    private bool ShouldAutoScrollAfterSync()
    {
        var textView = ChatTextEditor.TextArea.TextView;
        if (textView.DocumentHeight <= 0 || GetEditorViewportHeight(textView) <= 0)
        {
            return true;
        }

        return _isUserNearBottom || IsEditorNearBottom();
    }

    private void ScheduleAutoScrollToEnd(int endOffset, int version)
    {
        _queuedAutoScrollOffset = endOffset;
        _queuedAutoScrollVersion = version;

        if (_isAutoScrollQueued)
        {
            return;
        }

        _isAutoScrollQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isAutoScrollQueued = false;
            ExecuteAutoScrollToEnd(_queuedAutoScrollOffset, _queuedAutoScrollVersion);
        }, DispatcherPriority.Render);
    }

    private void ExecuteAutoScrollToEnd(int endOffset, int version)
    {
        if (version != _autoScrollRequestVersion)
        {
            return;
        }

        var document = ChatTextEditor.Document;
        if (document is null)
        {
            return;
        }

        _isProgrammaticAutoScroll = true;
        try
        {
            AlignEditorToEnd(document, endOffset);

            _isUserNearBottom = true;
        }
        finally
        {
            _isProgrammaticAutoScroll = false;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (version != _autoScrollRequestVersion)
            {
                return;
            }

            var latestDocument = ChatTextEditor.Document;
            if (latestDocument is null)
            {
                return;
            }

            _isProgrammaticAutoScroll = true;
            try
            {
                AlignEditorToEnd(latestDocument, endOffset);
                _isUserNearBottom = true;
            }
            finally
            {
                _isProgrammaticAutoScroll = false;
            }
        }, DispatcherPriority.Render);
    }

    private void AlignEditorToEnd(AvaloniaEdit.Document.TextDocument document, int endOffset)
    {
        var textView = ChatTextEditor.TextArea.TextView;
        var viewportHeight = GetEditorViewportHeight(textView);
        var maxVerticalOffset = Math.Max(0, textView.DocumentHeight - viewportHeight);
        ChatTextEditor.ScrollToVerticalOffset(maxVerticalOffset);

        var clampedOffset = Math.Clamp(endOffset, 0, document.TextLength);
        ChatTextEditor.CaretOffset = clampedOffset;
        ChatTextEditor.Select(clampedOffset, 0);
        ChatTextEditor.TextArea.Caret.BringCaretToView();
    }

    private static double GetEditorViewportHeight(AvaloniaEdit.Rendering.TextView textView)
    {
        if (textView is IScrollable scrollable && scrollable.Viewport.Height > 0)
        {
            return scrollable.Viewport.Height;
        }

        return textView.Bounds.Height;
    }

    private void SyncEditorText(string text)
    {
        var changed = _editorHost.SyncFromViewModel(text, appendSuffixWhenPossible: true, out _);
        _isUpdatingEditorFromViewModel = _editorHost.IsUpdatingEditorFromViewModel;
        if (!changed)
        {
            return;
        }

        var shouldAutoScroll = ShouldAutoScrollAfterSync();

        if (!shouldAutoScroll)
        {
            _isUserNearBottom = false;
            return;
        }

        _autoScrollRequestVersion++;
        ScheduleAutoScrollToEnd(text.Length, _autoScrollRequestVersion);
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ChatViewModel vm)
        {
            return;
        }

        _editorHost.SyncToViewModel(() => vm.EditorBody, text => vm.EditorBody = text);
        _isUpdatingViewModelFromEditor = _editorHost.IsUpdatingViewModelFromEditor;
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not ChatViewModel vm)
        {
            return;
        }

        vm.UpdateMentionSuggestions(InputTextBox.Text ?? string.Empty, InputTextBox.CaretIndex);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _windowChrome.OnTitleBarPointerPressed(e);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _windowChrome.OnCloseClick();

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e) => _windowChrome.OnWindowPointerMoved(e);

    private void OnWindowPointerExited(object? sender, PointerEventArgs e) => _windowChrome.OnWindowPointerExited();

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) => _windowChrome.OnWindowPointerPressed(e);

    private void OnInputKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ChatViewModel vm) return;

        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (vm.SaveConversationCommand.CanExecute(null))
            {
                vm.SaveConversationCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
             if (vm.SendMessageCommand.CanExecute(null))
             {
                 vm.SendMessageCommand.Execute(null);
                 e.Handled = true;
             }
             return;
        }

        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
             if (vm.IsMentionPopupOpen)
             {
                 if (vm.TryAcceptMention(InputTextBox.Text ?? string.Empty, InputTextBox.CaretIndex, out var updatedText, out var updatedCaretIndex))
                 {
                     InputTextBox.Text = updatedText;
                     InputTextBox.CaretIndex = updatedCaretIndex;
                     InputTextBox.Focus();
                 }
                 e.Handled = true;
                 return;
             }
        }

        if (e.Key == Key.Down && vm.IsMentionPopupOpen)
        {
            vm.MoveMentionSelection(1);
            e.Handled = true;
            return;
        }
        
        if (e.Key == Key.Up && vm.IsMentionPopupOpen)
        {
            vm.MoveMentionSelection(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && vm.IsMentionPopupOpen)
        {
            vm.DismissMentionPopup();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Back or Key.Delete)
        {
            vm.UpdateMentionSuggestions(InputTextBox.Text ?? string.Empty, InputTextBox.CaretIndex);
        }
    }

    private void OnMentionDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatViewModel vm)
        {
            return;
        }

        if (vm.TryAcceptMention(InputTextBox.Text ?? string.Empty, InputTextBox.CaretIndex, out var updatedText, out var updatedCaretIndex))
        {
            InputTextBox.Text = updatedText;
            InputTextBox.CaretIndex = updatedCaretIndex;
            InputTextBox.Focus();
        }
    }
}

public class RoleToAlignmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var role = value?.ToString()?.ToLower();
        return role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
