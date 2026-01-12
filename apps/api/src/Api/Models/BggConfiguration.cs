namespace Api.Models;

/// <summary>
/// Configuration for BoardGameGeek API integration (AI-13).
/// </summary>
/// <remarks>
/// To obtain an API token:
/// 1. Register your application at https://boardgamegeek.com/applications
/// 2. Wait for approval from BGG team
/// 3. Add token to environment: BGG_API_TOKEN=your_token_here
/// </remarks>
internal class BggConfiguration
{
    public string BaseUrl { get; set; } = "https://boardgamegeek.com/xmlapi2";

    /// <summary>
    /// BGG API Bearer token (optional for development, required for production).
    /// Register at https://boardgamegeek.com/applications to obtain token.
    /// </summary>
    public string? ApiToken { get; set; }

    public int CacheTtlDays { get; set; } = 7;
    public double MaxRequestsPerSecond { get; set; } = 2.0;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
}
