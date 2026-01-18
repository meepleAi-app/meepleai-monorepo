using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetActiveGameStateTemplateQuery.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class GetActiveGameStateTemplateQueryHandler
    : IQueryHandler<GetActiveGameStateTemplateQuery, GameStateTemplateDto?>
{
    private readonly IGameStateTemplateRepository _templateRepository;
    private readonly ILogger<GetActiveGameStateTemplateQueryHandler> _logger;

    public GetActiveGameStateTemplateQueryHandler(
        IGameStateTemplateRepository templateRepository,
        ILogger<GetActiveGameStateTemplateQueryHandler> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameStateTemplateDto?> Handle(
        GetActiveGameStateTemplateQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting active game state template for game {SharedGameId}",
            query.SharedGameId);

        var template = await _templateRepository.GetActiveTemplateAsync(
            query.SharedGameId,
            cancellationToken).ConfigureAwait(false);

        if (template is null)
        {
            _logger.LogInformation(
                "No active game state template found for game {SharedGameId}",
                query.SharedGameId);
            return null;
        }

        return new GameStateTemplateDto(
            template.Id,
            template.SharedGameId,
            template.Name,
            template.GetSchemaAsString(),
            template.Version,
            template.IsActive,
            template.Source,
            template.ConfidenceScore,
            template.GeneratedAt,
            template.CreatedBy);
    }
}
