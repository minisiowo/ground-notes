using Avalonia;

namespace GroundNotes.Views;

internal sealed class SidebarDragGhostPositionState
{
    private Point? _pendingPosition;
    private bool _callbackQueued;

    public bool Queue(Point position)
    {
        _pendingPosition = position;
        if (_callbackQueued)
        {
            return false;
        }

        _callbackQueued = true;
        return true;
    }

    public bool TryConsume(out Point position)
    {
        if (_pendingPosition is not { } pendingPosition)
        {
            position = default;
            _callbackQueued = false;
            return false;
        }

        position = pendingPosition;
        _pendingPosition = null;
        _callbackQueued = false;
        return true;
    }

    public void Reset()
    {
        _pendingPosition = null;
        _callbackQueued = false;
    }
}
