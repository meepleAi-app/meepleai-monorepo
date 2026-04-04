using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for RulebookAnalysis entities.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
internal sealed class RulebookAnalysisRepository : RepositoryBase, IRulebookAnalysisRepository
{

    public RulebookAnalysisRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(RulebookAnalysis analysis, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(analysis);
        await DbContext.RulebookAnalyses.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RulebookAnalysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.RulebookAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<RulebookAnalysis?> GetActiveAnalysisAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.RulebookAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.SharedGameId == sharedGameId
                     && a.PdfDocumentId == pdfDocumentId
                     && a.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<List<RulebookAnalysis>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.RulebookAnalyses
            .AsNoTracking()
            .Where(a => a.SharedGameId == sharedGameId)
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<RulebookAnalysis>> GetByPdfDocumentIdAsync(
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.RulebookAnalyses
            .AsNoTracking()
            .Where(a => a.PdfDocumentId == pdfDocumentId)
            .OrderByDescending(a => a.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public void Update(RulebookAnalysis analysis)
    {
        var entity = MapToEntity(analysis);
        DbContext.RulebookAnalyses.Update(entity);
    }

    public async Task DeactivateAllAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default)
    {
        await DbContext.RulebookAnalyses
            .Where(a => a.SharedGameId == sharedGameId
                        && a.PdfDocumentId == pdfDocumentId
                        && a.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.IsActive, false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string> GetPdfTextAsync(Guid pdfDocumentId, CancellationToken cancellationToken = default)
    {
        var pdfDocument = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == pdfDocumentId)
            .Select(p => new { p.ExtractedText })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return pdfDocument?.ExtractedText ?? string.Empty;
    }

    public async Task<string?> GetPdfDocumentCategoryAsync(Guid pdfDocumentId, CancellationToken cancellationToken = default)
    {
        return await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == pdfDocumentId)
            .Select(p => p.DocumentCategory)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<GamePdfPair>> GetGamePdfPairsWithReadyTextAsync(CancellationToken cancellationToken = default)
    {
        var pairs = await DbContext.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .Join(
                DbContext.PdfDocuments.Where(p => p.ProcessingState == "Ready" && p.ExtractedText != null && p.ExtractedText != ""),
                g => g.Id, p => p.SharedGameId,
                (g, p) => new GamePdfPair(g.Id, g.Title, p.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return pairs;
    }

    public async Task<HashSet<string>> GetActiveAnalysisKeysAsync(CancellationToken cancellationToken = default)
    {
        var activeKeys = await DbContext.RulebookAnalyses
            .AsNoTracking()
            .Where(a => a.IsActive)
            .Select(a => a.SharedGameId + ":" + a.PdfDocumentId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new HashSet<string>(activeKeys, StringComparer.Ordinal);
    }

    // Mapping methods

    private static RulebookAnalysis MapToDomain(RulebookAnalysisEntity entity)
    {
        var keyMechanics = JsonSerializer.Deserialize<List<string>>(entity.KeyMechanicsJson) ?? new List<string>();

        VictoryConditions? victoryConditions = null;
        if (!string.IsNullOrEmpty(entity.VictoryConditionsJson))
        {
            var vcDto = JsonSerializer.Deserialize<VictoryConditionsDto>(entity.VictoryConditionsJson);
            if (vcDto is not null)
            {
                victoryConditions = VictoryConditions.Create(
                    vcDto.Primary,
                    vcDto.Alternatives,
                    vcDto.IsPointBased,
                    vcDto.TargetPoints);
            }
        }

        var resources = JsonSerializer.Deserialize<List<ResourceDto>>(entity.ResourcesJson)?
            .Select(r => Resource.Create(r.Name, r.Type, r.Usage, r.IsLimited))
            .ToList() ?? new List<Resource>();

        var gamePhases = JsonSerializer.Deserialize<List<GamePhaseDto>>(entity.GamePhasesJson)?
            .Select(p => GamePhase.Create(p.Name, p.Description, p.Order, p.IsOptional))
            .ToList() ?? new List<GamePhase>();

        var commonQuestions = JsonSerializer.Deserialize<List<string>>(entity.CommonQuestionsJson) ?? new List<string>();

        var keyConcepts = JsonSerializer.Deserialize<List<KeyConceptDto>>(entity.KeyConceptsJson)?
            .Select(kc => new KeyConcept(kc.Term, kc.Definition, kc.Category))
            .ToList() ?? new List<KeyConcept>();

        var generatedFaqs = JsonSerializer.Deserialize<List<GeneratedFaqDto>>(entity.GeneratedFaqsJson)?
            .Select(f => new GeneratedFaq(f.Question, f.Answer, f.SourceSection, f.Confidence, f.Tags))
            .ToList() ?? new List<GeneratedFaq>();

        return new RulebookAnalysis(
            entity.Id,
            entity.SharedGameId,
            entity.PdfDocumentId,
            entity.GameTitle,
            entity.Summary,
            keyMechanics,
            victoryConditions,
            resources,
            gamePhases,
            commonQuestions,
            entity.ConfidenceScore,
            entity.Version,
            entity.IsActive,
            (GenerationSource)entity.Source,
            entity.AnalyzedAt,
            entity.CreatedBy,
            keyConcepts,
            generatedFaqs,
            entity.GameStateSchemaJson,
            (AnalysisCompletionStatus)entity.CompletionStatus,
            JsonSerializer.Deserialize<List<string>>(entity.MissingSectionsJson) ?? new List<string>());
    }

    private static RulebookAnalysisEntity MapToEntity(RulebookAnalysis analysis)
    {
        var victoryConditionsJson = analysis.VictoryConditions is not null
            ? JsonSerializer.Serialize(new VictoryConditionsDto(
                analysis.VictoryConditions.Primary,
                analysis.VictoryConditions.Alternatives.ToList(),
                analysis.VictoryConditions.IsPointBased,
                analysis.VictoryConditions.TargetPoints))
            : null;

        var resourceDtos = analysis.Resources
            .Select(r => new ResourceDto(r.Name, r.Type, r.Usage, r.IsLimited))
            .ToList();

        var phaseDtos = analysis.GamePhases
            .Select(p => new GamePhaseDto(p.Name, p.Description, p.Order, p.IsOptional))
            .ToList();

        return new RulebookAnalysisEntity
        {
            Id = analysis.Id,
            SharedGameId = analysis.SharedGameId,
            PdfDocumentId = analysis.PdfDocumentId,
            GameTitle = analysis.GameTitle,
            Summary = analysis.Summary,
            KeyMechanicsJson = JsonSerializer.Serialize(analysis.KeyMechanics.ToList()),
            VictoryConditionsJson = victoryConditionsJson,
            ResourcesJson = JsonSerializer.Serialize(resourceDtos),
            GamePhasesJson = JsonSerializer.Serialize(phaseDtos),
            CommonQuestionsJson = JsonSerializer.Serialize(analysis.CommonQuestions.ToList()),
            KeyConceptsJson = JsonSerializer.Serialize(
                analysis.KeyConcepts.Select(kc => new KeyConceptDto(kc.Term, kc.Definition, kc.Category)).ToList()),
            GeneratedFaqsJson = JsonSerializer.Serialize(
                analysis.GeneratedFaqs.Select(f => new GeneratedFaqDto(f.Question, f.Answer, f.SourceSection, f.Confidence, f.Tags)).ToList()),
            GameStateSchemaJson = analysis.GameStateSchemaJson,
            CompletionStatus = (int)analysis.CompletionStatus,
            MissingSectionsJson = JsonSerializer.Serialize(analysis.MissingSections.ToList()),
            ConfidenceScore = analysis.ConfidenceScore,
            Version = analysis.Version,
            IsActive = analysis.IsActive,
            Source = (int)analysis.Source,
            AnalyzedAt = analysis.AnalyzedAt,
            CreatedBy = analysis.CreatedBy
        };
    }

    // DTOs for JSON serialization
    private sealed record VictoryConditionsDto(
        string Primary,
        List<string> Alternatives,
        bool IsPointBased,
        int? TargetPoints);

    private sealed record ResourceDto(string Name, string Type, string? Usage, bool IsLimited);
    private sealed record GamePhaseDto(string Name, string Description, int Order, bool IsOptional);
    private sealed record KeyConceptDto(string Term, string Definition, string Category);
    private sealed record GeneratedFaqDto(string Question, string Answer, string SourceSection, decimal Confidence, List<string> Tags);
}
