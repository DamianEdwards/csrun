namespace System;

internal static class StringExtensions
{
    public static int GetLineCount(this string? value)
    {
        if (value == null)
        {
            return 0;
        }

        var span = value.AsSpan();
        var count = 0;

        if (Environment.NewLine.Length == 1)
        {
            var newLine = Environment.NewLine[0];
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == newLine)
                {
                    count++;
                }
            }
        }
        else
        {
            var newLine = Environment.NewLine.AsSpan();
            for (var i = 0; i < span.Length - newLine.Length + 1; i++)
            {
                if (span.Slice(i, newLine.Length).SequenceEqual(newLine))
                {
                    count++;
                    i += newLine.Length - 1;
                }
            }
        }

        if (span.Length > 0)
        {
            count++;
        }

        return count;
    }
}
