using CommunityToolkit.Mvvm.Input;

namespace GroundNotes.ViewModels;

public partial class MainViewModel
{
    private const double CalendarPanelChromeHeight = 72;
    private const double CalendarWeekHeaderHeight = 18;
    private const double CalendarDayRowHeight = 31;

    [RelayCommand]
    private void ToggleCalendar()
    {
        IsCalendarExpanded = !IsCalendarExpanded;
    }

    [RelayCommand]
    private void ShowPreviousCalendarMonth()
    {
        DisplayedCalendarMonth = DisplayedCalendarMonth.AddMonths(-1);
    }

    [RelayCommand]
    private void ShowNextCalendarMonth()
    {
        DisplayedCalendarMonth = DisplayedCalendarMonth.AddMonths(1);
    }

    [RelayCommand]
    private void JumpCalendarToToday()
    {
        DisplayedCalendarMonth = GetMonthStart(DateTime.Today);
    }

    [RelayCommand]
    private void SelectCalendarDay(CalendarDayViewModel? day)
    {
        if (day is null)
        {
            return;
        }

        DisplayedCalendarMonth = GetMonthStart(day.Date);
        if (SelectedCalendarDate?.Date == day.Date.Date)
        {
            SelectedCalendarDate = null;
            return;
        }

        SelectedCalendarDate = day.Date.Date;
    }

    [RelayCommand]
    private void ClearDateFilter()
    {
        SelectedCalendarDate = null;
    }

    private void RefreshCalendarNoteDates()
    {
        _calendarNoteDates = _allNotes
            .Select(note => note.CreatedAt.Date)
            .ToHashSet();
    }

    private void RefreshCalendarDays()
    {
        var monthStart = GetMonthStart(DisplayedCalendarMonth);
        var firstGridDate = GetCalendarGridStart(monthStart);
        var weekRowCount = GetCalendarWeekRowCount(monthStart);
        var selectedDate = SelectedCalendarDate?.Date;
        var today = DateTime.Today;

        CalendarWeekRowCount = weekRowCount;

        VisibleCalendarDays = Enumerable.Range(0, weekRowCount * 7)
            .Select(offset =>
            {
                var date = firstGridDate.AddDays(offset);
                return new CalendarDayViewModel
                {
                    Date = date,
                    DayNumberText = date.Day.ToString(),
                    IsCurrentMonth = date.Month == monthStart.Month && date.Year == monthStart.Year,
                    IsToday = date.Date == today,
                    IsSelected = selectedDate == date.Date,
                    HasNotes = _calendarNoteDates.Contains(date.Date)
                };
            })
            .ToList();
    }

    private static DateTime GetMonthStart(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }

    private static DateTime GetCalendarGridStart(DateTime monthStart)
    {
        var offset = ((int)monthStart.DayOfWeek + 6) % 7;
        return monthStart.AddDays(-offset);
    }

    private static int GetCalendarWeekRowCount(DateTime monthStart)
    {
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var leadingDays = ((int)monthStart.DayOfWeek + 6) % 7;
        return (int)Math.Ceiling((leadingDays + daysInMonth) / 7d);
    }
}
