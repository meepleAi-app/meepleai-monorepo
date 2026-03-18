namespace Api.Infrastructure.Configuration;

public sealed class HealthMonitorOptions
{
    public const string SectionName = "HealthMonitor";
    public int PollingIntervalSeconds { get; set; } = 60;
    public int StartupDelaySeconds { get; set; } = 10;
    public int ReminderIntervalMinutes { get; set; } = 30;
    public int DegradedThreshold { get; set; } = 1;
    public int UnhealthyThreshold { get; set; } = 3;
    public int RecoveryThreshold { get; set; } = 2;
}
