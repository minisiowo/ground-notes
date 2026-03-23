using System.Globalization;
using Avalonia.Data.Converters;

namespace GroundNotes.Converters;

public sealed class StringNotEmptyConverter : IValueConverter
{
    public static StringNotEmptyConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string text && !string.IsNullOrWhiteSpace(text);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
