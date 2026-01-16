using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for generating a game state template using AI.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class GenerateGameStateTemplateCommandHandler
    : ICommandHandler<GenerateGameStateTemplateCommand, GameStateTemplateDto>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly IGameStateTemplateRepository _templateRepository;
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly TemplateVersioningService _versioningService;
    private readonly IGameStateSchemaGenerator _schemaGenerator;
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateGameStateTemplateCommandHandler> _logger;

    public GenerateGameStateTemplateCommandHandler(
        ISharedGameRepository gameRepository,
        IGameStateTemplateRepository templateRepository,
        ISharedGameDocumentRepository documentRepository,
        TemplateVersioningService versioningService,
        IGameStateSchemaGenerator schemaGenerator,
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<GenerateGameStateTemplateCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _versioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        _schemaGenerator = schemaGenerator ?? throw new ArgumentNullException(nameof(schemaGenerator));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameStateTemplateDto> Handle(
        GenerateGameStateTemplateCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Generating game state template for shared game: {SharedGameId}",
            command.SharedGameId);

        // Verify shared game exists
        var game = await _gameRepository.GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new InvalidOperationException($"Shared game with ID {command.SharedGameId} not found");
        }

        // Get the active rulebook document for the game
        var rulebookDocument = await _documentRepository.GetActiveDocumentAsync(
            command.SharedGameId,
            SharedGameDocumentType.Rulebook,
            cancellationToken).ConfigureAwait(false);

        // Get rulebook content from PDF
        var rulebookContent = await GetRulebookContentAsync(
            rulebookDocument?.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(rulebookContent))
        {
            _logger.LogWarning(
                "No rulebook content available for game {SharedGameId}, using game description",
                command.SharedGameId);
            rulebookContent = $"Game: {game.Title}\nDescription: {game.Description}";
        }

        // Determine version for new template
        var existingTemplates = await _templateRepository.GetBySharedGameIdAsync(
            command.SharedGameId, cancellationToken).ConfigureAwait(false);

        var newVersion = DetermineNextVersion(existingTemplates);

        // Generate schema using AI
        var schemaResult = await _schemaGenerator.GenerateSchemaAsync(
            game.Title,
            rulebookContent,
            cancellationToken).ConfigureAwait(false);

        // Create the template
        var schema = JsonDocument.Parse(schemaResult.SchemaJson);

        var stateTemplate = GameStateTemplate.CreateFromAI(
            command.SharedGameId,
            command.Name,
            schema,
            command.CreatedBy,
            schemaResult.ConfidenceScore,
            newVersion);

        // Set as active if requested
        if (command.SetAsActive)
        {
            await _versioningService.SetActiveVersionAsync(stateTemplate, cancellationToken)
                .ConfigureAwait(false);
        }

        // Persist
        await _templateRepository.AddAsync(stateTemplate, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Game state template generated: {TemplateId} for game {SharedGameId} with confidence {Confidence}",
            stateTemplate.Id, command.SharedGameId, schemaResult.ConfidenceScore);

        return MapToDto(stateTemplate);
    }

    private async Task<string?> GetRulebookContentAsync(
        Guid? pdfDocumentId,
        CancellationToken cancellationToken)
    {
        if (pdfDocumentId == null)
            return null;

        // Get the extracted text from the PDF document
        var textChunks = await _context.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfDocumentId)
            .OrderBy(tc => tc.ChunkIndex)
            .Select(tc => tc.Content)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (textChunks.Count == 0)
            return null;

        return string.Join("\n\n", textChunks);
    }

    private static string DetermineNextVersion(IReadOnlyList<GameStateTemplate> existingTemplates)
    {
        if (existingTemplates.Count == 0)
            return "1.0";

        // Find the highest version
        var maxVersion = existingTemplates
            .Select(t => t.Version)
            .OrderByDescending(v => v, StringComparer.Ordinal)
            .First();

        // Parse and increment
        var parts = maxVersion.Split('.');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var major) &&
            int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var minor))
        {
            return $"{major}.{minor + 1}";
        }

        return "1.0";
    }

    private static GameStateTemplateDto MapToDto(GameStateTemplate template) =>
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
