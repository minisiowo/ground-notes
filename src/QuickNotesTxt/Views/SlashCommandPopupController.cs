using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using QuickNotesTxt.Editors;

namespace QuickNotesTxt.Views;

internal sealed class SlashCommandPopupController
{
    private readonly TextEditor _editor;
    private readonly Border _editorBorder;
    private readonly Popup _popup;
    private readonly Border _popupContent;
    private readonly ListBox _listBox;
    private readonly TextBlock _hintText;
    private bool _isRefreshQueued;
    private bool _isPositionUpdateQueued;

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
        if (_isRefreshQueued)
        {
            return;
        }

        _isRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isRefreshQueued = false;
            Update();
        }, priority ?? DispatcherPriority.Render);
    }

    public void SchedulePositionUpdate(bool resetPlacement = false)
    {
        if (!_popup.IsOpen || _isPositionUpdateQueued)
        {
            return;
        }

        _isPositionUpdateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isPositionUpdateQueued = false;
            UpdatePosition(resetPlacement);
        }, DispatcherPriority.Render);
    }

    public void Close()
    {
        ActiveTrigger = null;
        ActiveCommands = [];
        _popup.IsOpen = false;
        _listBox.ItemsSource = null;
        _listBox.SelectedItem = null;
        _editor.Focus();
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
        _popup.IsOpen = true;
        _listBox.ItemsSource = commands;
        _listBox.SelectedItem = commands[0];
        _hintText.Text = string.IsNullOrWhiteSpace(trigger.Value.Query) ? "Formatting commands" : $"/{trigger.Value.Query}";
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
            var caretRect = _editor.TextArea.Caret.CalculateCaretRectangle();
            var popupTopLeft = _editor.TextArea.TextView.TranslatePoint(new Point(caretRect.X, caretRect.Y), _editorBorder);
            if (popupTopLeft is null || _editorBorder.Bounds.Width <= 0 || _editorBorder.Bounds.Height <= 0)
            {
                return;
            }

            const double edgePadding = 12;
            const double horizontalPadding = 4;
            const double verticalPadding = 8;
            const double preferredPopupWidth = 400;
            const double minPopupWidth = 120;
            const double preferredListHeight = 220;
            const double minListHeight = 80;
            const double popupChromeHeight = 54;

            var availableWidth = Math.Max(minPopupWidth, _editorBorder.Bounds.Width - (edgePadding * 2));
            var maxPopupWidth = Math.Clamp(preferredPopupWidth, minPopupWidth, availableWidth);
            var anchorLeft = popupTopLeft.Value.X;
            var anchorRight = popupTopLeft.Value.X + Math.Max(1, caretRect.Width);
            var anchorTop = popupTopLeft.Value.Y;
            var anchorBottom = popupTopLeft.Value.Y + Math.Max(1, caretRect.Height);
            var availableBelow = Math.Max(0, _editorBorder.Bounds.Height - anchorBottom - verticalPadding - edgePadding);
            var availableAbove = Math.Max(0, anchorTop - verticalPadding - edgePadding);
            var maxViewportHeight = Math.Max(minListHeight, Math.Max(availableBelow, availableAbove) - popupChromeHeight);
            _listBox.MaxHeight = Math.Min(preferredListHeight, maxViewportHeight);

            _popupContent.Width = double.NaN;
            _popupContent.Height = double.NaN;
            _popupContent.InvalidateMeasure();
            _popupContent.UpdateLayout();
            _popupContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var targetWidth = Math.Clamp(_popupContent.DesiredSize.Width + 2, minPopupWidth, maxPopupWidth);
            var targetHeight = Math.Min(maxViewportHeight + popupChromeHeight, _popupContent.DesiredSize.Height);
            _popupContent.Width = targetWidth;
            _popupContent.Height = targetHeight;

            var availableRight = Math.Max(0, _editorBorder.Bounds.Width - anchorRight - horizontalPadding - edgePadding);
            var availableLeft = Math.Max(0, anchorLeft - horizontalPadding - edgePadding);

            var (anchor, gravity, horizontalOffset, verticalOffset) = ChoosePlacement(
                targetWidth,
                targetHeight,
                availableBelow,
                availableAbove,
                availableRight,
                availableLeft,
                horizontalPadding,
                verticalPadding);

            if (resetPlacement)
            {
                _popup.PlacementRect = default;
            }

            _popup.PlacementAnchor = anchor;
            _popup.PlacementGravity = gravity;
            _popup.HorizontalOffset = horizontalOffset;
            _popup.VerticalOffset = verticalOffset;
            _popup.PlacementRect = new Rect(anchorLeft, anchorTop, Math.Max(1, caretRect.Width), Math.Max(1, caretRect.Height));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static (PopupAnchor anchor, PopupGravity gravity, double horizontalOffset, double verticalOffset) ChoosePlacement(
        double targetWidth,
        double targetHeight,
        double availableBelow,
        double availableAbove,
        double availableRight,
        double availableLeft,
        double horizontalPadding,
        double verticalPadding)
    {
        if (availableBelow >= targetHeight && availableRight >= targetWidth)
        {
            return (PopupAnchor.BottomRight, PopupGravity.BottomRight, horizontalPadding, verticalPadding);
        }

        if (availableBelow >= targetHeight && availableLeft >= targetWidth)
        {
            return (PopupAnchor.BottomLeft, PopupGravity.BottomLeft, -horizontalPadding, verticalPadding);
        }

        if (availableAbove >= targetHeight && availableRight >= targetWidth)
        {
            return (PopupAnchor.TopRight, PopupGravity.TopRight, horizontalPadding, -verticalPadding);
        }

        if (availableAbove >= targetHeight && availableLeft >= targetWidth)
        {
            return (PopupAnchor.TopLeft, PopupGravity.TopLeft, -horizontalPadding, -verticalPadding);
        }

        if (availableBelow >= availableAbove)
        {
            return availableRight >= availableLeft
                ? (PopupAnchor.BottomRight, PopupGravity.BottomRight, horizontalPadding, verticalPadding)
                : (PopupAnchor.BottomLeft, PopupGravity.BottomLeft, -horizontalPadding, verticalPadding);
        }

        return availableRight >= availableLeft
            ? (PopupAnchor.TopRight, PopupGravity.TopRight, horizontalPadding, -verticalPadding)
            : (PopupAnchor.TopLeft, PopupGravity.TopLeft, -horizontalPadding, -verticalPadding);
    }
}
