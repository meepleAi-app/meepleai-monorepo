using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating a game state template.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class UpdateGameStateTemplateCommandHandler
    : ICommandHandler<UpdateGameStateTemplateCommand, GameStateTemplateDto>
{
    private readonly IGameStateTemplateRepository _templateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateGameStateTemplateCommandHandler> _logger;

    public UpdateGameStateTemplateCommandHandler(
        IGameStateTemplateRepository templateRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateGameStateTemplateCommandHandler> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameStateTemplateDto> Handle(
        UpdateGameStateTemplateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating game state template: {TemplateId} to version {NewVersion}",
            command.TemplateId, command.NewVersion);

        var template = await _templateRepository.GetByIdAsync(command.TemplateId, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            throw new InvalidOperationException($"Game state template with ID {command.TemplateId} not found");
        }

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            template.UpdateName(command.Name);
        }

        // Update schema with new version
        var newSchema = JsonDocument.Parse(command.SchemaJson);
        template.UpdateSchema(newSchema, command.NewVersion);

        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Game state template updated: {TemplateId}, new version: {Version}",
            template.Id, template.Version);

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
