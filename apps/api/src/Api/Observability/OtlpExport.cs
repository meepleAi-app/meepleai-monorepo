namespace Api.Observability;

/// <summary>
/// Gate unico dell'export OTLP (spec azure-local §4.4): trace e metriche OpenTelemetry e i log Serilog vanno
/// all'endpoint di <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> quando è valorizzato (Aspire Dashboard in locale, Azure
/// Monitor via collector in Azure). Prometheus e Seq restano sempre attivi.
/// </summary>
internal static class OtlpExport
{
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string ProfileKey = "MEEPLEAI_PROFILE";

    public static bool IsEnabled(IConfiguration configuration) => !string.IsNullOrWhiteSpace(configuration[EndpointKey]);

    public static Uri Endpoint(IConfiguration configuration)
    {
        var raw = configuration[EndpointKey];
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{EndpointKey} must be an absolute URL, got '{raw}'");
        }

        return uri;
    }

    public static string ProfileName(IConfiguration configuration) =>
        configuration[ProfileKey] is { Length: > 0 } profile ? profile : "default";
}
