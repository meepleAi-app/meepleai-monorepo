namespace Api.Infrastructure.Configuration;

public sealed class HealthMonitorOptions
{
    public const string SectionName = "HealthMonitor";
    public int PollingIntervalSeconds { get; set; } = 60;
    public int StartupDelaySeconds { get; set; } = 10;
    public int ReminderIntervalMinutes { get; set; } = 30;

    // Anti-flapping hysteresis-in: require a persistent failure streak before a service
    // is considered Degraded (and an alert fires). At the 60s poll interval, 3 = ~3 min
    // of sustained degradation, which filters out transient single-poll blips of
    // non-critical services (external BGG API slow, ML cold-start, FTS latency spike).
    public int DegradedThreshold { get; set; } = 3;
    public int UnhealthyThreshold { get; set; } = 5;
    public int RecoveryThreshold { get; set; } = 2;
}
