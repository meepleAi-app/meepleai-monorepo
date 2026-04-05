namespace Api.BoundedContexts.Administration.Infrastructure.External;

public sealed class SeqOptions
{
    public const string SectionName = "Seq";

    public string ServerUrl { get; set; } = "http://seq:5341";
    /// <summary>Query URL for Seq REST API (port 80); differs from ingestion port 5341.</summary>
    public string QueryUrl { get; set; } = "http://seq:80";
    public string? ApiKey { get; set; }
    public int RetentionDays { get; set; } = 7;
}
