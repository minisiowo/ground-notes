using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class ModalDialogRunnerTests
{
    [Fact]
    public async Task RunAsync_GenericReturnsDialogResultWhenRecoveryThrows()
    {
        var result = await ModalDialogRunner.RunAsync(
            () => Task.FromResult("result"),
            () => Task.FromException(new InvalidOperationException("recovery")));

        Assert.Equal("result", result);
    }

    [Fact]
    public async Task RunAsync_GenericPreservesDialogExceptionWhenRecoveryThrows()
    {
        var dialogException = new InvalidOperationException("dialog");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ModalDialogRunner.RunAsync<string>(
            () => Task.FromException<string>(dialogException),
            () => Task.FromException(new InvalidOperationException("recovery"))));

        Assert.Same(dialogException, exception);
    }

    [Fact]
    public async Task RunAsync_NonGenericRunsRecoveryAfterDialogCompletes()
    {
        var events = new List<string>();

        await ModalDialogRunner.RunAsync(
            () =>
            {
                events.Add("dialog");
                return Task.CompletedTask;
            },
            () =>
            {
                events.Add("recovery");
                return Task.CompletedTask;
            });

        Assert.Equal(["dialog", "recovery"], events);
    }

    [Fact]
    public async Task RunAsync_NonGenericPreservesDialogExceptionWhenRecoveryThrows()
    {
        var dialogException = new InvalidOperationException("dialog");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ModalDialogRunner.RunAsync(
            () => Task.FromException(dialogException),
            () => Task.FromException(new InvalidOperationException("recovery"))));

        Assert.Same(dialogException, exception);
    }

    [AvaloniaFact]
    public void RefreshResizeHints_XidHandleTogglesCanResizeAndPreservesMaximizeAndMinimize()
    {
        var owner = new Window
        {
            CanResize = true,
            CanMaximize = false,
            CanMinimize = false
        };
        var transitions = new List<bool>();
        owner.PropertyChanged += (_, args) =>
        {
            if (args.Property == Window.CanResizeProperty)
            {
                transitions.Add(owner.CanResize);
            }
        };

        ModalDialogRunner.RefreshResizeHints(owner, new FakePlatformHandle(new IntPtr(42), "XID"));

        Assert.Equal([false, true], transitions);
        Assert.True(owner.CanResize);
        Assert.False(owner.CanMaximize);
        Assert.False(owner.CanMinimize);
    }

    [AvaloniaFact]
    public void RefreshResizeHints_XidHandlePreservesEnabledMaximizeAndMinimize()
    {
        var owner = new Window
        {
            CanResize = true,
            CanMaximize = true,
            CanMinimize = true
        };

        ModalDialogRunner.RefreshResizeHints(owner, new FakePlatformHandle(new IntPtr(42), "XID"));

        Assert.True(owner.CanResize);
        Assert.True(owner.CanMaximize);
        Assert.True(owner.CanMinimize);
    }

    [AvaloniaTheory]
    [InlineData("WM_WINDOW_ROLE", 42)]
    [InlineData("XID", 0)]
    public void RefreshResizeHints_NonXidOrZeroHandleIsNoOp(string descriptor, long handle)
    {
        var owner = new Window { CanResize = true };
        var transitions = new List<bool>();
        owner.PropertyChanged += (_, args) =>
        {
            if (args.Property == Window.CanResizeProperty)
            {
                transitions.Add(owner.CanResize);
            }
        };

        ModalDialogRunner.RefreshResizeHints(owner, new FakePlatformHandle(new IntPtr(handle), descriptor));

        Assert.Empty(transitions);
        Assert.True(owner.CanResize);
    }

    [AvaloniaFact]
    public void RefreshResizeHints_NullHandleIsNoOp()
    {
        var owner = new Window { CanResize = true };
        ModalDialogRunner.RefreshResizeHints(owner, null);

        Assert.True(owner.CanResize);
    }

    [AvaloniaFact]
    public void RefreshResizeHints_NonResizableOwnerIsNoOp()
    {
        var owner = new Window { CanResize = false };
        ModalDialogRunner.RefreshResizeHints(owner, new FakePlatformHandle(new IntPtr(42), "XID"));

        Assert.False(owner.CanResize);
    }

    private sealed class FakePlatformHandle(IntPtr handle, string handleDescriptor) : IPlatformHandle
    {
        public IntPtr Handle { get; } = handle;

        public string HandleDescriptor { get; } = handleDescriptor;
    }
}
