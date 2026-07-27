using System;

namespace GroundNotes.Editors.Vim;

public sealed class VimCountParser
{
    private int _value;

    public bool HasValue { get; private set; }

    public int Value => HasValue ? _value : 0;

    public bool TryAppend(char character)
    {
        if (character is < '0' or > '9' || (!HasValue && character == '0'))
        {
            return false;
        }

        var digit = character - '0';
        _value = _value > (int.MaxValue - digit) / 10
            ? int.MaxValue
            : (_value * 10) + digit;
        HasValue = true;
        return true;
    }

    public int Consume(int defaultValue = 1)
    {
        if (defaultValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultValue));
        }

        var result = HasValue ? _value : defaultValue;
        Reset();
        return result;
    }

    public void Reset()
    {
        _value = 0;
        HasValue = false;
    }

    internal static int Multiply(int left, int right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > int.MaxValue / right ? int.MaxValue : left * right;
    }
}
