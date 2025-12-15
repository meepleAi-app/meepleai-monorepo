namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;

internal record AlertDuration
{
    public int Minutes { get; init; }

    public AlertDuration(int minutes)
    {
        if (minutes <= 0) throw new ArgumentException("Duration must be positive", nameof(minutes));
        Minutes = minutes;
    }

    public TimeSpan ToTimeSpan() => TimeSpan.FromMinutes(Minutes);
    public override string ToString() => $"{Minutes} minutes";

    public static AlertDuration FromMinutes(int minutes) => new(minutes);
    public static AlertDuration FiveMinutes => new(5);
    public static AlertDuration TenMinutes => new(10);
    public static AlertDuration FifteenMinutes => new(15);
}
