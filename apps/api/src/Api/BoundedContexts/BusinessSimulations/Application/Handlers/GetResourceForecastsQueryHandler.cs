using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handles retrieving saved resource forecast scenarios for a user.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed class GetResourceForecastsQueryHandler
    : IQueryHandler<GetResourceForecastsQuery, ResourceForecastsResponseDto>
{
    private readonly IResourceForecastRepository _repository;
    private readonly ILogger<GetResourceForecastsQueryHandler> _logger;

    public GetResourceForecastsQueryHandler(
        IResourceForecastRepository repository,
        ILogger<GetResourceForecastsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResourceForecastsResponseDto> Handle(
        GetResourceForecastsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (forecasts, total) = await _repository
            .GetByUserAsync(query.UserId, query.Page, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var items = forecasts.Select(ResourceForecastDto.FromEntity).ToList();

        _logger.LogDebug(
            "Retrieved {Count}/{Total} resource forecasts for user {UserId} (page {Page})",
            items.Count, total, query.UserId, query.Page);

        return new ResourceForecastsResponseDto(items, total, query.Page, query.PageSize);
    }
}
