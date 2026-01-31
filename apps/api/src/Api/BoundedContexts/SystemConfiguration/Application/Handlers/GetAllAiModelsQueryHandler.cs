using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handler for retrieving all AI model configurations with filtering and pagination
/// </summary>
/// <remarks>
/// Issue #2567: GET /api/v1/admin/ai-models endpoint handler
/// </remarks>
internal sealed class GetAllAiModelsQueryHandler : IQueryHandler<GetAllAiModelsQuery, AiModelListDto>
{
    private readonly IAiModelConfigurationRepository _repository;

    public GetAllAiModelsQueryHandler(IAiModelConfigurationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AiModelListDto> Handle(GetAllAiModelsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var models = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            models = models.Where(m => m.Provider.Equals(query.Provider, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (query.IsActive.HasValue)
        {
            models = models.Where(m => m.IsActive == query.IsActive.Value).ToList();
        }

        // Order by priority (lower = higher preference)
        models = models.OrderBy(m => m.Priority).ToList();

        var totalCount = models.Count;

        // Apply pagination
        var skip = (query.Page - 1) * query.PageSize;
        var pagedModels = models.Skip(skip).Take(query.PageSize).ToList();

        var modelDtos = pagedModels.Select(MapToDto).ToList();

        return new AiModelListDto
        {
            Models = modelDtos,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    private static AiModelDto MapToDto(AiModelConfiguration model)
    {
        return new AiModelDto
        {
            Id = model.Id,
            ModelId = model.ModelId,
            DisplayName = model.DisplayName,
            Provider = model.Provider,
            Priority = model.Priority,
            IsActive = model.IsActive,
            IsPrimary = model.IsPrimary,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            Settings = model.Settings,
            Usage = model.Usage
        };
    }
}
