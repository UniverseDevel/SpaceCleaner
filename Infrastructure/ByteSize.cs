namespace SpaceCleaner.Infrastructure;

/// <summary>Formats raw byte counts into human-friendly strings (e.g. "1.34 GB").</summary>
public static class ByteSize
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Bytes have no fractional part; larger units show up to two decimals.
        return unit == 0
            ? $"{value:0} {Units[unit]}"
            : $"{value:0.##} {Units[unit]}";
    }
}
