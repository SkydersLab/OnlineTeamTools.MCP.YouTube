using System.Globalization;
using System.Text;

namespace OnlineTeamTools.MCP.YouTube.Infrastructure;

public sealed class StderrLogger
{
    private static readonly object Sync = new();

    public void Info(string message, params (string Key, object? Value)[] fields)
        => Write("INFO", message, fields);

    public void Warn(string message, params (string Key, object? Value)[] fields)
        => Write("WARN", message, fields);

    public void Error(string message, params (string Key, object? Value)[] fields)
        => Write("ERROR", message, fields);

    private static void Write(string level, string message, IReadOnlyList<(string Key, object? Value)> fields)
    {
        var builder = new StringBuilder();
        builder.Append(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(' ');
        builder.Append(level);
        builder.Append(' ');
        builder.Append(message);

        foreach (var (key, value) in fields)
        {
            builder.Append(' ');
            builder.Append(key);
            builder.Append('=');
            builder.Append(Sanitize(value));
        }

        lock (Sync)
        {
            Console.Error.WriteLine(builder.ToString());
        }
    }

    private static string Sanitize(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        return value
            .ToString()
            ?.Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            ?? string.Empty;
    }
}
