using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get user AI consent status (Issue #5512)
/// </summary>
internal record GetUserAiConsentQuery(Guid UserId) : IQuery<UserAiConsentDto?>;

/// <summary>
/// DTO representing user AI consent state
/// </summary>
public record UserAiConsentDto(
    Guid UserId,
    bool ConsentedToAiProcessing,
    bool ConsentedToExternalProviders,
    DateTime ConsentedAt,
    string ConsentVersion);
