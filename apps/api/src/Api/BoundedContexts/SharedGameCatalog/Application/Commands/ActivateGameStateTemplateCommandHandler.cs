using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for activating a game state template version.
/// Ensures only one active version per game.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class ActivateGameStateTemplateCommandHandler
    : ICommandHandler<ActivateGameStateTemplateCommand, GameStateTemplateDto>
{
    private readonly IGameStateTemplateRepository _templateRepository;
    private readonly TemplateVersioningService _versioningService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateGameStateTemplateCommandHandler> _logger;

    public ActivateGameStateTemplateCommandHandler(
        IGameStateTemplateRepository templateRepository,
        TemplateVersioningService versioningService,
        IUnitOfWork unitOfWork,
        ILogger<ActivateGameStateTemplateCommandHandler> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _versioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameStateTemplateDto> Handle(
        ActivateGameStateTemplateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Activating game state template: {TemplateId}",
            command.TemplateId);

        var template = await _templateRepository.GetByIdAsync(command.TemplateId, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            throw new InvalidOperationException($"Game state template with ID {command.TemplateId} not found");
        }

        // Use domain service to ensure only one active version per game
        await _versioningService.SetActiveVersionAsync(template, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Game state template activated: {TemplateId} for game {SharedGameId}",
            template.Id, template.SharedGameId);

        return MapToDto(template);
    }

    private static GameStateTemplateDto MapToDto(Domain.Entities.GameStateTemplate template) =>
        new(
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
