using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to get all AI model configurations with optional filtering and pagination
/// </summary>
/// <remarks>
/// Issue #2567: Admin endpoint for listing AI models
/// </remarks>
internal sealed record GetAllAiModelsQuery(
    string? Provider = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<AiModelListDto>;
