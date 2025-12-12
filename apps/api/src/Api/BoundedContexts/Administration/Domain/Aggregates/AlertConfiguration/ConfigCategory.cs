namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;

public enum ConfigCategory
{
    Global,
    Email,
    Slack,
    PagerDuty
}

public static class ConfigCategoryExtensions
{
    public static string ToDisplayString(this ConfigCategory category) => category switch
    {
        ConfigCategory.Global => "Global",
        ConfigCategory.Email => "Email",
        ConfigCategory.Slack => "Slack",
        ConfigCategory.PagerDuty => "PagerDuty",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static ConfigCategory FromString(string category) => category.ToLowerInvariant() switch
    {
        "global" => ConfigCategory.Global,
        "email" => ConfigCategory.Email,
        "slack" => ConfigCategory.Slack,
        "pagerduty" => ConfigCategory.PagerDuty,
        _ => throw new ArgumentException($"Invalid category: {category}", nameof(category))
    };
}
