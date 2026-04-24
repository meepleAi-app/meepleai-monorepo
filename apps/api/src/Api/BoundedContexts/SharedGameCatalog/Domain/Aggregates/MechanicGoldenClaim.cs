using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public sealed class MechanicGoldenClaim
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public MechanicSection Section { get; private set; }
    public string Statement { get; private set; } = string.Empty;
    public int ExpectedPage { get; private set; }
    public string SourceQuote { get; private set; } = string.Empty;
    public string[] Keywords { get; private set; } = Array.Empty<string>();
    public float[]? Embedding { get; private set; }
    public Guid CuratorUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private MechanicGoldenClaim() { }

    public static async Task<MechanicGoldenClaim> CreateAsync(
        Guid sharedGameId, MechanicSection section, string statement, int expectedPage,
        string sourceQuote, Guid curatorUserId,
        IEmbeddingService embedding, IKeywordExtractor keywords, CancellationToken ct)
    {
        ValidateStatement(statement);
        ValidatePage(expectedPage);
        ValidateSourceQuote(sourceQuote);
        var now = DateTimeOffset.UtcNow;
        return new MechanicGoldenClaim
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            Section = section,
            Statement = statement,
            ExpectedPage = expectedPage,
            SourceQuote = sourceQuote,
            CuratorUserId = curatorUserId,
            Keywords = keywords.Extract(statement),
            Embedding = await embedding.EmbedAsync(statement, ct).ConfigureAwait(false),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public async Task UpdateAsync(string statement, int expectedPage, string sourceQuote,
        IEmbeddingService embedding, IKeywordExtractor keywords, CancellationToken ct)
    {
        if (DeletedAt.HasValue)
        {
            throw new InvalidOperationException("Cannot update a deactivated claim.");
        }
        ValidateStatement(statement);
        ValidatePage(expectedPage);
        ValidateSourceQuote(sourceQuote);
        if (!string.Equals(statement, Statement, StringComparison.Ordinal))
        {
            Statement = statement;
            Keywords = keywords.Extract(statement);
            Embedding = await embedding.EmbedAsync(statement, ct).ConfigureAwait(false);
        }
        ExpectedPage = expectedPage;
        SourceQuote = sourceQuote;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        if (DeletedAt.HasValue) throw new InvalidOperationException("Already deactivated.");
        DeletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rehydrates a claim from persistence. Used exclusively by the repository's
    /// <c>MapToDomain</c>; bypasses validation because invariants were enforced at creation time.
    /// </summary>
    public static MechanicGoldenClaim Reconstitute(
        Guid id,
        Guid sharedGameId,
        MechanicSection section,
        string statement,
        int expectedPage,
        string sourceQuote,
        string[] keywords,
        float[]? embedding,
        Guid curatorUserId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? deletedAt)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(sourceQuote);
        ArgumentNullException.ThrowIfNull(keywords);

        return new MechanicGoldenClaim
        {
            Id = id,
            SharedGameId = sharedGameId,
            Section = section,
            Statement = statement,
            ExpectedPage = expectedPage,
            SourceQuote = sourceQuote,
            Keywords = keywords,
            Embedding = embedding,
            CuratorUserId = curatorUserId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
        };
    }

    private static void ValidateStatement(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 500)
            throw new ArgumentException("Statement must be 1..500 chars.", nameof(s));
    }
    private static void ValidatePage(int p)
    {
        if (p < 1) throw new ArgumentException("Page must be >= 1.", nameof(p));
    }
    private static void ValidateSourceQuote(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 1000)
            throw new ArgumentException("SourceQuote must be 1..1000 chars.", nameof(s));
    }
}
