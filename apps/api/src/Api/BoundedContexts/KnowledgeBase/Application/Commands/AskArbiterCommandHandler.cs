using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for AskArbiterCommand.
/// Resolves disputes by searching the rulebook, extracting citations, and providing a structured verdict.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
internal sealed class AskArbiterCommandHandler : IRequestHandler<AskArbiterCommand, ArbiterVerdictDto>
{
    private readonly IAgentDefinitionRepository _definitionRepository;
    private readonly ILlmService _llmService;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<AskArbiterCommandHandler> _logger;

    /// <summary>
    /// Confidence threshold above which a verdict is considered conclusive.
    /// </summary>
    internal const double ConclusiveThreshold = 0.85;

    /// <summary>
    /// Minimum hybrid score for a chunk to be included in arbiter context.
    /// </summary>
    private const double ArbiterMinScore = 0.70;

    public AskArbiterCommandHandler(
        IAgentDefinitionRepository definitionRepository,
        ILlmService llmService,
        IHybridSearchService hybridSearchService,
        MeepleAiDbContext dbContext,
        IRagAccessService ragAccessService,
        ILogger<AskArbiterCommandHandler> logger)
    {
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ArbiterVerdictDto> Handle(AskArbiterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Starting arbiter verdict for AgentDefinition {AgentDefinitionId}, Session {SessionId}",
            command.AgentDefinitionId, command.SessionId);

        // 1. Load agent definition and validate
        var definition = await _definitionRepository
            .GetByIdAsync(command.AgentDefinitionId, cancellationToken)
            .ConfigureAwait(false);

        if (definition == null)
        {
            throw new NotFoundException("AgentDefinition", command.AgentDefinitionId.ToString());
        }

        // RAG access enforcement: resolve definition's game and check access
        if (definition.GameId is not null && definition.GameId != Guid.Empty)
        {
            var userRole = UserRole.User; // AskArbiterCommand doesn't carry UserRole; default to User
            var canAccess = await _ragAccessService.CanAccessRagAsync(
                command.UserId, definition.GameId.Value, userRole, cancellationToken).ConfigureAwait(false);
            if (!canAccess)
                throw new ForbiddenException("Accesso RAG non autorizzato");
        }

        // 2. Build the combined search query from the dispute
        var searchQuery = BuildSearchQuery(command.Situation, command.PositionA, command.PositionB);

        // 3. Perform hybrid search
        var hybridResults = new List<HybridSearchResult>();
        if (definition.GameId is not null && definition.GameId != Guid.Empty)
        {
            hybridResults = await _hybridSearchService.SearchAsync(
                searchQuery,
                definition.GameId.Value,
                SearchMode.Hybrid,
                limit: 10,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // 4. Filter by minimum score and deduplicate
        var relevantChunks = hybridResults
            .Where(r => r.HybridScore >= ArbiterMinScore)
            .OrderByDescending(r => r.HybridScore)
            .GroupBy(r => (PdfId: r.PdfDocumentId.Replace("-", "", StringComparison.Ordinal), r.ChunkIndex))
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation(
            "Arbiter retrieved {ChunkCount} relevant chunks for AgentDefinition {AgentDefinitionId}",
            relevantChunks.Count, command.AgentDefinitionId);

        // 5. Extract citations from retrieved chunks
        var citations = await BuildCitationsAsync(relevantChunks, cancellationToken).ConfigureAwait(false);

        // 6. Calculate confidence: avg(chunk_scores) * (citations > 0 ? 1.0 : 0.5)
        var confidence = CalculateConfidence(relevantChunks, citations);
        var isConclusive = confidence >= ConclusiveThreshold;

        // 7. Build arbiter system prompt
        var gameName = await ResolveGameNameAsync(definition.GameId, cancellationToken).ConfigureAwait(false);
        var systemPrompt = BuildArbiterSystemPrompt(gameName);
        var userPrompt = BuildArbiterUserPrompt(
            command.Situation, command.PositionA, command.PositionB, relevantChunks);

        // 8. Call LLM for verdict
        var llmResult = await _llmService
            .GenerateCompletionAsync(systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
            .ConfigureAwait(false);

        if (!llmResult.Success)
        {
            _logger.LogError("LLM call failed for arbiter verdict, AgentDefinition {AgentDefinitionId}: {Error}",
                command.AgentDefinitionId, llmResult.ErrorMessage);
            return new ArbiterVerdictDto
            {
                Verdict = "Impossibile generare il verdetto. Il servizio AI non e' al momento disponibile.",
                Confidence = 0,
                IsConclusive = false,
                Citations = citations,
                ExpansionWarning = null
            };
        }

        _logger.LogInformation(
            "Arbiter verdict generated for AgentDefinition {AgentDefinitionId}: confidence={Confidence:F2}, citations={CitationCount}, conclusive={IsConclusive}",
            command.AgentDefinitionId, confidence, citations.Count, isConclusive);

        return new ArbiterVerdictDto
        {
            Verdict = llmResult.Response,
            Confidence = confidence,
            IsConclusive = isConclusive,
            Citations = citations,
            ExpansionWarning = relevantChunks.Count == 0
                ? "Nessun documento analizzato trovato. Verifica che i PDF del regolamento siano stati caricati e indicizzati."
                : null
        };
    }

    /// <summary>
    /// Builds a combined search query from the dispute components for RAG retrieval.
    /// </summary>
    internal static string BuildSearchQuery(string situation, string positionA, string positionB)
    {
        return $"{situation} {positionA} {positionB}";
    }

    /// <summary>
    /// Calculates confidence: avg(chunk_scores) * (citations > 0 ? 1.0 : 0.5).
    /// Returns 0 if no chunks retrieved.
    /// </summary>
    internal static double CalculateConfidence(
        IReadOnlyList<HybridSearchResult> chunks,
        IReadOnlyList<ArbiterCitationDto> citations)
    {
        if (chunks.Count == 0)
            return 0;

        var avgScore = chunks.Average(c => (double)c.HybridScore);
        var citationMultiplier = citations.Count > 0 ? 1.0 : 0.5;
        return avgScore * citationMultiplier;
    }

    /// <summary>
    /// Extracts structured citations from retrieved chunks, resolving document titles.
    /// </summary>
    private async Task<List<ArbiterCitationDto>> BuildCitationsAsync(
        IReadOnlyList<HybridSearchResult> chunks,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
            return new List<ArbiterCitationDto>();

        // Resolve PDF document titles by PdfDocumentId
        var pdfIds = chunks
            .Select(c => c.PdfDocumentId.Replace("-", "", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Try to find matching PDF documents for title resolution
        var pdfTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var pdfDocs = await _dbContext.Set<PdfDocumentEntity>()
                .Where(p => pdfIds.Contains(p.Id.ToString()))
                .Select(p => new { p.Id, p.FileName })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var doc in pdfDocs)
            {
                pdfTitles[doc.Id.ToString("N")] = doc.FileName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve PDF document titles for citations");
        }

        return chunks.Select(chunk =>
        {
            var normalizedPdfId = chunk.PdfDocumentId.Replace("-", "", StringComparison.Ordinal);
            var documentTitle = pdfTitles.GetValueOrDefault(normalizedPdfId, chunk.PdfDocumentId);

            return new ArbiterCitationDto
            {
                DocumentTitle = documentTitle,
                Section = chunk.PageNumber.HasValue && chunk.PageNumber > 0
                    ? $"Pagina {chunk.PageNumber}"
                    : "Sezione sconosciuta",
                Text = chunk.Content.Length > 500
                    ? string.Concat(chunk.Content.AsSpan(0, 497), "...")
                    : chunk.Content,
                RelevanceScore = chunk.HybridScore
            };
        }).ToList();
    }

    /// <summary>
    /// Builds the arbiter-specific system prompt with Italian localization.
    /// </summary>
    internal static string BuildArbiterSystemPrompt(string? gameName)
    {
        var game = string.IsNullOrWhiteSpace(gameName) ? "giochi da tavolo" : gameName;

        return
            $"Sei l'arbitro ufficiale per il gioco \"{game}\". " +
            "Rispondi SOLO basandoti sul regolamento fornito nel contesto.\n\n" +
            "REGOLE DI COMPORTAMENTO:\n" +
            "- Cita sempre la fonte esatta (documento, pagina/sezione) per ogni affermazione.\n" +
            "- Indica chi ha ragione tra le posizioni presentate (Posizione A o Posizione B).\n" +
            "- Se entrambe le posizioni hanno elementi corretti, spiegalo chiaramente.\n" +
            "- Se il regolamento e' ambiguo su questo punto, dichiaralo esplicitamente.\n" +
            "- Non esprimere opinioni personali, basati solo sul regolamento ufficiale.\n" +
            "- Se il contesto non contiene informazioni sufficienti, dichiaralo chiaramente.\n\n" +
            "FORMATO RISPOSTA:\n" +
            "1. VERDETTO: Chi ha ragione (Posizione A / Posizione B / Entrambi parzialmente)\n" +
            "2. MOTIVAZIONE: Spiegazione basata sul regolamento\n" +
            "3. CITAZIONI: Regole esatte con pagina/sezione\n" +
            "4. ECCEZIONI: Eventuali casi speciali o interpretazioni alternative\n\n" +
            "Rispondi sempre in italiano.";
    }

    /// <summary>
    /// Builds the user prompt with dispute context and retrieved rulebook passages.
    /// </summary>
    internal static string BuildArbiterUserPrompt(
        string situation,
        string positionA,
        string positionB,
        IReadOnlyList<HybridSearchResult> chunks)
    {
        var sections = new List<string>();

        // RAG context
        if (chunks.Count > 0)
        {
            var contextParts = chunks.Select((chunk, index) =>
                $"[{index + 1}] (Score: {chunk.HybridScore:F2}, Pagina: {chunk.PageNumber ?? 0})\n{chunk.Content}");
            sections.Add($"=== Regolamento ===\n{string.Join("\n\n---\n\n", contextParts)}");
        }

        // Dispute details
        sections.Add(
            $"=== Disputa ===\n" +
            $"SITUAZIONE: {situation}\n\n" +
            $"POSIZIONE A: {positionA}\n\n" +
            $"POSIZIONE B: {positionB}");

        // Instructions
        if (chunks.Count == 0)
        {
            sections.Add(
                "NOTA: Nessun passaggio rilevante trovato nel regolamento. " +
                "Dichiara che non puoi emettere un verdetto basato sulla documentazione disponibile.");
        }
        else
        {
            sections.Add(
                "Emetti il tuo verdetto basandoti ESCLUSIVAMENTE sui passaggi del regolamento forniti sopra. " +
                "Cita i numeri di pagina dove applicabile.");
        }

        return string.Join("\n\n", sections);
    }

    private async Task<string?> ResolveGameNameAsync(Guid? gameId, CancellationToken ct)
    {
        if (!gameId.HasValue || gameId == Guid.Empty) return null;
        return await _dbContext.Games
            .Where(g => g.Id == gameId.Value)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
