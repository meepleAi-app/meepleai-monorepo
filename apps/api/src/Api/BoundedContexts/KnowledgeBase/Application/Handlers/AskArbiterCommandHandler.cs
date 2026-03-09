using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for AskArbiterCommand.
/// Resolves disputes by searching the rulebook, extracting citations, and providing a structured verdict.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
internal sealed class AskArbiterCommandHandler : IRequestHandler<AskArbiterCommand, ArbiterVerdictDto>
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILlmService _llmService;
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<AskArbiterCommandHandler> _logger;

    /// <summary>
    /// Confidence threshold above which a verdict is considered conclusive.
    /// </summary>
    internal const double ConclusiveThreshold = 0.85;

    public AskArbiterCommandHandler(
        IAgentRepository agentRepository,
        ILlmService llmService,
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        MeepleAiDbContext dbContext,
        ILogger<AskArbiterCommandHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ArbiterVerdictDto> Handle(AskArbiterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Starting arbiter verdict for Agent {AgentId}, Session {SessionId}",
            command.AgentId, command.SessionId);

        // 1. Load agent and validate
        var agent = await _agentRepository
            .GetByIdAsync(command.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent == null)
        {
            throw new NotFoundException("Agent", command.AgentId.ToString());
        }

        // 2. Load agent configuration and selected documents
        var agentConfig = await _dbContext.AgentConfigurations
            .FirstOrDefaultAsync(
                c => c.AgentId == command.AgentId && c.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        var selectedDocumentIds = new List<Guid>();
        if (agentConfig != null && !string.IsNullOrEmpty(agentConfig.SelectedDocumentIdsJson))
        {
            try
            {
                selectedDocumentIds = JsonSerializer.Deserialize<List<Guid>>(agentConfig.SelectedDocumentIdsJson)
                    ?? new List<Guid>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse document IDs for agent {AgentId}", command.AgentId);
            }
        }

        // 3. Build the combined search query from the dispute
        var searchQuery = BuildSearchQuery(command.Situation, command.PositionA, command.PositionB);

        // 4. Generate embedding
        var embeddingResult = await _embeddingService
            .GenerateEmbeddingAsync(searchQuery, cancellationToken)
            .ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogError("Embedding generation failed for arbiter query, Agent {AgentId}", command.AgentId);
            return BuildNoContextVerdict();
        }

        // 5. Resolve game ID and PDF document IDs for Qdrant search
        Guid? gameIdForSearch = agent.GameId;
        var pdfDocumentIds = new List<string>();

        if (selectedDocumentIds.Count > 0)
        {
            var vectorDocs = await _dbContext.VectorDocuments
                .Where(vd => selectedDocumentIds.Contains(vd.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!gameIdForSearch.HasValue && vectorDocs.Count > 0)
            {
                gameIdForSearch = vectorDocs[0].GameId;
            }

            // Qdrant stores pdf_id without hyphens (ToString("N"))
            pdfDocumentIds = vectorDocs.Select(vd => vd.PdfDocumentId.ToString("N")).ToList();
        }

        var gameId = gameIdForSearch?.ToString() ?? "default";
        var profile = TypologyProfile.Arbitro();

        // 6. Vector search in Qdrant
        var searchResult = await _qdrantService.SearchAsync(
            gameId,
            embeddingResult.Embeddings[0],
            limit: profile.TopK,
            documentIds: pdfDocumentIds,
            cancellationToken).ConfigureAwait(false);

        if (!searchResult.Success)
        {
            _logger.LogError("Vector search failed for arbiter query, Agent {AgentId}: {Error}",
                command.AgentId, searchResult.ErrorMessage);
            return BuildNoContextVerdict();
        }

        // 7. Filter by minimum score and deduplicate
        var relevantChunks = searchResult.Results
            .Where(r => r.Score >= profile.MinScore)
            .OrderByDescending(r => r.Score)
            .GroupBy(r => (PdfId: r.PdfId.Replace("-", "", StringComparison.Ordinal), r.ChunkIndex))
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation(
            "Arbiter retrieved {ChunkCount} relevant chunks for Agent {AgentId}",
            relevantChunks.Count, command.AgentId);

        // 8. Extract citations from retrieved chunks
        var citations = await BuildCitationsAsync(relevantChunks, cancellationToken).ConfigureAwait(false);

        // 9. Calculate confidence: avg(chunk_scores) * (citations > 0 ? 1.0 : 0.5)
        var confidence = CalculateConfidence(relevantChunks, citations);
        var isConclusive = confidence >= ConclusiveThreshold;

        // 10. Build arbiter system prompt
        var gameName = await ResolveGameNameAsync(agent.GameId, cancellationToken).ConfigureAwait(false);
        var systemPrompt = BuildArbiterSystemPrompt(gameName);
        var userPrompt = BuildArbiterUserPrompt(
            command.Situation, command.PositionA, command.PositionB, relevantChunks);

        // 11. Call LLM for verdict
        var llmResult = await _llmService
            .GenerateCompletionAsync(systemPrompt, userPrompt, RequestSource.Manual, cancellationToken)
            .ConfigureAwait(false);

        if (!llmResult.Success)
        {
            _logger.LogError("LLM call failed for arbiter verdict, Agent {AgentId}: {Error}",
                command.AgentId, llmResult.ErrorMessage);
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
            "Arbiter verdict generated for Agent {AgentId}: confidence={Confidence:F2}, citations={CitationCount}, conclusive={IsConclusive}",
            command.AgentId, confidence, citations.Count, isConclusive);

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
        IReadOnlyList<SearchResultItem> chunks,
        IReadOnlyList<ArbiterCitationDto> citations)
    {
        if (chunks.Count == 0)
            return 0;

        var avgScore = chunks.Average(c => (double)c.Score);
        var citationMultiplier = citations.Count > 0 ? 1.0 : 0.5;
        return avgScore * citationMultiplier;
    }

    /// <summary>
    /// Extracts structured citations from retrieved chunks, resolving document titles.
    /// </summary>
    private async Task<List<ArbiterCitationDto>> BuildCitationsAsync(
        IReadOnlyList<SearchResultItem> chunks,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
            return new List<ArbiterCitationDto>();

        // Resolve PDF document titles by PdfId
        var pdfIds = chunks
            .Select(c => c.PdfId.Replace("-", "", StringComparison.Ordinal))
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
            var normalizedPdfId = chunk.PdfId.Replace("-", "", StringComparison.Ordinal);
            var documentTitle = pdfTitles.GetValueOrDefault(normalizedPdfId, chunk.PdfId);

            return new ArbiterCitationDto
            {
                DocumentTitle = documentTitle,
                Section = chunk.Page > 0 ? $"Pagina {chunk.Page}" : "Sezione sconosciuta",
                Text = chunk.Text.Length > 500
                    ? string.Concat(chunk.Text.AsSpan(0, 497), "...")
                    : chunk.Text,
                RelevanceScore = chunk.Score
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
        IReadOnlyList<SearchResultItem> chunks)
    {
        var sections = new List<string>();

        // RAG context
        if (chunks.Count > 0)
        {
            var contextParts = chunks.Select((chunk, index) =>
                $"[{index + 1}] (Score: {chunk.Score:F2}, Pagina: {chunk.Page})\n{chunk.Text}");
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

    private static ArbiterVerdictDto BuildNoContextVerdict()
    {
        return new ArbiterVerdictDto
        {
            Verdict = "Impossibile emettere un verdetto: nessun contesto disponibile dalla Knowledge Base.",
            Confidence = 0,
            IsConclusive = false,
            Citations = new List<ArbiterCitationDto>(),
            ExpansionWarning = "Nessun documento analizzato trovato. Verifica che i PDF del regolamento siano stati caricati e indicizzati."
        };
    }
}
