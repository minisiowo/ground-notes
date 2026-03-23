using Avalonia.Controls;
using Avalonia.Threading;

namespace GroundNotes.Views;

/// <summary>
/// Coalesces mention-popup refresh work to one UI-thread pass per frame (same pattern as
/// <see cref="SlashCommandPopupController.ScheduleRefresh"/>), so typing does not run note search per character.
/// </summary>
internal sealed class ChatMentionPopupController
{
    private readonly Action _refresh;
    private bool _isRefreshQueued;

    public ChatMentionPopupController(Action refresh)
    {
        _refresh = refresh;
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
            _refresh();
        }, priority ?? DispatcherPriority.Render);
    }
}
