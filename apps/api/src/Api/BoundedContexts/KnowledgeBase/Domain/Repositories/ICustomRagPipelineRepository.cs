using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for custom RAG pipeline persistence.
/// Issue #3453: Visual RAG Strategy Builder - Save/Load/Export.
/// </summary>
public interface ICustomRagPipelineRepository
{
    /// <summary>
    /// Save a new custom pipeline.
    /// </summary>
    Task<Guid> SaveAsync(
        string name,
        string? description,
        PipelineDefinition pipeline,
        Guid createdBy,
        bool isPublished = false,
        string[] tags = null!,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing custom pipeline.
    /// </summary>
    Task UpdateAsync(
        Guid id,
        string name,
        string? description,
        PipelineDefinition pipeline,
        bool isPublished,
        string[] tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a custom pipeline by ID.
    /// </summary>
    Task<CustomPipelineData?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all pipelines for a user.
    /// </summary>
    Task<IReadOnlyList<CustomPipelineData>> GetUserPipelinesAsync(
        Guid userId,
        bool includePublished = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List published template pipelines.
    /// </summary>
    Task<IReadOnlyList<CustomPipelineData>> GetTemplatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a custom pipeline.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a pipeline exists.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Data transfer record for custom pipelines.
/// </summary>
public sealed record CustomPipelineData(
    Guid Id,
    string Name,
    string? Description,
    PipelineDefinition Pipeline,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsPublished,
    string[] Tags,
    bool IsTemplate,
    string? AccessTier);
