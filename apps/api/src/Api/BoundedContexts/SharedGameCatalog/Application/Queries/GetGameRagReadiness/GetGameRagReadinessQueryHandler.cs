using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameRagReadiness;

/// <summary>
/// Handler for GetGameRagReadinessQuery.
/// Aggregates RAG readiness status across SharedGameCatalog, DocumentProcessing,
/// and KnowledgeBase bounded contexts.
/// </summary>
internal sealed class GetGameRagReadinessQueryHandler
    : IRequestHandler<GetGameRagReadinessQuery, GameRagReadinessDto>
{
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IAgentDefinitionRepository _agentDefinitionRepository;
    private readonly ILogger<GetGameRagReadinessQueryHandler> _logger;

    public GetGameRagReadinessQueryHandler(
        ISharedGameRepository sharedGameRepository,
        ISharedGameDocumentRepository documentRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        IAgentDefinitionRepository agentDefinitionRepository,
        ILogger<GetGameRagReadinessQueryHandler> logger)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _agentDefinitionRepository = agentDefinitionRepository ?? throw new ArgumentNullException(nameof(agentDefinitionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GameRagReadinessDto> Handle(
        GetGameRagReadinessQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Step 1: Get SharedGame
        var game = await _sharedGameRepository.GetByIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new NotFoundException("SharedGame", request.GameId.ToString());
        }

        // Step 2: Get SharedGameDocuments linked to this game
        var sharedGameDocuments = await _documentRepository.GetBySharedGameIdAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false);

        // Step 3: Get PdfDocument entities for processing status
        var pdfDocumentIds = sharedGameDocuments.Select(d => d.PdfDocumentId).Distinct().ToList();
        var pdfDocuments = pdfDocumentIds.Count > 0
            ? await _pdfDocumentRepository.GetByIdsAsync(pdfDocumentIds, cancellationToken).ConfigureAwait(false)
            : [];

        // Build a lookup by PdfDocument ID
        var pdfLookup = pdfDocuments.ToDictionary(p => p.Id);

        // Step 4: Map documents to DTOs
        var documentDtos = new List<DocumentStatusDto>();
        foreach (var sharedDoc in sharedGameDocuments)
        {
            if (pdfLookup.TryGetValue(sharedDoc.PdfDocumentId, out var pdf))
            {
                documentDtos.Add(new DocumentStatusDto(
                    DocumentId: pdf.Id,
                    FileName: pdf.FileName.Value,
                    ProcessingState: pdf.ProcessingState.ToString(),
                    ProgressPercentage: pdf.ProgressPercentage,
                    IsActiveForRag: pdf.IsActiveForRag,
                    ErrorMessage: pdf.ProcessingError
                ));
            }
        }

        // Step 5: Get linked agent if present
        AgentInfoDto? agentInfo = null;
        if (game.AgentDefinitionId.HasValue)
        {
            var agentDef = await _agentDefinitionRepository.GetByIdAsync(
                game.AgentDefinitionId.Value, cancellationToken).ConfigureAwait(false);

            if (agentDef is not null)
            {
                agentInfo = new AgentInfoDto(
                    AgentId: agentDef.Id,
                    Name: agentDef.Name,
                    Type: agentDef.Type.Value,
                    IsActive: agentDef.IsActive,
                    IsReady: agentDef.IsActive
                );
            }
            else
            {
                _logger.LogWarning(
                    "GetGameRagReadiness: AgentDefinition {AgentDefinitionId} not found for game {GameId}",
                    game.AgentDefinitionId, request.GameId);
            }
        }

        // Step 6: Calculate readiness metrics
        var totalDocuments = documentDtos.Count;
        var readyDocuments = documentDtos.Count(d =>
            string.Equals(d.ProcessingState, PdfProcessingState.Ready.ToString(), StringComparison.Ordinal));
        var processingDocuments = documentDtos.Count(d =>
            !string.Equals(d.ProcessingState, PdfProcessingState.Ready.ToString(), StringComparison.Ordinal) &&
            !string.Equals(d.ProcessingState, PdfProcessingState.Failed.ToString(), StringComparison.Ordinal));
        var failedDocuments = documentDtos.Count(d =>
            string.Equals(d.ProcessingState, PdfProcessingState.Failed.ToString(), StringComparison.Ordinal));

        // Step 7: Determine overall readiness
        var overallReadiness = CalculateOverallReadiness(
            totalDocuments, readyDocuments, processingDocuments, failedDocuments, agentInfo);

        return new GameRagReadinessDto(
            GameId: game.Id,
            GameTitle: game.Title,
            GameStatus: game.Status.ToString(),
            TotalDocuments: totalDocuments,
            ReadyDocuments: readyDocuments,
            ProcessingDocuments: processingDocuments,
            FailedDocuments: failedDocuments,
            Documents: documentDtos,
            LinkedAgent: agentInfo,
            OverallReadiness: overallReadiness
        );
    }

    private static string CalculateOverallReadiness(
        int totalDocuments,
        int readyDocuments,
        int processingDocuments,
        int failedDocuments,
        AgentInfoDto? agent)
    {
        if (totalDocuments == 0)
            return "no_documents";

        if (processingDocuments > 0)
            return "documents_processing";

        if (failedDocuments == totalDocuments)
            return "documents_failed";

        if (readyDocuments > 0 && agent is null)
            return "ready_for_agent";

        if (readyDocuments > 0 && agent is not null)
            return "fully_operational";

        return "no_documents";
    }
}
