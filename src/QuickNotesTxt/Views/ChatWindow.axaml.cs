using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using QuickNotesTxt.Models;
using QuickNotesTxt.Editors;
using QuickNotesTxt.Styles;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Views;

public partial class ChatWindow : Window
{
    private const double WindowResizeBorderThickness = 6;
    private const double WindowCornerResizeThickness = 10;
    private const double AutoScrollNearBottomThreshold = 48;

    private WindowEdge? _activeResizeEdge;
    private bool _isUpdatingEditorFromViewModel;
    private bool _isUpdatingViewModelFromEditor;
    private bool _isProgrammaticAutoScroll;
    private bool _isAutoScrollQueued;
    private bool _isUserNearBottom = true;
    private int _autoScrollRequestVersion;
    private int _queuedAutoScrollVersion;
    private int _queuedAutoScrollOffset;
    private readonly MarkdownColorizingTransformer _markdownColorizer = new();
    private ChatViewModel? _boundViewModel;

    public ChatWindow()
    {
        InitializeComponent();

        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);

        _markdownColorizer.RedrawRequested += (_, startLine) =>
        {
            var document = ChatTextEditor.Document;
            if (document is null || startLine > document.LineCount)
            {
                return;
            }

            var startLineSegment = document.GetLineByNumber(startLine);
            var lastLineSegment = document.GetLineByNumber(document.LineCount);
            
            ChatTextEditor.TextArea.TextView.Redraw(startLineSegment.Offset, lastLineSegment.EndOffset - startLineSegment.Offset);
        };

        ChatTextEditor.Options.ConvertTabsToSpaces = false;
        ChatTextEditor.Options.EnableRectangularSelection = false;
        ChatTextEditor.Options.WordWrapIndentation = 0;
        ChatTextEditor.Options.InheritWordWrapIndentation = false;
        ChatTextEditor.TextArea.TextView.LineTransformers.Add(_markdownColorizer);
        ChatTextEditor.TextArea.TextView.ScrollOffsetChanged += OnEditorTextViewScrollOffsetChanged;
        ChatTextEditor.TextArea.TextView.VisualLinesChanged += OnEditorTextViewVisualLinesChanged;
        ChatTextEditor.SizeChanged += OnChatEditorSizeChanged;
        ApplyEditorSelectionTheme();
        
        ChatTextEditor.TextChanged += OnEditorTextChanged;
        
        DataContextChanged += OnDataContextChanged;
        
        Opened += (s, e) =>
        {
            InputTextBox.Focus();
            ApplyEditorSelectionTheme();
            if (DataContext is ChatViewModel vm)
            {
                SyncEditorText(vm.EditorBody);
            }
        };

        Activated += (_, _) =>
        {
            _markdownColorizer.InvalidateResourceCache();
            ApplyEditorSelectionTheme();
            ChatTextEditor.TextArea.TextView.InvalidateVisual();
        };
    }

    private void ApplyEditorSelectionTheme()
    {
        var resources = Application.Current?.Resources;
        ChatTextEditor.TextArea.SelectionBrush = resources?[ThemeKeys.EditorTextSelectionBrush] as IBrush;
        ChatTextEditor.TextArea.SelectionForeground = null;
        ChatTextEditor.TextArea.SelectionBorder = null;
        ChatTextEditor.TextArea.SelectionCornerRadius = 0;
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
        if (_isUpdatingViewModelFromEditor)
        {
            return;
        }

        var document = ChatTextEditor.Document;
        if (document is null)
        {
            return;
        }

        text ??= string.Empty;
        var currentText = document.Text;
        if (string.Equals(currentText, text, StringComparison.Ordinal))
        {
            return;
        }

        var shouldAutoScroll = ShouldAutoScrollAfterSync();

        _isUpdatingEditorFromViewModel = true;
        try
        {
            if (text.StartsWith(currentText, StringComparison.Ordinal))
            {
                var suffix = text[currentText.Length..];
                if (suffix.Length > 0)
                {
                    document.Insert(document.TextLength, suffix);
                }
            }
            else
            {
                document.Replace(0, document.TextLength, text);
            }
        }
        finally
        {
            _isUpdatingEditorFromViewModel = false;
        }

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
        if (_isUpdatingEditorFromViewModel || DataContext is not ChatViewModel vm) return;

        var text = ChatTextEditor.Document?.Text ?? string.Empty;
        if (string.Equals(vm.EditorBody, text, StringComparison.Ordinal)) return;

        _isUpdatingViewModelFromEditor = true;
        try
        {
            vm.EditorBody = text;
        }
        finally
        {
            _isUpdatingViewModelFromEditor = false;
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && _activeResizeEdge == null)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_activeResizeEdge != null) return;

        var edge = GetEdgeAtPoint(e.GetPosition(this));
        _activeResizeEdge = null; // Don't lock it yet, just set cursor.

        Cursor = edge switch
        {
            WindowEdge.North => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.West => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default
        };
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        Cursor = Cursor.Default;
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var edge = GetEdgeAtPoint(e.GetPosition(this));
        if (edge != null)
        {
            _activeResizeEdge = edge;
            BeginResizeDrag(edge.Value, e);
            _activeResizeEdge = null;
            e.Handled = true;
        }
    }

    private WindowEdge? GetEdgeAtPoint(Point point)
    {
        bool north = point.Y <= WindowResizeBorderThickness;
        bool south = point.Y >= Bounds.Height - WindowResizeBorderThickness;
        bool west = point.X <= WindowResizeBorderThickness;
        bool east = point.X >= Bounds.Width - WindowResizeBorderThickness;

        bool cornerNorth = point.Y <= WindowCornerResizeThickness;
        bool cornerSouth = point.Y >= Bounds.Height - WindowCornerResizeThickness;
        bool cornerWest = point.X <= WindowCornerResizeThickness;
        bool cornerEast = point.X >= Bounds.Width - WindowCornerResizeThickness;

        if (cornerNorth && cornerWest) return WindowEdge.NorthWest;
        if (cornerNorth && cornerEast) return WindowEdge.NorthEast;
        if (cornerSouth && cornerWest) return WindowEdge.SouthWest;
        if (cornerSouth && cornerEast) return WindowEdge.SouthEast;

        if (north) return WindowEdge.North;
        if (south) return WindowEdge.South;
        if (west) return WindowEdge.West;
        if (east) return WindowEdge.East;

        return null;
    }

    private void OnInputKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ChatViewModel vm) return;

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
             if (MentionPopup.IsOpen)
             {
                 AcceptMention();
                 e.Handled = true;
                 return;
             }
        }

        if (e.Key == Key.Down && MentionPopup.IsOpen)
        {
            if (MentionListBox.Items.Count > 0)
            {
                MentionListBox.SelectedIndex = (MentionListBox.SelectedIndex + 1) % (MentionListBox.Items.Count);
            }
            e.Handled = true;
            return;
        }
        
        if (e.Key == Key.Up && MentionPopup.IsOpen)
        {
            if (MentionListBox.Items.Count > 0)
            {
                MentionListBox.SelectedIndex = (MentionListBox.SelectedIndex - 1 + MentionListBox.Items.Count) % (MentionListBox.Items.Count);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && MentionPopup.IsOpen)
        {
            MentionPopup.IsOpen = false;
            e.Handled = true;
            return;
        }

        CheckForMention(InputTextBox.Text ?? "", InputTextBox.CaretIndex);
    }

    private void CheckForMention(string text, int caretIndex)
    {
        if (DataContext is not ChatViewModel vm) return;

        if (caretIndex <= 0)
        {
            MentionPopup.IsOpen = false;
            return;
        }

        var lastAt = text.LastIndexOf('@', caretIndex - 1);
        if (lastAt >= 0)
        {
            var query = text.Substring(lastAt + 1, caretIndex - (lastAt + 1));
            if (lastAt == 0 || char.IsWhiteSpace(text[lastAt - 1]))
            {
                var results = vm.SearchNotes(query);
                if (results.Any())
                {
                    MentionListBox.ItemsSource = results;
                    MentionListBox.SelectedIndex = 0;
                    MentionPopup.IsOpen = true;
                    return;
                }
            }
        }

        MentionPopup.IsOpen = false;
    }

    private void OnMentionDoubleTapped(object? sender, RoutedEventArgs e)
    {
        AcceptMention();
    }

    private void AcceptMention()
    {
        if (DataContext is not ChatViewModel vm || MentionListBox.SelectedItem is not NoteSummary note) return;

        var text = InputTextBox.Text ?? "";
        var caretIndex = InputTextBox.CaretIndex;
        var lastAt = text.LastIndexOf('@', Math.Max(0, caretIndex - 1));

        if (lastAt >= 0)
        {
            var newText = text.Remove(lastAt, caretIndex - lastAt);
            InputTextBox.Text = newText;
            InputTextBox.CaretIndex = lastAt;
            vm.InputText = newText;
            
            vm.AddNoteToContextCommand.Execute(note);
        }

        MentionPopup.IsOpen = false;
        InputTextBox.Focus();
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
