using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for GameStateTemplate.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
public record GameStateTemplateDto(
    Guid Id,
    Guid SharedGameId,
    string Name,
    string? SchemaJson,
    string Version,
    bool IsActive,
    GenerationSource Source,
    decimal? ConfidenceScore,
    DateTime GeneratedAt,
    Guid CreatedBy
);
