# SOLID Refactoring - Complete Implementation Guide

## Overview

This guide provides step-by-step instructions for refactoring the MeepleAI codebase to follow SOLID principles, with focus on reducing class sizes and improving maintainability.

**Estimated Total Time:** 12-16 hours
**Difficulty:** Medium
**Risk:** Low (systematic, incremental changes with validation)

---

## PHASE 2: DbContext Entity Configuration Extraction

### Priority: MEDIUM
### Estimated Time: 1-2 hours
### Risk: LOW

### Problem

`MeepleAiDbContext.cs` has 745 lines with all entity configurations inline in `OnModelCreating()`. This violates Single Responsibility Principle.

### Solution

Extract each entity configuration into separate `IEntityTypeConfiguration<T>` classes.

### 2.1 Create Infrastructure Folder

```bash
mkdir -p apps/api/src/Api/Infrastructure/EntityConfigurations
```

### 2.2 Example: UserEntityConfiguration

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Api.Infrastructure.Entities;

namespace Api.Infrastructure.EntityConfigurations;

public class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(e => e.Email)
            .IsUnique();

        builder.Property(e => e.Username)
            .HasColumnName("username")
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => e.Username)
            .IsUnique();

        builder.Property(e => e.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(e => e.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.LastLoginAt)
            .HasColumnName("last_login_at");

        // AUTH-07: Two-factor authentication
        builder.Property(e => e.TotpSecretEncrypted)
            .HasColumnName("totp_secret_encrypted");

        builder.Property(e => e.IsTwoFactorEnabled)
            .HasColumnName("is_two_factor_enabled")
            .HasDefaultValue(false);

        builder.Property(e => e.TwoFactorEnabledAt)
            .HasColumnName("two_factor_enabled_at");

        // Relationships
        builder.HasMany<UserSessionEntity>()
            .WithOne()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<ApiKeyEntity>()
            .WithOne()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<OAuthAccountEntity>()
            .WithOne()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 2.3 Example: GameEntityConfiguration

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Api.Infrastructure.Entities;

namespace Api.Infrastructure.EntityConfigurations;

public class GameEntityConfiguration : IEntityTypeConfiguration<GameEntity>
{
    public void Configure(EntityTypeBuilder<GameEntity> builder)
    {
        builder.ToTable("games");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(e => e.Title);

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(e => e.BggId)
            .HasColumnName("bgg_id");

        builder.HasIndex(e => e.BggId);

        builder.Property(e => e.MinPlayers)
            .HasColumnName("min_players");

        builder.Property(e => e.MaxPlayers)
            .HasColumnName("max_players");

        builder.Property(e => e.PlayingTime)
            .HasColumnName("playing_time");

        builder.Property(e => e.Complexity)
            .HasColumnName("complexity")
            .HasColumnType("decimal(3,2)");

        builder.Property(e => e.YearPublished)
            .HasColumnName("year_published");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        // Relationships
        builder.HasMany(g => g.RuleSpecs)
            .WithOne(r => r.Game)
            .HasForeignKey(r => r.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<PdfDocumentEntity>()
            .WithOne()
            .HasForeignKey(p => p.GameId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

### 2.4 Refactored MeepleAiDbContext.cs

**Before:** 745 lines
**After:** ~100 lines

```csharp
using Microsoft.EntityFrameworkCore;
using Api.Infrastructure.Entities;

namespace Api.Infrastructure;

public class MeepleAiDbContext : DbContext
{
    public MeepleAiDbContext(DbContextOptions<MeepleAiDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<UserEntity> Users { get; set; } = null!;
    public DbSet<UserSessionEntity> UserSessions { get; set; } = null!;
    public DbSet<ApiKeyEntity> ApiKeys { get; set; } = null!;
    public DbSet<GameEntity> Games { get; set; } = null!;
    public DbSet<RuleSpecEntity> RuleSpecs { get; set; } = null!;
    public DbSet<RuleAtomEntity> RuleAtoms { get; set; } = null!;
    public DbSet<PdfDocumentEntity> PdfDocuments { get; set; } = null!;
    public DbSet<TextChunkEntity> TextChunks { get; set; } = null!;
    public DbSet<VectorDocumentEntity> VectorDocuments { get; set; } = null!;
    public DbSet<ChatEntity> Chats { get; set; } = null!;
    public DbSet<ChatLogEntity> ChatLogs { get; set; } = null!;
    public DbSet<AiRequestLogEntity> AiRequestLogs { get; set; } = null!;
    public DbSet<AgentEntity> Agents { get; set; } = null!;
    public DbSet<AgentFeedbackEntity> AgentFeedback { get; set; } = null!;
    public DbSet<N8nConfigEntity> N8nConfigs { get; set; } = null!;
    public DbSet<AuditLogEntity> AuditLogs { get; set; } = null!;
    public DbSet<OAuthAccountEntity> OAuthAccounts { get; set; } = null!;
    public DbSet<TempSessionEntity> TempSessions { get; set; } = null!;
    public DbSet<UserBackupCodeEntity> UserBackupCodes { get; set; } = null!;
    public DbSet<PromptTemplateEntity> PromptTemplates { get; set; } = null!;
    public DbSet<PromptVersionEntity> PromptVersions { get; set; } = null!;
    public DbSet<PromptAuditLogEntity> PromptAuditLogs { get; set; } = null!;
    public DbSet<PromptEvaluationResultEntity> PromptEvaluationResults { get; set; } = null!;
    public DbSet<AlertEntity> Alerts { get; set; } = null!;
    public DbSet<SystemConfigurationEntity> SystemConfigurations { get; set; } = null!;
    public DbSet<RuleSpecCommentEntity> RuleSpecComments { get; set; } = null!;
    public DbSet<CacheStatEntity> CacheStats { get; set; } = null!;
    public DbSet<PasswordResetTokenEntity> PasswordResetTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeepleAiDbContext).Assembly);
    }
}
```

### 2.5 Implementation Steps

1. **Create entity configuration classes** (one per entity, ~30 files)
2. **Test incrementally** (create 5-10 configs, test, continue)
3. **Update MeepleAiDbContext** (replace OnModelCreating with ApplyConfigurationsFromAssembly)
4. **Create and test migration:**
   ```bash
   cd apps/api/src/Api
   dotnet ef migrations add RefactorEntityConfigurations
   dotnet ef database update
   dotnet test
   ```

### 2.6 Benefits

- ✅ Each entity configuration is isolated and testable
- ✅ Easy to find and modify entity configurations
- ✅ Better organization and maintainability
- ✅ Follows Single Responsibility Principle
- ✅ Standard EF Core pattern

---

## PHASE 3: Service Layer Refactoring

### Priority: HIGH
### Estimated Time: 8-12 hours total
### Risk: MEDIUM (requires careful interface design and DI updates)

## 3.1 RagService Refactoring

**Current:** 1,298 lines, 8+ dependencies, multiple responsibilities

**Target:** 200-300 lines (facade pattern)

### Extract Services

#### 3.1.1 QueryExpansionService

```csharp
namespace Api.Services.Rag;

public interface IQueryExpansionService
{
    Task<List<string>> ExpandQueryAsync(string query, CancellationToken cancellationToken = default);
}

public class QueryExpansionService : IQueryExpansionService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<QueryExpansionService> _logger;

    public QueryExpansionService(
        ILlmService llmService,
        ILogger<QueryExpansionService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<List<string>> ExpandQueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        // Extract query expansion logic from RagService
        // Generate synonyms and alternative phrasings
        var expandedQueries = new List<string> { query };

        try
        {
            var prompt = $"Generate 2-3 alternative phrasings for: {query}";
            var response = await _llmService.GenerateAsync(prompt, cancellationToken);

            // Parse response and extract alternatives
            // Add to expandedQueries

            _logger.LogInformation("Expanded query into {Count} variants", expandedQueries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query expansion failed, using original query only");
        }

        return expandedQueries;
    }
}
```

#### 3.1.2 CitationExtractorService

```csharp
namespace Api.Services.Rag;

public interface ICitationExtractorService
{
    List<Citation> ExtractCitations(List<SearchResult> results, string answer);
    bool ValidateCitations(List<Citation> citations, List<SearchResult> results);
}

public class CitationExtractorService : ICitationExtractorService
{
    private readonly ILogger<CitationExtractorService> _logger;

    public CitationExtractorService(ILogger<CitationExtractorService> logger)
    {
        _logger = logger;
    }

    public List<Citation> ExtractCitations(List<SearchResult> results, string answer)
    {
        var citations = new List<Citation>();

        // Extract [1], [2], etc. from answer
        var citationPattern = @"\[(\d+)\]";
        var matches = Regex.Matches(answer, citationPattern);

        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int index))
            {
                if (index > 0 && index <= results.Count)
                {
                    var result = results[index - 1];
                    citations.Add(new Citation
                    {
                        Index = index,
                        Source = result.Source,
                        PageNumber = result.PageNumber,
                        Confidence = result.Score
                    });
                }
            }
        }

        _logger.LogInformation("Extracted {Count} citations from answer", citations.Count);
        return citations;
    }

    public bool ValidateCitations(List<Citation> citations, List<SearchResult> results)
    {
        // Validate that citations reference actual results
        foreach (var citation in citations)
        {
            if (citation.Index < 1 || citation.Index > results.Count)
            {
                _logger.LogWarning("Invalid citation index: {Index}", citation.Index);
                return false;
            }
        }

        return true;
    }
}
```

#### 3.1.3 SearchResultReranker

```csharp
namespace Api.Services.Rag;

public interface ISearchResultReranker
{
    Task<List<SearchResult>> RerankAsync(
        string query,
        List<SearchResult> results,
        CancellationToken cancellationToken = default);
}

public class SearchResultReranker : ISearchResultReranker
{
    private readonly ILlmService _llmService;
    private readonly ILogger<SearchResultReranker> _logger;

    public SearchResultReranker(
        ILlmService llmService,
        ILogger<SearchResultReranker> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<List<SearchResult>> RerankAsync(
        string query,
        List<SearchResult> results,
        CancellationToken cancellationToken = default)
    {
        if (results.Count <= 1)
        {
            return results;
        }

        try
        {
            // Use LLM to rerank results based on relevance
            var reranked = results
                .OrderByDescending(r => CalculateRelevanceScore(query, r))
                .ToList();

            _logger.LogInformation("Reranked {Count} search results", results.Count);
            return reranked;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reranking failed, returning original order");
            return results;
        }
    }

    private double CalculateRelevanceScore(string query, SearchResult result)
    {
        // Implement relevance scoring logic
        // Consider: semantic similarity, keyword overlap, source authority
        return result.Score;
    }
}
```

#### 3.1.4 Refactored RagService (Facade)

```csharp
namespace Api.Services;

public class RagService : IRagService
{
    private readonly IQdrantService _qdrantService;
    private readonly ILlmService _llmService;
    private readonly IQueryExpansionService _queryExpansion;
    private readonly ICitationExtractorService _citationExtractor;
    private readonly ISearchResultReranker _reranker;
    private readonly IHybridSearchService _hybridSearch;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IQdrantService qdrantService,
        ILlmService llmService,
        IQueryExpansionService queryExpansion,
        ICitationExtractorService citationExtractor,
        ISearchResultReranker reranker,
        IHybridSearchService hybridSearch,
        ILogger<RagService> logger)
    {
        _qdrantService = qdrantService;
        _llmService = llmService;
        _queryExpansion = queryExpansion;
        _citationExtractor = citationExtractor;
        _reranker = reranker;
        _hybridSearch = hybridSearch;
        _logger = logger;
    }

    public async Task<RagResponse> SearchAsync(
        string query,
        string? gameId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Expand query
        var expandedQueries = await _queryExpansion.ExpandQueryAsync(query, cancellationToken);

        // 2. Perform hybrid search
        var searchResults = await _hybridSearch.SearchAsync(
            expandedQueries,
            gameId,
            cancellationToken);

        // 3. Rerank results
        var rerankedResults = await _reranker.RerankAsync(query, searchResults, cancellationToken);

        // 4. Generate answer with LLM
        var answer = await GenerateAnswerAsync(query, rerankedResults, cancellationToken);

        // 5. Extract and validate citations
        var citations = _citationExtractor.ExtractCitations(rerankedResults, answer);
        var citationsValid = _citationExtractor.ValidateCitations(citations, rerankedResults);

        return new RagResponse
        {
            Answer = answer,
            Citations = citations,
            Results = rerankedResults,
            Confidence = CalculateConfidence(rerankedResults),
            CitationsValid = citationsValid
        };
    }

    private async Task<string> GenerateAnswerAsync(
        string query,
        List<SearchResult> results,
        CancellationToken cancellationToken)
    {
        // Build context from search results
        var context = string.Join("\n\n", results.Select((r, i) =>
            $"[{i + 1}] {r.Content} (Page {r.PageNumber})"));

        var prompt = $@"Based on the following context, answer the question.
Use citations [1], [2], etc. to reference specific sources.

Context:
{context}

Question: {query}

Answer:";

        return await _llmService.GenerateAsync(prompt, cancellationToken);
    }

    private double CalculateConfidence(List<SearchResult> results)
    {
        if (!results.Any()) return 0.0;
        return results.Average(r => r.Score);
    }
}
```

### 3.1.5 Update DI Registration

**In ApplicationServiceExtensions.cs:**

```csharp
private static IServiceCollection AddAiServices(this IServiceCollection services)
{
    // RAG sub-services
    services.AddScoped<IQueryExpansionService, QueryExpansionService>();
    services.AddScoped<ICitationExtractorService, CitationExtractorService>();
    services.AddScoped<ISearchResultReranker, SearchResultReranker>();
    services.AddScoped<IHybridSearchCoordinator, HybridSearchCoordinator>();

    // RAG facade
    services.AddScoped<IRagService, RagService>();

    // ... other services
}
```

## 3.2 QdrantService Refactoring

**Current:** 1,027 lines, mixed responsibilities

**Target:** 200-300 lines (facade pattern)

### Extract Managers

#### 3.2.1 QdrantCollectionManager

```csharp
namespace Api.Services.Qdrant;

public interface IQdrantCollectionManager
{
    Task<bool> CollectionExistsAsync(string collectionName);
    Task CreateCollectionAsync(string collectionName, int vectorSize);
    Task DeleteCollectionAsync(string collectionName);
    Task<CollectionInfo> GetCollectionInfoAsync(string collectionName);
}

public class QdrantCollectionManager : IQdrantCollectionManager
{
    private readonly IQdrantClientAdapter _client;
    private readonly ILogger<QdrantCollectionManager> _logger;

    public QdrantCollectionManager(
        IQdrantClientAdapter client,
        ILogger<QdrantCollectionManager> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> CollectionExistsAsync(string collectionName)
    {
        try
        {
            await _client.GetCollectionInfoAsync(collectionName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateCollectionAsync(string collectionName, int vectorSize)
    {
        _logger.LogInformation("Creating collection {CollectionName} with vector size {VectorSize}",
            collectionName, vectorSize);

        var config = new VectorCollectionConfig
        {
            Size = vectorSize,
            Distance = DistanceMetric.Cosine
        };

        await _client.CreateCollectionAsync(collectionName, config);
        _logger.LogInformation("Collection {CollectionName} created successfully", collectionName);
    }

    public async Task DeleteCollectionAsync(string collectionName)
    {
        _logger.LogInformation("Deleting collection {CollectionName}", collectionName);
        await _client.DeleteCollectionAsync(collectionName);
        _logger.LogInformation("Collection {CollectionName} deleted successfully", collectionName);
    }

    public async Task<CollectionInfo> GetCollectionInfoAsync(string collectionName)
    {
        return await _client.GetCollectionInfoAsync(collectionName);
    }
}
```

#### 3.2.2 QdrantVectorIndexer

```csharp
namespace Api.Services.Qdrant;

public interface IQdrantVectorIndexer
{
    Task IndexVectorsAsync(
        string collectionName,
        List<VectorPoint> vectors,
        CancellationToken cancellationToken = default);

    Task BatchIndexAsync(
        string collectionName,
        IAsyncEnumerable<VectorPoint> vectors,
        int batchSize = 100,
        CancellationToken cancellationToken = default);
}

public class QdrantVectorIndexer : IQdrantVectorIndexer
{
    private readonly IQdrantClientAdapter _client;
    private readonly ILogger<QdrantVectorIndexer> _logger;

    public QdrantVectorIndexer(
        IQdrantClientAdapter client,
        ILogger<QdrantVectorIndexer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task IndexVectorsAsync(
        string collectionName,
        List<VectorPoint> vectors,
        CancellationToken cancellationToken = default)
    {
        if (!vectors.Any())
        {
            _logger.LogWarning("No vectors to index");
            return;
        }

        _logger.LogInformation("Indexing {Count} vectors to {CollectionName}",
            vectors.Count, collectionName);

        await _client.UpsertVectorsAsync(collectionName, vectors, cancellationToken);

        _logger.LogInformation("Successfully indexed {Count} vectors", vectors.Count);
    }

    public async Task BatchIndexAsync(
        string collectionName,
        IAsyncEnumerable<VectorPoint> vectors,
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        var batch = new List<VectorPoint>(batchSize);
        var totalIndexed = 0;

        await foreach (var vector in vectors.WithCancellation(cancellationToken))
        {
            batch.Add(vector);

            if (batch.Count >= batchSize)
            {
                await IndexVectorsAsync(collectionName, batch, cancellationToken);
                totalIndexed += batch.Count;
                batch.Clear();

                _logger.LogDebug("Batch indexed: {TotalIndexed} vectors so far", totalIndexed);
            }
        }

        // Index remaining vectors
        if (batch.Any())
        {
            await IndexVectorsAsync(collectionName, batch, cancellationToken);
            totalIndexed += batch.Count;
        }

        _logger.LogInformation("Batch indexing complete: {TotalIndexed} total vectors", totalIndexed);
    }
}
```

#### 3.2.3 QdrantVectorSearcher

```csharp
namespace Api.Services.Qdrant;

public interface IQdrantVectorSearcher
{
    Task<List<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 10,
        Filter? filter = null,
        CancellationToken cancellationToken = default);

    Task<List<ScoredPoint>> BatchSearchAsync(
        string collectionName,
        List<float[]> queryVectors,
        int topK = 10,
        CancellationToken cancellationToken = default);
}

public class QdrantVectorSearcher : IQdrantVectorSearcher
{
    private readonly IQdrantClientAdapter _client;
    private readonly ILogger<QdrantVectorSearcher> _logger;

    public QdrantVectorSearcher(
        IQdrantClientAdapter client,
        ILogger<QdrantVectorSearcher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<ScoredPoint>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 10,
        Filter? filter = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching {CollectionName} with topK={TopK}",
            collectionName, topK);

        var results = await _client.SearchAsync(
            collectionName,
            queryVector,
            topK,
            filter,
            cancellationToken);

        _logger.LogInformation("Found {Count} results", results.Count);
        return results;
    }

    public async Task<List<ScoredPoint>> BatchSearchAsync(
        string collectionName,
        List<float[]> queryVectors,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Batch searching {Count} queries", queryVectors.Count);

        var allResults = new List<ScoredPoint>();

        foreach (var queryVector in queryVectors)
        {
            var results = await SearchAsync(
                collectionName,
                queryVector,
                topK,
                cancellationToken: cancellationToken);

            allResults.AddRange(results);
        }

        // Deduplicate and re-score
        var deduplicated = allResults
            .GroupBy(r => r.Id)
            .Select(g => new ScoredPoint
            {
                Id = g.Key,
                Score = g.Max(r => r.Score), // Take best score
                Payload = g.First().Payload
            })
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        _logger.LogInformation("Batch search complete: {Count} unique results", deduplicated.Count);
        return deduplicated;
    }
}
```

#### 3.2.4 Refactored QdrantService (Facade)

```csharp
namespace Api.Services;

public class QdrantService : IQdrantService
{
    private readonly IQdrantCollectionManager _collectionManager;
    private readonly IQdrantVectorIndexer _indexer;
    private readonly IQdrantVectorSearcher _searcher;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(
        IQdrantCollectionManager collectionManager,
        IQdrantVectorIndexer indexer,
        IQdrantVectorSearcher searcher,
        ILogger<QdrantService> logger)
    {
        _collectionManager = collectionManager;
        _indexer = indexer;
        _searcher = searcher;
        _logger = logger;
    }

    public async Task InitializeCollectionAsync(string collectionName, int vectorSize)
    {
        var exists = await _collectionManager.CollectionExistsAsync(collectionName);

        if (!exists)
        {
            await _collectionManager.CreateCollectionAsync(collectionName, vectorSize);
        }
        else
        {
            _logger.LogInformation("Collection {CollectionName} already exists", collectionName);
        }
    }

    public async Task IndexTextChunksAsync(
        string gameId,
        List<TextChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var collectionName = $"game-{gameId}";

        // Delegate to indexer
        var vectors = chunks.Select(c => new VectorPoint
        {
            Id = c.Id,
            Vector = c.Embedding,
            Payload = new Dictionary<string, object>
            {
                ["content"] = c.Content,
                ["page_number"] = c.PageNumber,
                ["game_id"] = gameId
            }
        }).ToList();

        await _indexer.IndexVectorsAsync(collectionName, vectors, cancellationToken);
    }

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        float[] queryEmbedding,
        string? gameId = null,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        var collectionName = gameId != null ? $"game-{gameId}" : "default";

        // Delegate to searcher
        var results = await _searcher.SearchAsync(
            collectionName,
            queryEmbedding,
            topK,
            cancellationToken: cancellationToken);

        return results.Select(r => new SearchResult
        {
            Content = r.Payload["content"].ToString() ?? "",
            PageNumber = int.Parse(r.Payload["page_number"].ToString() ?? "0"),
            Score = r.Score,
            Source = r.Payload.GetValueOrDefault("source")?.ToString()
        }).ToList();
    }
}
```

### 3.2.5 Update DI Registration

```csharp
private static IServiceCollection AddAiServices(this IServiceCollection services)
{
    // Qdrant sub-services
    services.AddScoped<IQdrantCollectionManager, QdrantCollectionManager>();
    services.AddScoped<IQdrantVectorIndexer, QdrantVectorIndexer>();
    services.AddScoped<IQdrantVectorSearcher, QdrantVectorSearcher>();

    // Qdrant facade
    services.AddScoped<IQdrantService, QdrantService>();

    // ... other services
}
```

---

## Summary

### Total Expected Impact

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Program.cs** | 6,972 lines | ~150 lines | **-98%** |
| **MeepleAiDbContext** | 745 lines | ~100 lines | **-87%** |
| **RagService** | 1,298 lines | ~250 lines | **-81%** |
| **QdrantService** | 1,027 lines | ~200 lines | **-81%** |
| **Average Class Size** | 1,000+ lines | 200-400 lines | **-60-80%** |

### SOLID Compliance

- ✅ **Single Responsibility**: Each class has ONE clear purpose
- ✅ **Open/Closed**: Easy to extend without modifying existing code
- ✅ **Liskov Substitution**: Proper interface abstractions
- ✅ **Interface Segregation**: Specific, focused interfaces
- ✅ **Dependency Inversion**: Dependencies on abstractions, not concretions

### Next Steps

1. **Review this guide** with your team
2. **Start with Phase 1** (Program.cs modularization)
3. **Test thoroughly** after each phase
4. **Commit incrementally** for easy rollback
5. **Document learnings** as you go

### Additional Resources

- ASP.NET Core Extension Methods: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/
- EF Core Entity Configuration: https://docs.microsoft.com/en-us/ef/core/modeling/
- SOLID Principles in C#: https://www.pluralsight.com/courses/csharp-solid-principles

---

**Document Version:** 1.0
**Last Updated:** 2025-10-27
**Author:** Claude Code Refactoring Agent
