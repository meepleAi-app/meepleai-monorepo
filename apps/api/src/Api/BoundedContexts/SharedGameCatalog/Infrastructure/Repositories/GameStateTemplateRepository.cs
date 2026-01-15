using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for GameStateTemplate entities.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class GameStateTemplateRepository : IGameStateTemplateRepository
{
    private readonly MeepleAiDbContext _context;

    public GameStateTemplateRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<GameStateTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GameStateTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<GameStateTemplate>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.GameStateTemplates
            .AsNoTracking()
            .Where(t => t.SharedGameId == sharedGameId)
            .OrderByDescending(t => t.IsActive)
            .ThenByDescending(t => t.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<GameStateTemplate?> GetActiveTemplateAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.GameStateTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.SharedGameId == sharedGameId && t.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<bool> VersionExistsAsync(
        Guid sharedGameId,
        string version,
        CancellationToken cancellationToken = default)
    {
        return await _context.GameStateTemplates
            .AsNoTracking()
            .AnyAsync(
                t => t.SharedGameId == sharedGameId && t.Version == version,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(GameStateTemplate stateTemplate, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(stateTemplate);
        await _context.GameStateTemplates.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Update(GameStateTemplate stateTemplate)
    {
        var entity = MapToEntity(stateTemplate);
        _context.GameStateTemplates.Update(entity);
    }

    public void Remove(GameStateTemplate stateTemplate)
    {
        var entity = MapToEntity(stateTemplate);
        _context.GameStateTemplates.Remove(entity);
    }

    public async Task DeactivateOtherVersionsAsync(
        Guid sharedGameId,
        Guid exceptTemplateId,
        CancellationToken cancellationToken = default)
    {
        await _context.GameStateTemplates
            .Where(t => t.SharedGameId == sharedGameId
                && t.Id != exceptTemplateId
                && t.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.IsActive, false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // Mapping methods

    private static GameStateTemplate MapToDomain(GameStateTemplateEntity entity)
    {
        JsonDocument? schema = null;
        if (!string.IsNullOrEmpty(entity.SchemaJson))
        {
            schema = JsonDocument.Parse(entity.SchemaJson);
        }

        return new GameStateTemplate(
            entity.Id,
            entity.SharedGameId,
            entity.Name,
            schema,
            entity.Version,
            entity.IsActive,
            (GenerationSource)entity.Source,
            entity.ConfidenceScore,
            entity.GeneratedAt,
            entity.CreatedBy);
    }

    private static GameStateTemplateEntity MapToEntity(GameStateTemplate template)
    {
        return new GameStateTemplateEntity
        {
            Id = template.Id,
            SharedGameId = template.SharedGameId,
            Name = template.Name,
            SchemaJson = template.GetSchemaAsString(),
            Version = template.Version,
            IsActive = template.IsActive,
            Source = (int)template.Source,
            ConfidenceScore = template.ConfidenceScore,
            GeneratedAt = template.GeneratedAt,
            CreatedBy = template.CreatedBy
        };
    }
}
