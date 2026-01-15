using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetGameStateTemplateVersionsQuery.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class GetGameStateTemplateVersionsQueryHandler
    : IQueryHandler<GetGameStateTemplateVersionsQuery, IReadOnlyList<GameStateTemplateDto>>
{
    private readonly IGameStateTemplateRepository _templateRepository;
    private readonly ILogger<GetGameStateTemplateVersionsQueryHandler> _logger;

    public GetGameStateTemplateVersionsQueryHandler(
        IGameStateTemplateRepository templateRepository,
        ILogger<GetGameStateTemplateVersionsQueryHandler> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<GameStateTemplateDto>> Handle(
        GetGameStateTemplateVersionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting all game state template versions for game {SharedGameId}",
            query.SharedGameId);

        var templates = await _templateRepository.GetBySharedGameIdAsync(
            query.SharedGameId,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Found {Count} game state template versions for game {SharedGameId}",
            templates.Count, query.SharedGameId);

        return templates
            .OrderByDescending(t => t.Version, StringComparer.Ordinal)
            .Select(t => new GameStateTemplateDto(
                t.Id,
                t.SharedGameId,
                t.Name,
                t.GetSchemaAsString(),
                t.Version,
                t.IsActive,
                t.Source,
                t.ConfidenceScore,
                t.GeneratedAt,
                t.CreatedBy))
            .ToList();
    }
}
