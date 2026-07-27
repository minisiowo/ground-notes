using GroundNotes.Models;
using Xunit;

namespace GroundNotes.Tests;

public sealed class VimModeSettingsTests
{
    [Fact]
    public void Default_UsesSafeOptInVimConfiguration()
    {
        var settings = VimModeSettings.Default;

        Assert.False(settings.IsEnabled);
        Assert.Equal("<Space>", settings.LeaderKey);
        Assert.Equal(1000, settings.KeySequenceTimeoutMilliseconds);
        Assert.Equal(220, settings.WhichKeyDelayMilliseconds);
        Assert.True(settings.UseStandardCtrlBindings);
        Assert.Equal(VimClipboardMode.ExplicitSystemRegister, settings.ClipboardMode);
        Assert.True(settings.ShowStatus);
    }

    [Fact]
    public void Normalize_NullReturnsDefaults()
    {
        Assert.Equal(VimModeSettings.Default, VimModeSettings.Normalize(null));
    }

    [Fact]
    public void Normalize_CanonicalizesLeaderAndClampsDelays()
    {
        var settings = VimModeSettings.Default with
        {
            LeaderKey = "  space  ",
            KeySequenceTimeoutMilliseconds = 25,
            WhichKeyDelayMilliseconds = 5001
        };

        var normalized = VimModeSettings.Normalize(settings);

        Assert.Equal("<Space>", normalized.LeaderKey);
        Assert.Equal(VimModeSettings.MinKeySequenceTimeoutMilliseconds, normalized.KeySequenceTimeoutMilliseconds);
        Assert.Equal(VimModeSettings.MaxWhichKeyDelayMilliseconds, normalized.WhichKeyDelayMilliseconds);
    }

    [Fact]
    public void Normalize_PreservesValidCustomValuesAndRepairsUnknownClipboardMode()
    {
        var settings = VimModeSettings.Default with
        {
            LeaderKey = " , ",
            KeySequenceTimeoutMilliseconds = 2500,
            WhichKeyDelayMilliseconds = 0,
            ClipboardMode = (VimClipboardMode)999
        };

        var normalized = VimModeSettings.Normalize(settings);

        Assert.Equal(",", normalized.LeaderKey);
        Assert.Equal(2500, normalized.KeySequenceTimeoutMilliseconds);
        Assert.Equal(0, normalized.WhichKeyDelayMilliseconds);
        Assert.Equal(VimClipboardMode.ExplicitSystemRegister, normalized.ClipboardMode);
    }
}
