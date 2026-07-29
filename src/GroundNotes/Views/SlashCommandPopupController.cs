using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using GroundNotes.Editors;

namespace GroundNotes.Views;

internal sealed class SlashCommandPopupController : IDisposable
{
    private const double EdgePadding = 12;
    private const double HorizontalPadding = 4;
    private const double VerticalPadding = 8;
    private const double PreferredPopupWidth = 400;
    private const double PreferredListHeight = 220;
    private const double PopupChromeHeight = 54;

    private readonly TextEditor _editor;
    private readonly Border _editorBorder;
    private readonly Popup _popup;
    private readonly Border _popupContent;
    private readonly ListBox _listBox;
    private readonly TextBlock _hintText;
    private bool _isRefreshQueued;
    private bool _isPositionUpdateQueued;
    private bool _needsPlacementReset;
    private bool _isDisposed;
    private SlashPopupVerticalPlacement? _verticalPlacement;
    private SlashPopupHorizontalPlacement? _horizontalPlacement;

    public SlashCommandPopupController(
        TextEditor editor,
        Border editorBorder,
        Popup popup,
        Border popupContent,
        ListBox listBox,
        TextBlock hintText)
    {
        _editor = editor;
        _editorBorder = editorBorder;
        _popup = popup;
        _popupContent = popupContent;
        _listBox = listBox;
        _hintText = hintText;
    }

    public MarkdownSlashTrigger? ActiveTrigger { get; private set; }

    public IReadOnlyList<MarkdownSlashCommand> ActiveCommands { get; private set; } = [];

    public bool HandleKeyDown(KeyEventArgs e, Action<MarkdownEditResult> applyEdit)
    {
        if (!_popup.IsOpen)
        {
            return false;
        }

        if (!ShouldHandleNavigationKey(e.Key, e.KeyModifiers))
        {
            return false;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            MoveSelection(1);
            return true;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            MoveSelection(-1);
            return true;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplySelectedCommand(applyEdit);
            return true;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return true;
        }

        return false;
    }

    internal static bool ShouldHandleNavigationKey(Key key, KeyModifiers modifiers)
    {
        return key == Key.Escape
               || (modifiers == KeyModifiers.None && key is Key.Down or Key.Up or Key.Enter);
    }

    public void ApplySelectedCommand(Action<MarkdownEditResult> applyEdit)
    {
        if (ActiveTrigger is not { } trigger || _listBox.SelectedItem is not MarkdownSlashCommand command)
        {
            Close();
            return;
        }

        var document = _editor.Document;
        if (document is null)
        {
            Close();
            return;
        }

        if (command.Action == SlashCommandAction.FormatTable
            && !MarkdownTableEditingCommands.IsInTable(document.Text, trigger.Start))
        {
            Close();
            return;
        }

        document.Replace(trigger.Start, trigger.Length, string.Empty);
        _editor.CaretOffset = trigger.Start;
        _editor.Select(trigger.Start, 0);
        var commandOffset = trigger.Start;

        var edit = command.Action switch
        {
            SlashCommandAction.Bold => MarkdownEditingCommands.ToggleWrap(document.Text, commandOffset, 0, "**"),
            SlashCommandAction.Italic => MarkdownEditingCommands.ToggleWrap(document.Text, commandOffset, 0, "*"),
            SlashCommandAction.InlineCode => MarkdownEditingCommands.ToggleWrap(document.Text, commandOffset, 0, "`"),
            SlashCommandAction.CodeBlock => MarkdownEditingCommands.ToggleCodeBlock(document.Text, commandOffset, 0),
            SlashCommandAction.TaskList => MarkdownEditingCommands.ToggleTaskList(document.Text, commandOffset, 0),
            SlashCommandAction.BulletList => MarkdownEditingCommands.ToggleBulletList(document.Text, commandOffset, 0),
            SlashCommandAction.Table => MarkdownTableEditingCommands.InsertTable(document.Text, commandOffset, 0),
            SlashCommandAction.FormatTable when MarkdownTableEditingCommands.TryFormat(document.Text, commandOffset, out var tableEdit) => tableEdit,
            SlashCommandAction.Heading1 => MarkdownEditingCommands.ToggleHeading(document.Text, commandOffset, 0, 1),
            SlashCommandAction.Heading2 => MarkdownEditingCommands.ToggleHeading(document.Text, commandOffset, 0, 2),
            SlashCommandAction.Heading3 => MarkdownEditingCommands.ToggleHeading(document.Text, commandOffset, 0, 3),
            _ => default
        };

        Close();
        applyEdit(edit);
    }

    public void ScheduleRefresh(DispatcherPriority? priority = null)
    {
        if (_isDisposed || _isRefreshQueued)
        {
            return;
        }

        _isRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isRefreshQueued = false;
            if (!_isDisposed)
            {
                Update();
            }
        }, priority ?? DispatcherPriority.Render);
    }

    public void SchedulePositionUpdate(bool resetPlacement = false)
    {
        if (_isDisposed || !_popup.IsOpen)
        {
            return;
        }

        _needsPlacementReset |= resetPlacement;
        if (_isPositionUpdateQueued)
        {
            return;
        }

        _isPositionUpdateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isPositionUpdateQueued = false;
            if (_isDisposed)
            {
                return;
            }

            var shouldResetPlacement = _needsPlacementReset;
            _needsPlacementReset = false;
            UpdatePosition(shouldResetPlacement);
        }, DispatcherPriority.Render);
    }

    public void Close()
    {
        var wasOpen = _popup.IsOpen;
        ActiveTrigger = null;
        ActiveCommands = [];
        _verticalPlacement = null;
        _horizontalPlacement = null;
        _needsPlacementReset = false;
        _popup.IsOpen = false;
        _listBox.ItemsSource = null;
        _listBox.SelectedItem = null;
        if (wasOpen && !_isDisposed)
        {
            _editor.Focus();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ActiveTrigger = null;
        ActiveCommands = [];
        _verticalPlacement = null;
        _horizontalPlacement = null;
        _popup.IsOpen = false;
        _listBox.ItemsSource = null;
        _listBox.SelectedItem = null;
    }

    private void Update()
    {
        var trigger = MarkdownSlashCommandCatalog.TryGetTrigger(_editor.Document?.Text ?? string.Empty, _editor.CaretOffset);
        if (trigger is null)
        {
            Close();
            return;
        }

        var commands = MarkdownSlashCommandCatalog.Filter(trigger.Value.Query);
        if (commands.Count == 0)
        {
            Close();
            return;
        }

        var wasVisible = _popup.IsOpen;
        if (wasVisible && ActiveCommands.Count == commands.Count && ActiveCommands.Zip(commands, (a, b) => a.Id == b.Id).All(static x => x))
        {
            ActiveTrigger = trigger;
            _hintText.Text = string.IsNullOrWhiteSpace(trigger.Value.Query) ? "Formatting commands" : $"/{trigger.Value.Query}";
            SchedulePositionUpdate();
            return;
        }

        ActiveTrigger = trigger;
        ActiveCommands = commands;
        if (!wasVisible)
        {
            _popupContent.Width = Math.Max(1, Math.Min(PreferredPopupWidth, _editorBorder.Bounds.Width - (EdgePadding * 2)));
            _listBox.MaxHeight = PreferredListHeight;
        }

        _listBox.ItemsSource = commands;
        _listBox.SelectedItem = commands[0];
        _hintText.Text = string.IsNullOrWhiteSpace(trigger.Value.Query) ? "Formatting commands" : $"/{trigger.Value.Query}";
        _popup.IsOpen = true;
        _editor.Focus();
        SchedulePositionUpdate(!wasVisible);
    }

    private void MoveSelection(int delta)
    {
        if (ActiveCommands.Count == 0)
        {
            return;
        }

        var currentIndex = _listBox.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        currentIndex = (currentIndex + delta + ActiveCommands.Count) % ActiveCommands.Count;
        _listBox.SelectedIndex = currentIndex;
        if (_listBox.SelectedItem is not null)
        {
            _listBox.ScrollIntoView(_listBox.SelectedItem);
        }
    }

    private void UpdatePosition(bool resetPlacement)
    {
        if (!_popup.IsOpen)
        {
            return;
        }

        try
        {
            var textView = _editor.TextArea.TextView;
            var caretRect = _editor.TextArea.Caret.CalculateCaretRectangle();
            var viewportCaretTopLeft = caretRect.Position - textView.ScrollOffset;
            var popupTopLeft = textView.TranslatePoint(viewportCaretTopLeft, _editorBorder);
            if (popupTopLeft is null || _editorBorder.Bounds.Width <= 0 || _editorBorder.Bounds.Height <= 0)
            {
                return;
            }

            var anchorLeft = popupTopLeft.Value.X;
            var anchorTop = popupTopLeft.Value.Y;
            var anchorWidth = Math.Max(1, caretRect.Width);
            var anchorHeight = Math.Max(1, caretRect.Height);
            var layout = CalculateLayout(
                _editorBorder.Bounds.Width,
                _editorBorder.Bounds.Height,
                anchorLeft,
                anchorTop,
                anchorWidth,
                anchorHeight,
                _popupContent.DesiredSize.Height,
                resetPlacement ? null : _verticalPlacement,
                resetPlacement ? null : _horizontalPlacement);

            _verticalPlacement = layout.VerticalPlacement;
            _horizontalPlacement = layout.HorizontalPlacement;
            _popupContent.Width = layout.PopupWidth;
            _popupContent.Height = double.NaN;
            _listBox.MaxHeight = layout.ListMaxHeight;

            var (anchor, gravity, horizontalOffset, verticalOffset) = ToPopupPlacement(layout);
            if (resetPlacement)
            {
                _popup.PlacementRect = default;
            }

            _popup.PlacementAnchor = anchor;
            _popup.PlacementGravity = gravity;
            _popup.HorizontalOffset = horizontalOffset;
            _popup.VerticalOffset = verticalOffset;
            _popup.PlacementRect = new Rect(anchorLeft, anchorTop, anchorWidth, anchorHeight);
        }
        catch (InvalidOperationException)
        {
        }
    }

    internal static SlashCommandPopupLayout CalculateLayout(
        double editorWidth,
        double editorHeight,
        double anchorLeft,
        double anchorTop,
        double anchorWidth,
        double anchorHeight,
        double desiredPopupHeight,
        SlashPopupVerticalPlacement? currentVerticalPlacement = null,
        SlashPopupHorizontalPlacement? currentHorizontalPlacement = null)
    {
        var anchorRight = anchorLeft + Math.Max(1, anchorWidth);
        var anchorBottom = anchorTop + Math.Max(1, anchorHeight);
        var availableBelow = Math.Max(0, editorHeight - anchorBottom - VerticalPadding - EdgePadding);
        var availableAbove = Math.Max(0, anchorTop - VerticalPadding - EdgePadding);
        var availableRight = Math.Max(0, editorWidth - anchorRight - HorizontalPadding - EdgePadding);
        var availableLeft = Math.Max(0, anchorLeft - HorizontalPadding - EdgePadding);
        var popupWidth = Math.Max(1, Math.Min(PreferredPopupWidth, editorWidth - (EdgePadding * 2)));
        var requiredHeight = desiredPopupHeight > 0
            ? Math.Min(PopupChromeHeight + PreferredListHeight, desiredPopupHeight)
            : PopupChromeHeight + PreferredListHeight;

        var verticalPlacement = currentVerticalPlacement switch
        {
            SlashPopupVerticalPlacement.Below when availableBelow >= Math.Min(requiredHeight, PopupChromeHeight) => SlashPopupVerticalPlacement.Below,
            SlashPopupVerticalPlacement.Above when availableAbove >= Math.Min(requiredHeight, PopupChromeHeight) => SlashPopupVerticalPlacement.Above,
            _ when availableBelow >= requiredHeight => SlashPopupVerticalPlacement.Below,
            _ when availableAbove >= requiredHeight => SlashPopupVerticalPlacement.Above,
            _ when availableBelow >= availableAbove => SlashPopupVerticalPlacement.Below,
            _ => SlashPopupVerticalPlacement.Above
        };

        var horizontalPlacement = currentHorizontalPlacement switch
        {
            SlashPopupHorizontalPlacement.Right when availableRight >= Math.Min(popupWidth, 120) => SlashPopupHorizontalPlacement.Right,
            SlashPopupHorizontalPlacement.Left when availableLeft >= Math.Min(popupWidth, 120) => SlashPopupHorizontalPlacement.Left,
            _ when availableRight >= popupWidth => SlashPopupHorizontalPlacement.Right,
            _ when availableLeft >= popupWidth => SlashPopupHorizontalPlacement.Left,
            _ when availableRight >= availableLeft => SlashPopupHorizontalPlacement.Right,
            _ => SlashPopupHorizontalPlacement.Left
        };

        var availableHeight = verticalPlacement == SlashPopupVerticalPlacement.Below
            ? availableBelow
            : availableAbove;
        var listMaxHeight = Math.Min(PreferredListHeight, Math.Max(0, availableHeight - PopupChromeHeight));

        return new SlashCommandPopupLayout(popupWidth, listMaxHeight, verticalPlacement, horizontalPlacement);
    }

    private static (PopupAnchor anchor, PopupGravity gravity, double horizontalOffset, double verticalOffset) ToPopupPlacement(
        SlashCommandPopupLayout layout)
    {
        return (layout.VerticalPlacement, layout.HorizontalPlacement) switch
        {
            (SlashPopupVerticalPlacement.Below, SlashPopupHorizontalPlacement.Right) =>
                (PopupAnchor.BottomRight, PopupGravity.BottomRight, HorizontalPadding, VerticalPadding),
            (SlashPopupVerticalPlacement.Below, SlashPopupHorizontalPlacement.Left) =>
                (PopupAnchor.BottomLeft, PopupGravity.BottomLeft, -HorizontalPadding, VerticalPadding),
            (SlashPopupVerticalPlacement.Above, SlashPopupHorizontalPlacement.Right) =>
                (PopupAnchor.TopRight, PopupGravity.TopRight, HorizontalPadding, -VerticalPadding),
            _ => (PopupAnchor.TopLeft, PopupGravity.TopLeft, -HorizontalPadding, -VerticalPadding)
        };
    }
}

internal enum SlashPopupVerticalPlacement
{
    Below,
    Above
}

internal enum SlashPopupHorizontalPlacement
{
    Right,
    Left
}

internal readonly record struct SlashCommandPopupLayout(
    double PopupWidth,
    double ListMaxHeight,
    SlashPopupVerticalPlacement VerticalPlacement,
    SlashPopupHorizontalPlacement HorizontalPlacement);
