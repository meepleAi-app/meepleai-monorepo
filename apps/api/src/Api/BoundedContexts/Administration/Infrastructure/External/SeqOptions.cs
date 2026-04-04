namespace Api.BoundedContexts.Administration.Infrastructure.External;

public sealed class SeqOptions
{
    public const string SectionName = "Seq";

    public string ServerUrl { get; set; } = "http://seq:5341";
    public string? ApiKey { get; set; }
    public int RetentionDays { get; set; } = 7;
}
