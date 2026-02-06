using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for custom RAG pipeline persistence.
/// Issue #3453: Visual RAG Strategy Builder - Save/Load/Export.
/// </summary>
internal sealed class CustomRagPipelineRepository : ICustomRagPipelineRepository
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<CustomRagPipelineRepository> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CustomRagPipelineRepository(
        MeepleAiDbContext dbContext,
        ILogger<CustomRagPipelineRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Guid> SaveAsync(
        string name,
        string? description,
        PipelineDefinition pipeline,
        Guid createdBy,
        bool isPublished = false,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        var pipelineJson = JsonSerializer.Serialize(pipeline, s_jsonOptions);

        var entity = new CustomRagPipelineEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            PipelineJson = pipelineJson,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            IsPublished = isPublished,
            Tags = tags ?? Array.Empty<string>(),
            IsTemplate = false
        };

        _dbContext.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Saved custom RAG pipeline {PipelineId} '{Name}' by user {UserId}",
            entity.Id,
            name,
            createdBy);

        return entity.Id;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        Guid id,
        string name,
        string? description,
        PipelineDefinition pipeline,
        bool isPublished,
        string[] tags,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<CustomRagPipelineEntity>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found for update", id);
            throw new InvalidOperationException($"Pipeline {id} not found");
        }

        var pipelineJson = JsonSerializer.Serialize(pipeline, s_jsonOptions);

        entity.Name = name;
        entity.Description = description;
        entity.PipelineJson = pipelineJson;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsPublished = isPublished;
        entity.Tags = tags;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated custom RAG pipeline {PipelineId}", id);
    }

    /// <inheritdoc/>
    public async Task<CustomPipelineData?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<CustomRagPipelineEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            return null;
        }

        var pipeline = JsonSerializer.Deserialize<PipelineDefinition>(
            entity.PipelineJson,
            s_jsonOptions);

        if (pipeline == null)
        {
            _logger.LogError("Failed to deserialize pipeline JSON for {PipelineId}", id);
            return null;
        }

        return new CustomPipelineData(
            entity.Id,
            entity.Name,
            entity.Description,
            pipeline,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsPublished,
            entity.Tags,
            entity.IsTemplate,
            entity.AccessTier);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CustomPipelineData>> GetUserPipelinesAsync(
        Guid userId,
        bool includePublished = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<CustomRagPipelineEntity>()
            .AsNoTracking()
            .Where(e => e.CreatedBy == userId);

        if (!includePublished)
        {
            query = query.Where(e => !e.IsPublished);
        }

        var entities = await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<CustomPipelineData>();
        foreach (var entity in entities)
        {
            var pipeline = JsonSerializer.Deserialize<PipelineDefinition>(
                entity.PipelineJson,
                s_jsonOptions);

            if (pipeline == null)
            {
                _logger.LogWarning("Skipping pipeline {PipelineId} due to deserialization error", entity.Id);
                continue;
            }

            results.Add(new CustomPipelineData(
                entity.Id,
                entity.Name,
                entity.Description,
                pipeline,
                entity.CreatedBy,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.IsPublished,
                entity.Tags,
                entity.IsTemplate,
                entity.AccessTier));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CustomPipelineData>> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Set<CustomRagPipelineEntity>()
            .AsNoTracking()
            .Where(e => e.IsTemplate)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<CustomPipelineData>();
        foreach (var entity in entities)
        {
            var pipeline = JsonSerializer.Deserialize<PipelineDefinition>(
                entity.PipelineJson,
                s_jsonOptions);

            if (pipeline == null)
            {
                _logger.LogWarning("Skipping template {PipelineId} due to deserialization error", entity.Id);
                continue;
            }

            results.Add(new CustomPipelineData(
                entity.Id,
                entity.Name,
                entity.Description,
                pipeline,
                entity.CreatedBy,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.IsPublished,
                entity.Tags,
                entity.IsTemplate,
                entity.AccessTier));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<CustomRagPipelineEntity>()
            .FirstOrDefaultAsync(e => e.Id == id && e.CreatedBy == userId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found or user {UserId} not authorized", id, userId);
            return false;
        }

        _dbContext.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Deleted custom RAG pipeline {PipelineId} by user {UserId}", id, userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<CustomRagPipelineEntity>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
