using Avalonia;
using GroundNotes.Models;

namespace GroundNotes.Views;

internal readonly record struct NoteWindowPlacement(
    double Width,
    double Height,
    PixelPoint Position);

internal static class NoteWindowPlacementCalculator
{
    private const double CascadeOffset = 32;
    private const int CascadeCycleLength = 8;

    public static NoteWindowLayout CreateDefaultLayout(
        NoteWindowMode mode,
        double ownerWidth,
        double ownerHeight)
    {
        var widthFactor = mode == NoteWindowMode.Zen ? 0.75 : 0.85;
        var layout = new NoteWindowLayout(
            Math.Clamp(ownerWidth * widthFactor, 560, 1000),
            Math.Clamp(ownerHeight * 0.9, 480, 900));
        return NoteWindowLayout.Normalize(layout)!;
    }

    public static NoteWindowPlacement Calculate(
        PixelRect workingArea,
        double screenScaling,
        PixelPoint ownerPosition,
        NoteWindowLayout requestedLayout,
        int cascadeIndex)
    {
        var scaling = double.IsFinite(screenScaling) && screenScaling > 0 ? screenScaling : 1;
        var availableWidth = workingArea.Width / scaling;
        var availableHeight = workingArea.Height / scaling;
        var width = Math.Min(requestedLayout.Width, availableWidth);
        var height = Math.Min(requestedLayout.Height, availableHeight);
        var pixelWidth = Math.Max(1, (int)Math.Round(width * scaling));
        var pixelHeight = Math.Max(1, (int)Math.Round(height * scaling));
        var cascadeStep = (Math.Abs(cascadeIndex) % CascadeCycleLength) + 1;
        var offset = (int)Math.Round(CascadeOffset * scaling * cascadeStep);
        var requestedX = ownerPosition.X + offset;
        var requestedY = ownerPosition.Y + offset;
        var maxX = workingArea.Right - pixelWidth;
        var maxY = workingArea.Bottom - pixelHeight;

        var x = requestedX >= workingArea.X && requestedX <= maxX
            ? requestedX
            : workingArea.X + Math.Max(0, (workingArea.Width - pixelWidth) / 2);
        var y = requestedY >= workingArea.Y && requestedY <= maxY
            ? requestedY
            : workingArea.Y + Math.Max(0, (workingArea.Height - pixelHeight) / 2);

        return new NoteWindowPlacement(width, height, new PixelPoint(x, y));
    }
}
