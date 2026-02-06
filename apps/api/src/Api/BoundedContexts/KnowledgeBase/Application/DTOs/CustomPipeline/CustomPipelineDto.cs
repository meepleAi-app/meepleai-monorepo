using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs.CustomPipeline;

/// <summary>
/// DTO for custom RAG pipeline response.
/// Issue #3453: Visual RAG Strategy Builder - API DTOs.
/// </summary>
public sealed record CustomPipelineDto(
    Guid Id,
    string Name,
    string? Description,
    PipelineDefinition Pipeline,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsPublished,
    string[] Tags,
    bool IsTemplate);

/// <summary>
/// DTO for saving a custom pipeline.
/// </summary>
public sealed record SaveCustomPipelineRequest(
    string Name,
    string? Description,
    PipelineDefinition Pipeline,
    bool IsPublished = false,
    string[] Tags = null!);

/// <summary>
/// DTO for updating a custom pipeline.
/// </summary>
public sealed record UpdateCustomPipelineRequest(
    string Name,
    string? Description,
    PipelineDefinition Pipeline,
    bool IsPublished,
    string[] Tags);

/// <summary>
/// DTO for list response with summary info.
/// </summary>
public sealed record CustomPipelineSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsPublished,
    string[] Tags,
    int NodeCount,
    int EdgeCount);
