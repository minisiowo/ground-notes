using Avalonia;
using GroundNotes.Models;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class NoteWindowPlacementCalculatorTests
{
    [Fact]
    public void CreateDefaultLayout_UsesNarrowerWidthForZen()
    {
        var standard = NoteWindowPlacementCalculator.CreateDefaultLayout(NoteWindowMode.Standard, 1200, 800);
        var zen = NoteWindowPlacementCalculator.CreateDefaultLayout(NoteWindowMode.Zen, 1200, 800);

        Assert.Equal(1000, standard.Width);
        Assert.Equal(900, zen.Width);
        Assert.Equal(720, standard.Height);
        Assert.Equal(720, zen.Height);
    }

    [Fact]
    public void Calculate_CascadesWindowsFromOwner()
    {
        var workingArea = new PixelRect(0, 0, 1920, 1080);
        var layout = new NoteWindowLayout(800, 600);

        var first = NoteWindowPlacementCalculator.Calculate(workingArea, 1, new PixelPoint(100, 100), layout, 0);
        var second = NoteWindowPlacementCalculator.Calculate(workingArea, 1, new PixelPoint(100, 100), layout, 1);

        Assert.Equal(new PixelPoint(132, 132), first.Position);
        Assert.Equal(new PixelPoint(164, 164), second.Position);
        Assert.Equal(800, first.Width);
        Assert.Equal(600, first.Height);
    }

    [Fact]
    public void Calculate_ClampsOversizedWindowToWorkingArea()
    {
        var placement = NoteWindowPlacementCalculator.Calculate(
            new PixelRect(1920, 0, 1920, 1080),
            1,
            new PixelPoint(3600, 900),
            new NoteWindowLayout(3000, 2000),
            0);

        Assert.Equal(1920, placement.Width);
        Assert.Equal(1080, placement.Height);
        Assert.Equal(new PixelPoint(1920, 0), placement.Position);
    }

    [Fact]
    public void Calculate_UsesScreenScalingForPixelPosition()
    {
        var placement = NoteWindowPlacementCalculator.Calculate(
            new PixelRect(0, 0, 2000, 1600),
            2,
            new PixelPoint(100, 100),
            new NoteWindowLayout(800, 600),
            0);

        Assert.Equal(800, placement.Width);
        Assert.Equal(600, placement.Height);
        Assert.Equal(new PixelPoint(164, 164), placement.Position);
    }
}
