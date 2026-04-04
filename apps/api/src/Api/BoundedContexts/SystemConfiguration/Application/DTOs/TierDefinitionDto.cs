using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// DTO representing a tier definition with its resource limits.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
public record TierDefinitionDto(
    Guid Id,
    string Name,
    string DisplayName,
    TierLimitsDto Limits,
    string LlmModelTier,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static TierDefinitionDto FromEntity(TierDefinition entity) => new(
        entity.Id,
        entity.Name,
        entity.DisplayName,
        TierLimitsDto.FromValueObject(entity.Limits),
        entity.LlmModelTier,
        entity.IsDefault,
        entity.CreatedAt,
        entity.UpdatedAt);
}

/// <summary>
/// DTO representing tier resource limits for API requests and responses.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
public record TierLimitsDto(
    int MaxPrivateGames,
    int MaxPdfUploadsPerMonth,
    long MaxPdfSizeBytes,
    int MaxAgents,
    int MaxAgentQueriesPerDay,
    int MaxSessionQueries,
    int MaxSessionPlayers,
    int MaxPhotosPerSession,
    bool SessionSaveEnabled,
    int MaxCatalogProposalsPerWeek)
{
    public static TierLimitsDto FromValueObject(TierLimits limits) => new(
        limits.MaxPrivateGames,
        limits.MaxPdfUploadsPerMonth,
        limits.MaxPdfSizeBytes,
        limits.MaxAgents,
        limits.MaxAgentQueriesPerDay,
        limits.MaxSessionQueries,
        limits.MaxSessionPlayers,
        limits.MaxPhotosPerSession,
        limits.SessionSaveEnabled,
        limits.MaxCatalogProposalsPerWeek);

    public TierLimits ToValueObject() => TierLimits.Create(
        MaxPrivateGames, MaxPdfUploadsPerMonth,
        MaxPdfSizeBytes, MaxAgents,
        MaxAgentQueriesPerDay, MaxSessionQueries,
        MaxSessionPlayers, MaxPhotosPerSession,
        SessionSaveEnabled, MaxCatalogProposalsPerWeek);
}
