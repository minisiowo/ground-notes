using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using GroundNotes.Editors;

namespace GroundNotes.Views;

internal sealed class EditorMarkdownTableController : IDisposable
{
    private readonly TextEditor _editor;
    private bool _markdownFormattingEnabled = true;
    private Func<bool>? _canHandleTextInput;
    private Action? _beginHandledTextInput;
    private Action? _endHandledTextInput;
    private bool _isCoercingCaret;
    private bool _isApplyingTableEdit;
    private int? _sessionTableStart;
    private int _sessionRowIndex;
    private int _sessionColumnIndex;
    private int _sessionCaretOffset;
    private string? _sessionBuffer;
    private (int TableStart, int RowIndex, int ColumnIndex)? _emptyCellSelectAllArmed;

    public EditorMarkdownTableController(TextEditor editor)
    {
        _editor = editor;
        _editor.TextArea.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _editor.TextChanged += OnEditorTextChanged;
    }

    public bool IsEnabled => _markdownFormattingEnabled;

    public bool IsCaretInTable
        => _markdownFormattingEnabled
           && _editor.Document is { } document
           && MarkdownTableEditingCommands.IsInTable(document.Text, _editor.CaretOffset);

    public bool ShouldHandlePaste => SelectionTouchesTable;

    public bool SelectionTouchesTable
    {
        get
        {
            if (!_markdownFormattingEnabled || _editor.Document is not { } document)
            {
                return false;
            }

            if (_editor.SelectionLength == 0)
            {
                return MarkdownTableEditingCommands.IsInTable(document.Text, _editor.CaretOffset);
            }

            var selectionStart = _editor.SelectionStart;
            var selectionEnd = selectionStart + _editor.SelectionLength;
            return MarkdownTableParser.FindTables(document.Text)
                .Any(table => selectionStart < table.Start + table.Length && selectionEnd > table.Start);
        }
    }

    public void SetMarkdownFormattingEnabled(bool enabled) => _markdownFormattingEnabled = enabled;

    internal void SetCanHandleTextInput(Func<bool> canHandleTextInput)
    {
        _canHandleTextInput = canHandleTextInput;
    }

    public void SetTextInputCoordination(
        Func<bool> canHandleTextInput,
        Action beginHandledTextInput,
        Action endHandledTextInput)
    {
        _canHandleTextInput = canHandleTextInput;
        _beginHandledTextInput = beginHandledTextInput;
        _endHandledTextInput = endHandledTextInput;
    }

    public bool TryFormat() => TryApply(MarkdownTableEditingCommands.TryFormat);

    public bool TryInsertText(string text)
    {
        CommitAndClearSession();
        if (!_markdownFormattingEnabled || _editor.Document is not { } document)
        {
            return false;
        }

        if (MarkdownTableEditingCommands.TryInsertCellText(
            document.Text,
            _editor.SelectionStart,
            _editor.SelectionLength,
            text,
            out var cellEdit))
        {
            ApplyEdit(cellEdit);
            return true;
        }

        if (!MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(
            document.Text,
            _editor.SelectionStart,
            _editor.SelectionLength))
        {
            return false;
        }

        var replacement = MarkdownTableFormatter.FormatAll(text);
        ApplyEdit(new MarkdownEditResult(
            _editor.SelectionStart,
            _editor.SelectionLength,
            replacement,
            _editor.SelectionStart + replacement.Length,
            0));
        return true;
    }

    public bool TryInsertRow(bool above)
        => TryApply((string text, int offset, out MarkdownEditResult edit) => MarkdownTableEditingCommands.TryInsertRow(text, offset, above, out edit));

    public bool TryDeleteRow() => TryApply(MarkdownTableEditingCommands.TryDeleteRow);

    public bool CanDeleteSelection
        => _editor.SelectionLength != 0
           && _editor.Document is { } document
           && (MarkdownTableEditingCommands.TryDeleteCellSelection(
                   document.Text,
                   _editor.SelectionStart,
                   _editor.SelectionLength,
                   out _)
               || MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(
                   document.Text,
                   _editor.SelectionStart,
                   _editor.SelectionLength));

    public bool TryDeleteSelection()
        => _editor.SelectionLength != 0 && TryHandleCellDeletion(backwards: true);

    public bool TryApplyExternalTextEdit(int start, int length, string newText, int caretOffset)
    {
        if (!_markdownFormattingEnabled || _editor.Document is not { } document)
        {
            return false;
        }

        var editStart = Math.Clamp(start, 0, document.TextLength);
        var editLength = Math.Clamp(length, 0, document.TextLength - editStart);
        var end = editStart + editLength;
        var touchedTables = MarkdownTableParser.FindTables(document.Text)
            .Where(table => editLength == 0
                ? editStart >= table.Start && editStart <= table.Start + table.Length
                : editStart < table.Start + table.Length && end > table.Start)
            .ToList();
        if (touchedTables.Count == 0)
        {
            return false;
        }

        ClearSession();
        if (MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(
            document.Text,
            editStart,
            editLength))
        {
            var replacement = MarkdownTableFormatter.FormatAll(newText);
            ApplyEdit(new MarkdownEditResult(editStart, editLength, replacement, editStart + replacement.Length, 0));
            return true;
        }

        if (touchedTables.Count == 1 && string.IsNullOrEmpty(newText))
        {
            var table = touchedTables[0];
            var deletedRow = table.Rows.FirstOrDefault(row => row.Index >= 2
                && editStart == row.Start
                && end >= row.Start + row.Length);
            if (deletedRow is not null
                && MarkdownTableEditingCommands.TryDeleteRow(document.Text, deletedRow.Cells[0].EditableStart, out var rowEdit))
            {
                ApplyEdit(rowEdit);
                return true;
            }
        }

        var rawEdit = new MarkdownEditResult(
            editStart,
            editLength,
            newText,
            Math.Clamp(caretOffset, editStart, editStart + newText.Length),
            0);
        if (touchedTables.Count == 1
            && MarkdownTableEditingCommands.TryAdaptCellEdit(document.Text, rawEdit, out var tableEdit))
        {
            ApplyEdit(tableEdit);
        }

        return true;
    }

    public bool TryMoveRow(bool down)
        => TryApply((string text, int offset, out MarkdownEditResult edit) => MarkdownTableEditingCommands.TryMoveRow(text, offset, down, out edit));

    public bool TryInsertColumn(bool before)
        => TryApply((string text, int offset, out MarkdownEditResult edit) => MarkdownTableEditingCommands.TryInsertColumn(text, offset, before, out edit));

    public bool TryDeleteColumn() => TryApply(MarkdownTableEditingCommands.TryDeleteColumn);

    public bool TryMoveColumn(bool right)
        => TryApply((string text, int offset, out MarkdownEditResult edit) => MarkdownTableEditingCommands.TryMoveColumn(text, offset, right, out edit));



    public void Dispose()
    {
        _editor.TextArea.RemoveHandler(InputElement.KeyDownEvent, OnEditorKeyDown);
        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.TextChanged -= OnEditorTextChanged;
        ClearSession();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled
            || !_markdownFormattingEnabled
            || _editor.Document is null)
        {
            return;
        }

        var isSelectAll = e.Key == Key.A
                          && e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta;
        if (isSelectAll)
        {
            if (_canHandleTextInput?.Invoke() != false && TryHandleSelectAll())
            {
                e.Handled = true;
            }

            return;
        }

        _emptyCellSelectAllArmed = null;

        if (e.Key is Key.Back or Key.Delete
            && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Control)
        {
            if (TryHandleCellDeletion(
                backwards: e.Key == Key.Back,
                byWord: e.KeyModifiers == KeyModifiers.Control))
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            CommitAndClearSession();
            if (MarkdownTableEditingCommands.TryNavigate(
                _editor.Document.Text,
                _editor.CaretOffset,
                backwards: e.KeyModifiers == KeyModifiers.Shift,
                out var edit))
            {
                ApplyEdit(edit);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            CommitAndClearSession();
            if (MarkdownTableEditingCommands.TryHandleEnter(
                _editor.Document.Text,
                _editor.CaretOffset,
                above: e.KeyModifiers == KeyModifiers.Shift,
                out var edit))
            {
                ApplyEdit(edit);
                e.Handled = true;
            }
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _emptyCellSelectAllArmed = null;
        if (!_isApplyingTableEdit)
        {
            ClearSession();
        }
    }

    internal bool TryHandleSelectAll()
    {
        if (!_markdownFormattingEnabled || _editor.Document is not { } document)
        {
            return false;
        }

        var tables = MarkdownTableParser.FindTables(document.Text);
        if (_editor.SelectionStart == 0 && _editor.SelectionLength == document.TextLength)
        {
            return false;
        }

        foreach (var table in tables)
        {
            if (_editor.SelectionStart == table.Start && _editor.SelectionLength == table.Length)
            {
                _emptyCellSelectAllArmed = null;
                _editor.Select(0, document.TextLength);
                _editor.CaretOffset = document.TextLength;
                return true;
            }
        }

        foreach (var table in tables)
        {
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                if (row.IsDelimiter)
                {
                    continue;
                }

                for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                {
                    var cell = row.Cells[columnIndex];
                    var isArmedEmptyCell = cell.ContentLength == 0
                                           && _editor.SelectionLength == 0
                                           && _emptyCellSelectAllArmed == (table.Start, rowIndex, columnIndex)
                                           && _editor.CaretOffset == cell.EditableStart;
                    var isSelectedCell = cell.ContentLength > 0
                                         && _editor.SelectionStart == cell.EditableStart
                                         && _editor.SelectionLength == cell.ContentLength;
                    if (!isArmedEmptyCell && !isSelectedCell)
                    {
                        continue;
                    }

                    _emptyCellSelectAllArmed = null;
                    _editor.Select(table.Start, table.Length);
                    _editor.CaretOffset = table.Start + table.Length;
                    return true;
                }
            }
        }

        if (!MarkdownTableParser.TryFindTableAtOffset(document.Text, _editor.CaretOffset, out var caretTable)
            || !caretTable.TryGetCellAtOffset(_editor.CaretOffset, out var position))
        {
            _emptyCellSelectAllArmed = null;
            return false;
        }

        var caretCell = caretTable.Rows[position.RowIndex].Cells[position.ColumnIndex];
        _editor.Select(caretCell.EditableStart, caretCell.ContentLength);
        _editor.CaretOffset = caretCell.EditableStart + caretCell.ContentLength;
        _emptyCellSelectAllArmed = caretCell.ContentLength == 0
            ? (caretTable.Start, position.RowIndex, position.ColumnIndex)
            : null;
        return true;
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_isCoercingCaret || _isApplyingTableEdit || !_markdownFormattingEnabled || _editor.SelectionLength != 0 || _editor.Document is not { } document)
        {
            return;
        }

        if (_sessionTableStart is int tableStart && _sessionBuffer is not null)
        {
            var activeTable = MarkdownTableParser.FindTables(document.Text).FirstOrDefault(table => table.Start == tableStart);
            if (activeTable is not null
                && activeTable.TryGetCellAtOffset(_editor.CaretOffset, out var activePosition)
                && activePosition.RowIndex == _sessionRowIndex
                && activePosition.ColumnIndex == _sessionColumnIndex)
            {
                var cell = activeTable.Rows[_sessionRowIndex].Cells[_sessionColumnIndex];
                _sessionCaretOffset = Math.Clamp(_editor.CaretOffset - cell.SegmentStart - 1, 0, _sessionBuffer.Length);
                return;
            }

            var requestedOffset = _editor.CaretOffset;
            MarkdownTableCellPosition? requestedCell = activeTable is not null
                && activeTable.TryGetCellAtOffset(requestedOffset, out var requestedPosition)
                    ? requestedPosition
                    : null;
            var requestedAnchor = requestedCell is null ? document.CreateAnchor(requestedOffset) : null;
            CommitAndClearSession();
            if (requestedCell is { } semanticTarget)
            {
                var committedTable = MarkdownTableParser.FindTables(document.Text).FirstOrDefault(table => table.Start == tableStart);
                if (committedTable is not null
                    && semanticTarget.RowIndex >= 0
                    && semanticTarget.RowIndex < committedTable.Rows.Count
                    && semanticTarget.ColumnIndex >= 0
                    && semanticTarget.ColumnIndex < committedTable.ColumnCount)
                {
                    var targetCell = committedTable.Rows[semanticTarget.RowIndex].Cells[semanticTarget.ColumnIndex];
                    requestedOffset = targetCell.SegmentStart + Math.Min(targetCell.SegmentLength, 1 + semanticTarget.ContentOffset);
                }
            }
            else if (requestedAnchor is not null)
            {
                requestedOffset = requestedAnchor.IsDeleted
                    ? Math.Clamp(requestedOffset, 0, document.TextLength)
                    : requestedAnchor.Offset;
            }
            _isCoercingCaret = true;
            try
            {
                _editor.CaretOffset = requestedOffset;
                _editor.Select(requestedOffset, 0);
            }
            finally
            {
                _isCoercingCaret = false;
            }
        }

        if (!MarkdownTableEditingCommands.TryGetEditableCaretOffset(document.Text, _editor.CaretOffset, out var editableOffset)
            || editableOffset == _editor.CaretOffset)
        {
            return;
        }

        _isCoercingCaret = true;
        try
        {
            _editor.CaretOffset = editableOffset;
            _editor.Select(editableOffset, 0);
        }
        finally
        {
            _isCoercingCaret = false;
        }
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Handled
            || !_markdownFormattingEnabled
            || _canHandleTextInput?.Invoke() == false
            || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (_editor.SelectionLength != 0)
        {
            if (_editor.Document is not { } selectionDocument || !SelectionTouchesTable)
            {
                return;
            }

            if (MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(
                selectionDocument.Text,
                _editor.SelectionStart,
                _editor.SelectionLength))
            {
                ClearSession();
                return;
            }

            e.Handled = true;
            _beginHandledTextInput?.Invoke();
            try
            {
                if (MarkdownTableEditingCommands.TryInsertCellText(
                    selectionDocument.Text,
                    _editor.SelectionStart,
                    _editor.SelectionLength,
                    e.Text,
                    out var selectionEdit))
                {
                    ClearSession();
                    ApplyEdit(selectionEdit);
                }
            }
            finally
            {
                _endHandledTextInput?.Invoke();
            }

            return;
        }

        if (!EnsureSession())
        {
            return;
        }

        e.Handled = true;
        var normalizedInput = MarkdownTableEditingCommands.NormalizeCellInput(e.Text);
        _sessionBuffer = _sessionBuffer!.Insert(_sessionCaretOffset, normalizedInput);
        _sessionCaretOffset += normalizedInput.Length;
        ApplySessionBufferWithUndoCoordination();
    }

    private bool EnsureSession()
    {
        if (_editor.Document is not { } document)
        {
            return false;
        }

        if (_sessionTableStart is int existingStart && _sessionBuffer is not null)
        {
            var existingTable = MarkdownTableParser.FindTables(document.Text).FirstOrDefault(table => table.Start == existingStart);
            if (existingTable is not null
                && _sessionRowIndex >= 0
                && _sessionRowIndex < existingTable.Rows.Count
                && _sessionColumnIndex >= 0
                && _sessionColumnIndex < existingTable.ColumnCount)
            {
                return true;
            }

            ClearSession();
        }

        if (!MarkdownTableParser.TryFindTableAtOffset(document.Text, _editor.CaretOffset, out var table)
            || !table.TryGetCellAtOffset(_editor.CaretOffset, out var position))
        {
            return false;
        }

        var cell = table.Rows[position.RowIndex].Cells[position.ColumnIndex];
        _sessionTableStart = table.Start;
        _sessionRowIndex = position.RowIndex;
        _sessionColumnIndex = position.ColumnIndex;
        _sessionBuffer = cell.Content;
        _sessionCaretOffset = Math.Clamp(position.ContentOffset, 0, _sessionBuffer.Length);
        return true;
    }

    private void ApplySessionBufferWithUndoCoordination()
    {
        if (_editor.Document is not { } document
            || _sessionTableStart is not int tableStart
            || _sessionBuffer is null
            || !MarkdownTableEditingCommands.TrySetCellContent(
                document.Text,
                tableStart,
                _sessionRowIndex,
                _sessionColumnIndex,
                _sessionBuffer,
                _sessionCaretOffset,
                out var edit))
        {
            ClearSession();
            return;
        }

        _beginHandledTextInput?.Invoke();
        try
        {
            ApplyEdit(edit);
        }
        finally
        {
            _endHandledTextInput?.Invoke();
        }
    }

    internal bool TryHandleCellDeletion(bool backwards, bool byWord = false)
    {
        if (_editor.Document is not { } document)
        {
            return false;
        }

        if (_editor.SelectionLength != 0)
        {
            if (!SelectionTouchesTable)
            {
                return false;
            }

            ClearSession();
            if (!MarkdownTableEditingCommands.TryDeleteCellSelection(
                    document.Text,
                    _editor.SelectionStart,
                    _editor.SelectionLength,
                    out var selectionEdit)
                && MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(
                    document.Text,
                    _editor.SelectionStart,
                    _editor.SelectionLength))
            {
                selectionEdit = new MarkdownEditResult(
                    _editor.SelectionStart,
                    _editor.SelectionLength,
                    string.Empty,
                    _editor.SelectionStart,
                    0);
            }

            if (selectionEdit.Length != 0 || selectionEdit.Replacement.Length != 0)
            {
                _beginHandledTextInput?.Invoke();
                try
                {
                    ApplyEdit(selectionEdit);
                }
                finally
                {
                    _endHandledTextInput?.Invoke();
                }
            }

            return true;
        }

        if (!EnsureSession() || _sessionBuffer is null)
        {
            return false;
        }

        if (_sessionCaretOffset == 0 && backwards)
        {
            if (byWord)
            {
                return true;
            }

            var table = MarkdownTableParser.FindTables(document.Text).FirstOrDefault(candidate => candidate.Start == _sessionTableStart);
            var isEmptyRow = table is not null
                             && _sessionRowIndex >= 2
                             && table.Rows[_sessionRowIndex].Cells.Select((cell, index) => index == _sessionColumnIndex ? _sessionBuffer : cell.Content)
                                 .All(static content => string.IsNullOrWhiteSpace(content));
            if (_sessionColumnIndex == 0 && isEmptyRow)
            {
                var caretOffset = _editor.CaretOffset;
                ClearSession();
                if (MarkdownTableEditingCommands.TryDeleteRow(document.Text, caretOffset, out var deleteRowEdit))
                {
                    ApplyEdit(deleteRowEdit);
                }

                return true;
            }

            return true;
        }

        MarkdownTableEditingCommands.DeleteCellContent(
            _sessionBuffer,
            _sessionCaretOffset,
            0,
            backwards,
            byWord,
            out _sessionBuffer,
            out _sessionCaretOffset);
        ApplySessionBufferWithUndoCoordination();
        return true;
    }

    private void CommitAndClearSession()
    {
        if (_sessionTableStart is int tableStart && _sessionBuffer is not null && _editor.Document is { } document)
        {
            var committed = _sessionBuffer.Trim();
            var caret = Math.Min(_sessionCaretOffset, committed.Length);
            if (!string.Equals(committed, _sessionBuffer, StringComparison.Ordinal)
                && MarkdownTableEditingCommands.TrySetCellContent(
                    document.Text,
                    tableStart,
                    _sessionRowIndex,
                    _sessionColumnIndex,
                    committed,
                    caret,
                    out var edit))
            {
                ApplyEdit(edit);
            }
        }

        ClearSession();
    }

    private void ClearSession()
    {
        _sessionTableStart = null;
        _sessionRowIndex = 0;
        _sessionColumnIndex = 0;
        _sessionCaretOffset = 0;
        _sessionBuffer = null;
    }

    private bool TryApply(TableEditFactory factory)
    {
        CommitAndClearSession();
        if (!_markdownFormattingEnabled || _editor.Document is null)
        {
            return false;
        }

        if (!factory(_editor.Document.Text, _editor.CaretOffset, out var edit))
        {
            return false;
        }

        ApplyEdit(edit);
        return true;
    }

    private void ApplyEdit(MarkdownEditResult edit)
    {
        var document = _editor.Document;
        if (document is null)
        {
            return;
        }

        _isApplyingTableEdit = true;
        try
        {
            var start = Math.Clamp(edit.Start, 0, document.TextLength);
            var length = Math.Clamp(edit.Length, 0, document.TextLength - start);
            var replacement = edit.Replacement ?? string.Empty;
            var changesDocument = !string.Equals(document.GetText(start, length), replacement, StringComparison.Ordinal);
            if (changesDocument)
            {
                using (document.RunUpdate())
                {
                    document.Replace(start, length, replacement);
                }
            }

            var selectionStart = Math.Clamp(edit.SelectionStart, 0, document.TextLength);
            var selectionLength = Math.Clamp(edit.SelectionLength, 0, document.TextLength - selectionStart);
            _editor.Select(selectionStart, selectionLength);
            _editor.CaretOffset = selectionStart + selectionLength;
            _editor.Focus();
        }
        finally
        {
            _isApplyingTableEdit = false;
        }
    }

    private delegate bool TableEditFactory(string text, int caretOffset, out MarkdownEditResult edit);
}
