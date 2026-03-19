using System.Globalization;

namespace QuickNotesTxt.Models;

public static class EditorDisplaySettings
{
    public const int DefaultIndentSize = 4;
    public const double DefaultLineHeightFactor = 1.15;

    public static IReadOnlyList<int> SupportedIndentSizes { get; } = [2, 4];

    public static IReadOnlyList<double> SupportedLineHeightFactors { get; } = [1.0, 1.15, 1.3, 1.5];

    public static int NormalizeIndentSize(int? value)
    {
        return value is int indent && SupportedIndentSizes.Contains(indent)
            ? indent
            : DefaultIndentSize;
    }

    public static double NormalizeLineHeightFactor(double? value)
    {
        if (value is double lineHeight)
        {
            foreach (var supported in SupportedLineHeightFactors)
            {
                if (Math.Abs(lineHeight - supported) < 0.0001)
                {
                    return supported;
                }
            }
        }

        return DefaultLineHeightFactor;
    }

    public static string FormatLineHeight(double value)
    {
        return NormalizeLineHeightFactor(value).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
