using System.Text.Json;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of UserLibrary repository.
/// Maps between domain UserLibraryEntry entity and UserLibraryEntryEntity persistence model.
/// </summary>
internal class UserLibraryRepository : RepositoryBase, IUserLibraryRepository
{
    private readonly ILogger<UserLibraryRepository> _logger;

    public UserLibraryRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<UserLibraryRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserLibraryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<UserLibraryEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<UserLibraryEntry?> GetByUserAndGameAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<(IReadOnlyList<UserLibraryEntry> Entries, int Total)> GetUserLibraryPaginatedAsync(
        Guid userId,
        string? search,
        bool? favoritesOnly,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = DbContext.UserLibraryEntries
            .AsNoTracking()
            .Include(e => e.SharedGame)
            .Where(e => e.UserId == userId);

        // Apply search filter (by game title)
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.SharedGame != null && EF.Functions.ILike(e.SharedGame.Title, $"%{search}%"));
        }

        // Apply favorites filter
        if (favoritesOnly == true)
        {
            query = query.Where(e => e.IsFavorite);
        }

        // Get total count before pagination
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply sorting
        query = ApplySorting(query, sortBy, descending);

        // Apply pagination
        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var entries = entities.Select(MapToDomain).ToList();

        return (entries, total);
    }

    public async Task<int> GetUserLibraryCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetFavoriteCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.IsFavorite, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsGameInLibraryAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(DateTime? Oldest, DateTime? Newest)> GetLibraryDateRangeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var dates = await DbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.AddedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dates.Count == 0)
            return (null, null);

        return (dates.Min(), dates.Max());
    }

    public async Task AddAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToPersistence(entry);
        await DbContext.UserLibraryEntries.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToPersistence(entry);
        DbContext.UserLibraryEntries.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(UserLibraryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToPersistence(entry);
        DbContext.UserLibraryEntries.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserLibraryEntries
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IQueryable<UserLibraryEntryEntity> ApplySorting(
        IQueryable<UserLibraryEntryEntity> query,
        string? sortBy,
        bool descending)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => descending
                ? query.OrderByDescending(e => e.SharedGame != null ? e.SharedGame.Title : "")
                : query.OrderBy(e => e.SharedGame != null ? e.SharedGame.Title : ""),
            "favorite" => descending
                ? query.OrderByDescending(e => e.IsFavorite).ThenByDescending(e => e.AddedAt)
                : query.OrderBy(e => e.IsFavorite).ThenByDescending(e => e.AddedAt),
            "addedat" or _ => descending
                ? query.OrderByDescending(e => e.AddedAt)
                : query.OrderBy(e => e.AddedAt),
        };
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private UserLibraryEntry MapToDomain(UserLibraryEntryEntity entity)
    {
        var entry = new UserLibraryEntry(entity.Id, entity.UserId, entity.GameId);

        // Set notes if present
        if (!string.IsNullOrWhiteSpace(entity.Notes))
        {
            entry.UpdateNotes(new LibraryNotes(entity.Notes));
        }

        // Set favorite status
        if (entity.IsFavorite)
        {
            entry.MarkAsFavorite();
        }

        // Deserialize custom agent configuration from JSONB
        if (!string.IsNullOrWhiteSpace(entity.CustomAgentConfigJson))
        {
            try
            {
                var configDto = JsonSerializer.Deserialize<AgentConfigJson>(entity.CustomAgentConfigJson);
                if (configDto != null)
                {
                    var agentConfig = AgentConfiguration.Create(
                        llmModel: configDto.LlmModel,
                        temperature: configDto.Temperature,
                        maxTokens: configDto.MaxTokens,
                        personality: configDto.Personality,
                        detailLevel: configDto.DetailLevel,
                        personalNotes: configDto.PersonalNotes
                    );
                    entry.ConfigureAgent(agentConfig);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to deserialize CustomAgentConfigJson for UserLibraryEntry {EntryId}. JSON: {Json}",
                    entity.Id, entity.CustomAgentConfigJson);
                // Entry will have null CustomAgentConfig (graceful degradation)
            }
        }

        // Reconstruct custom PDF metadata
        if (!string.IsNullOrWhiteSpace(entity.CustomPdfUrl) &&
            entity.CustomPdfUploadedAt.HasValue &&
            entity.CustomPdfFileSizeBytes.HasValue &&
            !string.IsNullOrWhiteSpace(entity.CustomPdfOriginalFileName))
        {
            var pdfMetadata = CustomPdfMetadata.CreateWithTimestamp(
                url: entity.CustomPdfUrl,
                uploadedAt: entity.CustomPdfUploadedAt.Value,
                fileSizeBytes: entity.CustomPdfFileSizeBytes.Value,
                originalFileName: entity.CustomPdfOriginalFileName
            );
            entry.UploadCustomPdf(pdfMetadata);
        }

        // Override AddedAt from DB using reflection (same pattern as GameRepository)
        var addedAtProp = typeof(UserLibraryEntry).GetProperty("AddedAt");
        addedAtProp?.SetValue(entry, entity.AddedAt);

        // Clear domain events that were raised during construction
        // (we don't want to re-raise events for existing entities)
        entry.ClearDomainEvents();

        return entry;
    }

    /// <summary>
    /// Internal DTO for JSON serialization of AgentConfiguration.
    /// </summary>
    private sealed record AgentConfigJson(
        string LlmModel,
        double Temperature,
        int MaxTokens,
        string Personality,
        string DetailLevel,
        string? PersonalNotes
    );

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static UserLibraryEntryEntity MapToPersistence(UserLibraryEntry domainEntity)
    {
        ArgumentNullException.ThrowIfNull(domainEntity);

        // Serialize custom agent configuration to JSON
        string? agentConfigJson = null;
        if (domainEntity.CustomAgentConfig != null)
        {
            var configDto = new AgentConfigJson(
                LlmModel: domainEntity.CustomAgentConfig.LlmModel,
                Temperature: domainEntity.CustomAgentConfig.Temperature,
                MaxTokens: domainEntity.CustomAgentConfig.MaxTokens,
                Personality: domainEntity.CustomAgentConfig.Personality,
                DetailLevel: domainEntity.CustomAgentConfig.DetailLevel,
                PersonalNotes: domainEntity.CustomAgentConfig.PersonalNotes
            );
            agentConfigJson = JsonSerializer.Serialize(configDto);
        }

        return new UserLibraryEntryEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            GameId = domainEntity.GameId,
            AddedAt = domainEntity.AddedAt,
            Notes = domainEntity.Notes?.Value,
            IsFavorite = domainEntity.IsFavorite,
            CustomAgentConfigJson = agentConfigJson,
            CustomPdfUrl = domainEntity.CustomPdfMetadata?.Url,
            CustomPdfUploadedAt = domainEntity.CustomPdfMetadata?.UploadedAt,
            CustomPdfFileSizeBytes = domainEntity.CustomPdfMetadata?.FileSizeBytes,
            CustomPdfOriginalFileName = domainEntity.CustomPdfMetadata?.OriginalFileName
        };
    }
}
