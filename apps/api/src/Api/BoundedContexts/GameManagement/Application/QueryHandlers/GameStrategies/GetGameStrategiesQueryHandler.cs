using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.GameStrategies;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.QueryHandlers.GameStrategies;

/// <summary>
/// Handles GetGameStrategiesQuery with pagination.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
internal sealed class GetGameStrategiesQueryHandler
    : IQueryHandler<GetGameStrategiesQuery, PagedResult<GameStrategyDto>>
{
    private readonly IGameStrategyRepository _repository;
    private readonly ILogger<GetGameStrategiesQueryHandler> _logger;

    public GetGameStrategiesQueryHandler(
        IGameStrategyRepository repository,
        ILogger<GetGameStrategiesQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<GameStrategyDto>> Handle(
        GetGameStrategiesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving strategies for game {GameId}, page {Page}/{PageSize}",
            query.GameId, query.PageNumber, query.PageSize);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _repository
            .GetBySharedGameIdAsync(query.GameId, pageNumber, pageSize, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} strategies (total: {Total}) for game {GameId}",
            items.Count, totalCount, query.GameId);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<GameStrategyDto>(dtos, totalCount, pageNumber, pageSize);
    }

    private static GameStrategyDto MapToDto(GameStrategy strategy) =>
        new(
            strategy.Id,
            strategy.SharedGameId,
            strategy.Title,
            strategy.Content,
            strategy.Author,
            strategy.Upvotes,
            strategy.Tags,
            strategy.CreatedAt);
}
