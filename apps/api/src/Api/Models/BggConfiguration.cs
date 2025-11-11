namespace Api.Models;

/// <summary>
/// Configuration for BoardGameGeek API integration (AI-13).
/// </summary>
public class BggConfiguration
{
    public string BaseUrl { get; set; } = "https://boardgamegeek.com/xmlapi2";
    public int CacheTtlDays { get; set; } = 7;
    public double MaxRequestsPerSecond { get; set; } = 2.0;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
}
