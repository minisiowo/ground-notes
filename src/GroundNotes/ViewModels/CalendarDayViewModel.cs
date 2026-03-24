namespace GroundNotes.ViewModels;

public sealed class CalendarDayViewModel
{
    public required DateTime Date { get; init; }

    public required string DayNumberText { get; init; }

    public required bool IsCurrentMonth { get; init; }

    public required bool IsToday { get; init; }

    public required bool IsSelected { get; init; }

    public required bool HasNotes { get; init; }
}
