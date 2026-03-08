using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for finalizing a mechanic draft into a RulebookAnalysis entry.
/// Parses the accepted JSON drafts and creates a RulebookAnalysis.CreateManual().
/// </summary>
internal sealed class FinalizeMechanicAnalysisCommandHandler
    : ICommandHandler<FinalizeMechanicAnalysisCommand, RulebookAnalysisDto>
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMechanicDraftRepository _draftRepository;
    private readonly IRulebookAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FinalizeMechanicAnalysisCommandHandler> _logger;

    public FinalizeMechanicAnalysisCommandHandler(
        IMechanicDraftRepository draftRepository,
        IRulebookAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        ILogger<FinalizeMechanicAnalysisCommandHandler> logger)
    {
        _draftRepository = draftRepository ?? throw new ArgumentNullException(nameof(draftRepository));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RulebookAnalysisDto> Handle(
        FinalizeMechanicAnalysisCommand request,
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetByIdAsync(request.DraftId, cancellationToken)
            .ConfigureAwait(false);

        if (draft is null)
        {
            throw new NotFoundException($"Mechanic draft {request.DraftId} not found");
        }

        if (draft.Status == MechanicDraftStatus.Activated)
        {
            throw new ConflictException("This draft has already been finalized");
        }

        // Validate that at least summary and mechanics have accepted drafts
        if (string.IsNullOrWhiteSpace(draft.SummaryDraft))
        {
            throw new ConflictException("Summary draft must be accepted before finalization");
        }

        if (string.IsNullOrWhiteSpace(draft.MechanicsDraft))
        {
            throw new ConflictException("Mechanics draft must be accepted before finalization");
        }

        _logger.LogInformation(
            "Finalizing mechanic draft {DraftId} for game {SharedGameId}",
            request.DraftId,
            draft.SharedGameId);

        // Parse JSON drafts into domain objects
        var keyMechanics = ParseJsonArrayOrEmpty<string>(draft.MechanicsDraft);
        var victoryConditions = ParseVictoryConditions(draft.VictoryDraft);
        var resources = ParseResources(draft.ResourcesDraft);
        var gamePhases = ParseGamePhases(draft.PhasesDraft);
        var commonQuestions = ParseJsonArrayOrEmpty<string>(draft.QuestionsDraft);

        // Deactivate any existing analyses for this game+PDF
        await _analysisRepository.DeactivateAllAsync(
            draft.SharedGameId,
            draft.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        // Create the RulebookAnalysis via CreateManual (copyright-compliant, Variant C)
        var analysis = RulebookAnalysis.CreateManual(
            draft.SharedGameId,
            draft.PdfDocumentId,
            draft.GameTitle,
            draft.SummaryDraft,
            keyMechanics,
            victoryConditions,
            resources,
            gamePhases,
            commonQuestions,
            request.UserId);

        // Activate immediately
        analysis.SetAsActive();

        await _analysisRepository.AddAsync(analysis, cancellationToken).ConfigureAwait(false);

        // Mark draft as activated
        draft.MarkActivated();
        _draftRepository.Update(draft);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Finalized mechanic draft {DraftId} → RulebookAnalysis {AnalysisId} (active)",
            request.DraftId,
            analysis.Id);

        return new RulebookAnalysisDto(
            analysis.Id,
            analysis.SharedGameId,
            analysis.PdfDocumentId,
            analysis.GameTitle,
            analysis.Summary,
            analysis.KeyMechanics.ToList(),
            analysis.VictoryConditions is not null
                ? new VictoryConditionsDto(
                    analysis.VictoryConditions.Primary,
                    analysis.VictoryConditions.Alternatives.ToList(),
                    analysis.VictoryConditions.IsPointBased,
                    analysis.VictoryConditions.TargetPoints)
                : null,
            analysis.Resources.Select(r => new ResourceDto(r.Name, r.Type, r.Usage, r.IsLimited)).ToList(),
            analysis.GamePhases.Select(p => new GamePhaseDto(p.Name, p.Description, p.Order, p.IsOptional)).ToList(),
            analysis.CommonQuestions.ToList(),
            analysis.ConfidenceScore,
            analysis.Version,
            analysis.IsActive,
            analysis.Source,
            analysis.AnalyzedAt,
            analysis.CreatedBy);
    }

    private static List<T> ParseJsonArrayOrEmpty<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<T>();

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }

    private static VictoryConditions? ParseVictoryConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var dto = JsonSerializer.Deserialize<VictoryConditionsJsonDto>(json,
                CaseInsensitiveOptions);

            if (dto is null || string.IsNullOrWhiteSpace(dto.Primary))
                return null;

            return VictoryConditions.Create(
                dto.Primary,
                dto.Alternatives ?? new List<string>(),
                dto.IsPointBased,
                dto.TargetPoints);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<Resource> ParseResources(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<Resource>();

        try
        {
            var dtos = JsonSerializer.Deserialize<List<ResourceJsonDto>>(json,
                CaseInsensitiveOptions);

            return dtos?
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => Resource.Create(r.Name, r.Type ?? "Other", r.Usage, r.IsLimited))
                .ToList() ?? new List<Resource>();
        }
        catch (JsonException)
        {
            return new List<Resource>();
        }
    }

    private static List<GamePhase> ParseGamePhases(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<GamePhase>();

        try
        {
            var dtos = JsonSerializer.Deserialize<List<GamePhaseJsonDto>>(json,
                CaseInsensitiveOptions);

            return dtos?
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => GamePhase.Create(p.Name, p.Description ?? string.Empty, p.Order, p.IsOptional))
                .ToList() ?? new List<GamePhase>();
        }
        catch (JsonException)
        {
            return new List<GamePhase>();
        }
    }

    // Private DTOs for JSON deserialization
    private sealed record VictoryConditionsJsonDto(
        string Primary,
        List<string>? Alternatives,
        bool IsPointBased,
        int? TargetPoints);

    private sealed record ResourceJsonDto(
        string Name,
        string? Type,
        string? Usage,
        bool IsLimited);

    private sealed record GamePhaseJsonDto(
        string Name,
        string? Description,
        int Order,
        bool IsOptional);
}
