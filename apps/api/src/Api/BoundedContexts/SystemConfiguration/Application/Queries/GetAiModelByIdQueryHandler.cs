using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Handler for retrieving a single AI model configuration by ID
/// </summary>
/// <remarks>
/// Issue #2567: GET /api/v1/admin/ai-models/{id} endpoint handler
/// </remarks>
internal sealed class GetAiModelByIdQueryHandler : IQueryHandler<GetAiModelByIdQuery, AiModelDto>
{
    private readonly IAiModelConfigurationRepository _repository;

    public GetAiModelByIdQueryHandler(IAiModelConfigurationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AiModelDto> Handle(GetAiModelByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var model = await _repository.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (model is null)
        {
            throw new NotFoundException("AiModel", query.Id.ToString());
        }

        return MapToDto(model);
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
