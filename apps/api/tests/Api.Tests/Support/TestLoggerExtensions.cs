using System.Collections.Generic;
using System.Linq;

namespace Api.Tests.Support;

public static class TestLoggerExtensions
{
    public static string? GetStateValue<T>(this TestLogger<T>.LogEntry entry, string key)
    {
        if (entry.State is IEnumerable<KeyValuePair<string, object?>> stateValues)
        {
            var match = stateValues.FirstOrDefault(kvp => kvp.Key == key);
            if (!EqualityComparer<KeyValuePair<string, object?>>.Default.Equals(match, default))
            {
                return match.Value?.ToString();
            }
        }

        return null;
    }
}
