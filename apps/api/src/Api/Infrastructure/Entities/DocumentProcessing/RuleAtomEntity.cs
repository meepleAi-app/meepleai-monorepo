namespace Api.Infrastructure.Entities;

public class RuleAtomEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleSpecId { get; set; } = Guid.Empty;
    public string Key { get; set; } = default!;
    public string Text { get; set; } = default!;
    public string? Section { get; set; }
    public int? PageNumber { get; set; }
    public int? LineNumber { get; set; }
    public int SortOrder { get; set; }

    public RuleSpecEntity RuleSpec { get; set; } = default!;
}
