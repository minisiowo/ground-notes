using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using GroundNotes.Editors.Vim;
using GroundNotes.Models;
using GroundNotes.Services.KeySequences;
using GroundNotes.Services.KeySequences.Defaults;

namespace GroundNotes.Views;

internal sealed class VimEditorController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly VimWorkspaceState _workspaceState;
    private readonly VimEngine _engine = new();
    private readonly KeySequenceResolver _leaderResolver = GroundNotesKeySequenceMap.CreateResolver();
    private readonly DispatcherTimer _sequenceTimeoutTimer = new();
    private readonly DispatcherTimer _whichKeyTimer = new();
    private VimModeSettings _settings = VimModeSettings.Default;
    private KeySequenceResolution? _pendingLeaderResolution;
    private Func<KeyEventArgs, bool>? _preVimKeyHandler;
    private Func<string, Task>? _leaderCommandHandler;
    private object? _insertUndoDescriptor;
    private bool _insertUndoGroupOpen;
    private bool _leaderCommandExecuting;
    private bool _disposed;

    public VimEditorController(TextEditor editor, VimWorkspaceState workspaceState)
    {
        _editor = editor;
        _workspaceState = workspaceState;
        _sequenceTimeoutTimer.Tick += OnSequenceTimeout;
        _whichKeyTimer.Tick += OnWhichKeyDelayElapsed;
        _editor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;
    }

    public event EventHandler<VimStatusChangedEventArgs>? StatusChanged;

    public VimMode Mode => _engine.Mode;

    public bool IsEnabled => _settings.IsEnabled;

    internal void BeginExternalInsertUndoGroup()
    {
        if (_settings.IsEnabled && _engine.Mode == VimMode.Insert)
        {
            BeginInsertUndoGroup();
        }
    }

    internal void EndExternalInsertUndoGroup()
    {
        if (_settings.IsEnabled && _engine.Mode == VimMode.Insert)
        {
            EndInsertUndoGroup();
        }
    }

    internal bool ProcessSpecialKey(VimKey key) => ProcessVimInput(VimInput.Special(key));

    public void SetSettings(VimModeSettings? settings)
    {
        var normalized = VimModeSettings.Normalize(settings);
        if (normalized == _settings)
        {
            PublishStatus();
            return;
        }

        _settings = normalized;
        _sequenceTimeoutTimer.Interval = TimeSpan.FromMilliseconds(_settings.KeySequenceTimeoutMilliseconds);
        _whichKeyTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, _settings.WhichKeyDelayMilliseconds));
        ResetState();
    }

    public void SetPreVimKeyHandler(Func<KeyEventArgs, bool>? handler)
    {
        _preVimKeyHandler = handler;
    }

    public void SetLeaderCommandHandler(Func<string, Task>? handler)
    {
        _leaderCommandHandler = handler;
    }

    public void ResetState()
    {
        StopLeaderTimers();
        EndInsertUndoGroup();
        _insertUndoDescriptor = null;
        _leaderResolver.Reset();
        _pendingLeaderResolution = null;
        _engine.Reset();
        SetCaret(_editor.CaretOffset);
        PublishStatus();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_disposed || !_settings.IsEnabled || e.Handled)
        {
            return;
        }

        if (_engine.Mode == VimMode.Insert)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            if (_preVimKeyHandler?.Invoke(e) == true)
            {
                return;
            }

            e.Handled = ProcessVimInput(VimInput.Special(VimKey.Escape));
            return;
        }

        if (_leaderResolver.IsPending)
        {
            if (e.Key == Key.Escape)
            {
                CancelLeader();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                var resolution = _leaderResolver.Backspace();
                if (_leaderResolver.IsPending)
                {
                    ScheduleLeaderFeedback(resolution);
                }
                else
                {
                    StopLeaderTimers();
                    _pendingLeaderResolution = null;
                    PublishStatus();
                }

                e.Handled = true;
                return;
            }
        }

        var normalizedModifiers = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta);
        if (_settings.UseStandardCtrlBindings && normalizedModifiers == KeyModifiers.Control)
        {
            if (e.Key == Key.W && _engine.Mode == VimMode.Normal)
            {
                ResolveLeaderStroke(new KeyStroke("w", KeyStrokeModifiers.Control));
                e.Handled = true;
                return;
            }

            if (e.Key == Key.R)
            {
                e.Handled = ProcessVimInput(VimInput.Special(VimKey.CtrlR));
                return;
            }

            if (e.Key == Key.B)
            {
                ProcessSyntheticSequence("20k");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                ProcessSyntheticSequence("20j");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.U)
            {
                ProcessSyntheticSequence("10k");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D)
            {
                ProcessSyntheticSequence("10j");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.C)
            {
                ProcessVimInput(VimInput.Special(VimKey.Escape));
                e.Handled = true;
                return;
            }

            if (e.Key is Key.I or Key.V or Key.X)
            {
                PublishStatus($"Ctrl+{e.Key} is not implemented in Vim mode yet");
                e.Handled = true;
                return;
            }
        }

        if (normalizedModifiers != KeyModifiers.None)
        {
            return;
        }

        VimInput? input = e.Key switch
        {
            Key.Escape => VimInput.Special(VimKey.Escape),
            Key.Left => VimInput.Printable('h'),
            Key.Down => VimInput.Printable('j'),
            Key.Up => VimInput.Printable('k'),
            Key.Right => VimInput.Printable('l'),
            Key.Home => VimInput.Printable('0'),
            Key.End => VimInput.Printable('$'),
            Key.Delete => VimInput.Printable('x'),
            Key.Enter => VimInput.Special(VimKey.Enter),
            Key.Back => VimInput.Special(VimKey.Backspace),
            Key.Tab => VimInput.Special(VimKey.Tab),
            _ => null
        };

        if (input is null)
        {
            return;
        }

        var handled = ProcessVimInput(input.Value);
        e.Handled = handled || _engine.Mode != VimMode.Insert;
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_disposed || !_settings.IsEnabled || e.Handled || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (_engine.Mode == VimMode.Insert)
        {
            BeginInsertUndoGroup();
            return;
        }

        e.Handled = true;
        if (e.Text.Length != 1 || char.IsControl(e.Text[0]))
        {
            PublishStatus("Unsupported Normal-mode input");
            return;
        }

        var character = e.Text[0];
        if (_engine.Mode == VimMode.Normal && (_leaderResolver.IsPending || IsLeaderCharacter(character)))
        {
            var stroke = !_leaderResolver.IsPending && IsLeaderCharacter(character)
                ? new KeyStroke("space")
                : KeyStroke.FromCharacter(character);
            ResolveLeaderStroke(stroke);
            return;
        }

        ProcessVimInput(VimInput.Printable(character));
    }

    private bool ProcessVimInput(VimInput input)
    {
        var document = _editor.Document;
        if (document is null)
        {
            return false;
        }

        _engine.ImportRegister(_workspaceState.Register);
        var result = _engine.Process(input, new VimDocumentSnapshot(document.Text, Math.Clamp(_editor.CaretOffset, 0, document.TextLength)));
        if (result.PreviousMode != VimMode.Insert && result.Mode == VimMode.Insert)
        {
            _insertUndoDescriptor = new object();
        }

        ApplyOperations(result.Operations);
        if (result.PreviousMode == VimMode.Insert && result.Mode != VimMode.Insert)
        {
            EndInsertUndoGroup();
            _insertUndoDescriptor = null;
        }
        _workspaceState.Register = _engine.Register;
        PublishStatus();
        return result.IsHandled;
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        EndInsertUndoGroup();
    }

    private void BeginInsertUndoGroup()
    {
        var document = _editor.Document;
        if (document is null || _insertUndoDescriptor is null || _insertUndoGroupOpen)
        {
            return;
        }

        if (ReferenceEquals(document.UndoStack.LastGroupDescriptor, _insertUndoDescriptor))
        {
            document.UndoStack.StartContinuedUndoGroup(_insertUndoDescriptor);
        }
        else
        {
            document.UndoStack.StartUndoGroup(_insertUndoDescriptor);
        }
        _insertUndoGroupOpen = true;
    }

    private void EndInsertUndoGroup()
    {
        if (!_insertUndoGroupOpen)
        {
            return;
        }

        _insertUndoGroupOpen = false;
        _editor.Document?.UndoStack.EndUndoGroup();
    }

    private void ProcessSyntheticSequence(string sequence)
    {
        foreach (var character in sequence)
        {
            ProcessVimInput(VimInput.Printable(character));
        }
    }

    private void ApplyOperations(IReadOnlyList<VimOperation> operations)
    {
        var document = _editor.Document;
        if (document is null)
        {
            return;
        }

        int? activeCaretOffset = Math.Clamp(_editor.CaretOffset, 0, document.TextLength);
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case VimMoveCaretOperation move:
                    activeCaretOffset = Math.Clamp(move.Offset, 0, document.TextLength);
                    SetCaret(activeCaretOffset.Value);
                    break;
                case VimSetSelectionOperation selection:
                    var selectionStart = Math.Clamp(selection.Start, 0, document.TextLength);
                    var selectionLength = Math.Clamp(selection.Length, 0, document.TextLength - selectionStart);
                    _editor.Select(selectionStart, selectionLength);
                    if (activeCaretOffset is { } visualCaretOffset)
                    {
                        _editor.CaretOffset = visualCaretOffset;
                    }
                    break;
                case VimClearSelectionOperation:
                    SetCaret(_editor.CaretOffset);
                    break;
                case VimTextEditOperation edit:
                    var ownsInsertGroup = _engine.Mode == VimMode.Insert
                        && _insertUndoDescriptor is not null
                        && !_insertUndoGroupOpen;
                    if (ownsInsertGroup)
                    {
                        document.UndoStack.StartUndoGroup(_insertUndoDescriptor);
                        _insertUndoGroupOpen = true;
                    }

                    try
                    {
                        using (document.RunUpdate())
                        {
                            var start = Math.Clamp(edit.Start, 0, document.TextLength);
                            var length = Math.Clamp(edit.Length, 0, document.TextLength - start);
                            document.Replace(start, length, edit.NewText);
                            SetCaret(edit.NewCaretOffset);
                        }
                    }
                    finally
                    {
                        if (ownsInsertGroup)
                        {
                            EndInsertUndoGroup();
                        }
                    }
                    break;
                case VimSetRegisterOperation setRegister:
                    _workspaceState.Register = setRegister.Register;
                    break;
                case VimHistoryOperation history:
                    for (var index = 0; index < Math.Max(1, history.Count); index++)
                    {
                        if (history.Action == VimHistoryAction.Undo)
                        {
                            _editor.Undo();
                        }
                        else
                        {
                            _editor.Redo();
                        }
                    }
                    break;
            }
        }

        _editor.TextArea.Caret.BringCaretToView();
    }

    private void SetCaret(int offset)
    {
        var length = _editor.Document?.TextLength ?? 0;
        var clamped = Math.Clamp(offset, 0, length);
        _editor.Select(clamped, 0);
        _editor.CaretOffset = clamped;
    }

    private void ResolveLeaderStroke(KeyStroke stroke)
    {
        var resolution = _leaderResolver.Resolve(stroke);
        switch (resolution.Kind)
        {
            case KeySequenceResolutionKind.Prefix:
                ScheduleLeaderFeedback(resolution);
                break;
            case KeySequenceResolutionKind.Command:
                StopLeaderTimers();
                _pendingLeaderResolution = null;
                PublishStatus();
                if (!string.IsNullOrWhiteSpace(resolution.ActionId))
                {
                    _ = ExecuteLeaderCommandAsync(resolution.ActionId);
                }
                break;
            default:
                StopLeaderTimers();
                _pendingLeaderResolution = null;
                PublishStatus("Unknown leader sequence");
                break;
        }
    }

    private async Task ExecuteLeaderCommandAsync(string actionId)
    {
        if (_leaderCommandHandler is null || _leaderCommandExecuting || _disposed)
        {
            return;
        }

        _leaderCommandExecuting = true;
        try
        {
            await _leaderCommandHandler(actionId);
        }
        catch (Exception ex)
        {
            PublishStatus($"Command failed: {ex.Message}");
        }
        finally
        {
            _leaderCommandExecuting = false;
        }
    }

    private bool IsLeaderCharacter(char character)
    {
        if (string.Equals(_settings.LeaderKey, VimModeSettings.DefaultLeaderKey, StringComparison.OrdinalIgnoreCase))
        {
            return character == ' ';
        }

        return _settings.LeaderKey.Length == 1 && character == _settings.LeaderKey[0];
    }

    private void ScheduleLeaderFeedback(KeySequenceResolution resolution)
    {
        _pendingLeaderResolution = resolution;
        _sequenceTimeoutTimer.Stop();
        _sequenceTimeoutTimer.Start();
        _whichKeyTimer.Stop();
        if (_settings.WhichKeyDelayMilliseconds == 0)
        {
            PublishLeaderStatus(resolution);
        }
        else
        {
            _whichKeyTimer.Start();
            PublishStatus();
        }
    }

    private void OnSequenceTimeout(object? sender, EventArgs e)
    {
        CancelLeader();
    }

    private void OnWhichKeyDelayElapsed(object? sender, EventArgs e)
    {
        _whichKeyTimer.Stop();
        if (_pendingLeaderResolution is { } resolution && _leaderResolver.IsPending)
        {
            PublishLeaderStatus(resolution);
        }
    }

    private void CancelLeader()
    {
        StopLeaderTimers();
        _leaderResolver.Reset();
        _pendingLeaderResolution = null;
        PublishStatus();
    }

    private void StopLeaderTimers()
    {
        _sequenceTimeoutTimer.Stop();
        _whichKeyTimer.Stop();
    }

    private void PublishLeaderStatus(KeySequenceResolution resolution)
    {
        var options = string.Join("  ", resolution.Continuations.Select(option =>
            $"{option.Display} {option.Description ?? option.ActionId}"));
        PublishStatus($"{resolution.DisplayBreadcrumb}  {options}".TrimEnd());
    }

    private void PublishStatus(string? message = null)
    {
        _editor.TextArea.CaretShape = _settings.IsEnabled && _engine.Mode != VimMode.Insert
            ? CaretShape.Block
            : CaretShape.Bar;

        var mode = _engine.Mode switch
        {
            VimMode.Insert => "INSERT",
            VimMode.OperatorPending => "OPERATOR",
            VimMode.Visual => "VISUAL",
            VimMode.VisualLine => "VISUAL LINE",
            _ => "NORMAL"
        };
        var count = _engine.PendingCount is { } pendingCount ? $" {pendingCount}" : string.Empty;
        var text = string.IsNullOrWhiteSpace(message)
            ? $"-- {mode}{count} --"
            : $"-- {mode}{count} --  {message}";
        StatusChanged?.Invoke(this, new VimStatusChangedEventArgs(text, _settings.IsEnabled && _settings.ShowStatus));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopLeaderTimers();
        EndInsertUndoGroup();
        _sequenceTimeoutTimer.Tick -= OnSequenceTimeout;
        _whichKeyTimer.Tick -= OnWhichKeyDelayElapsed;
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnEditorKeyDown);
        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.TextArea.TextEntered -= OnTextEntered;
    }
}

internal sealed class VimStatusChangedEventArgs(string text, bool isVisible) : EventArgs
{
    public string Text { get; } = text;

    public bool IsVisible { get; } = isVisible;
}
