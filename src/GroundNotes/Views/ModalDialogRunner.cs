using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Diagnostics;

namespace GroundNotes.Views;

internal static class ModalDialogRunner
{
    public static async Task<TResult> ShowAsync<TResult>(Window dialog, Window owner)
    {
        return await RunAsync(
            () => dialog.ShowDialog<TResult>(owner),
            () => RefreshResizeHintsAsync(owner));
    }

    public static async Task ShowAsync(Window dialog, Window owner)
    {
        await RunAsync(
            () => dialog.ShowDialog(owner),
            () => RefreshResizeHintsAsync(owner));
    }

    internal static async Task<TResult> RunAsync<TResult>(Func<Task<TResult>> dialog, Func<Task> recovery)
    {
        try
        {
            return await dialog();
        }
        finally
        {
            await RunRecoveryBestEffortAsync(recovery);
        }
    }

    internal static async Task RunAsync(Func<Task> dialog, Func<Task> recovery)
    {
        try
        {
            await dialog();
        }
        finally
        {
            await RunRecoveryBestEffortAsync(recovery);
        }
    }

    internal static void RefreshResizeHints(Window owner, IPlatformHandle? platformHandle)
    {
        if (!owner.CanResize
            || platformHandle is null
            || platformHandle.Handle == IntPtr.Zero
            || !string.Equals(platformHandle.HandleDescriptor, "XID", StringComparison.Ordinal))
        {
            return;
        }

        // Avalonia 11.3.12 X11 fixes owner min/max hints while a modal is open; Niri/XWayland can retain them, so this toggle rewrites hints after cleanup.
        owner.CanResize = false;
        owner.CanResize = true;
    }

    private static async Task RefreshResizeHintsAsync(Window owner)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!owner.IsVisible)
            {
                return;
            }

            RefreshResizeHints(owner, owner.TryGetPlatformHandle());
        }, DispatcherPriority.Background);
    }

    private static async Task RunRecoveryBestEffortAsync(Func<Task> recovery)
    {
        try
        {
            await recovery();
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Modal dialog resize recovery failed: {exception.Message}");
        }
    }
}
