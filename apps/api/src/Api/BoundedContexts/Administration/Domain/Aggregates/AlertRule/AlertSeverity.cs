#pragma warning disable MA0048 // File name must match type name - enum and extensions kept together
namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;

internal enum AlertSeverity { Info, Warning, Error, Critical }

internal static class AlertSeverityExtensions
{
    public static string ToDisplayString(this AlertSeverity severity) => severity switch
    {
        AlertSeverity.Info => "Info",
        AlertSeverity.Warning => "Warning",
        AlertSeverity.Error => "Error",
        AlertSeverity.Critical => "Critical",
        _ => throw new ArgumentOutOfRangeException(nameof(severity))
    };

    public static AlertSeverity FromString(string severity) => severity.ToLowerInvariant() switch
    {
        "info" => AlertSeverity.Info,
        "warning" => AlertSeverity.Warning,
        "error" => AlertSeverity.Error,
        "critical" => AlertSeverity.Critical,
        _ => throw new ArgumentException($"Invalid severity: {severity}", nameof(severity))
    };
}
