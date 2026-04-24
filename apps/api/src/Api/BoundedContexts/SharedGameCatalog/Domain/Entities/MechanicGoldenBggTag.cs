namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

public sealed class MechanicGoldenBggTag
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; private set; }

    private MechanicGoldenBggTag() { }

    public static MechanicGoldenBggTag Create(Guid sharedGameId, string name, string category)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Name 1..200", nameof(name));
        if (string.IsNullOrWhiteSpace(category) || category.Length > 100) throw new ArgumentException("Category 1..100", nameof(category));
        return new MechanicGoldenBggTag
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            Name = name,
            Category = category,
            ImportedAt = DateTimeOffset.UtcNow,
        };
    }
}
