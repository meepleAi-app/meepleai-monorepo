namespace Api.BoundedContexts.KbQuality.Application.Configuration;

public sealed class EvalQualityOptions
{
    public const string SectionName = "EvalQuality";

    public decimal MonthlyCostCap { get; set; } = 50.00m;
    public int RateLimitPerDocMinutes { get; set; } = 10;
    public int RetentionMonths { get; set; } = 18;
    public QualityBandsConfig QualityBands { get; set; } = new();
}
