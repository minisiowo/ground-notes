namespace GroundNotes.Models;

public sealed record NoteWindowLayout(double Width, double Height)
{
    private const double MinWidth = 500;
    private const double MinHeight = 350;
    private const double MaxDimension = 10000;

    public static NoteWindowLayout? Normalize(NoteWindowLayout? layout)
    {
        if (layout is null
            || !double.IsFinite(layout.Width)
            || !double.IsFinite(layout.Height)
            || layout.Width <= 0
            || layout.Height <= 0)
        {
            return null;
        }

        return new NoteWindowLayout(
            Math.Clamp(layout.Width, MinWidth, MaxDimension),
            Math.Clamp(layout.Height, MinHeight, MaxDimension));
    }
}
