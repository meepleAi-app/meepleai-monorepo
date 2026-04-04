using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetTestResultsQuery.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal sealed class GetTestResultsQueryHandler : IRequestHandler<GetTestResultsQuery, AgentTestResultListDto>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly ILogger<GetTestResultsQueryHandler> _logger;

    public GetTestResultsQueryHandler(
        IAgentTestResultRepository testResultRepository,
        ILogger<GetTestResultsQueryHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentTestResultListDto> Handle(
        GetTestResultsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Getting test results with filters - TypologyId: {TypologyId}, ExecutedBy: {ExecutedBy}, SavedOnly: {SavedOnly}",
            request.TypologyId,
            request.ExecutedBy,
            request.SavedOnly);

        List<AgentTestResult> results;
        int totalCount;

        // Apply filters based on provided parameters
        if (request.SavedOnly == true)
        {
            results = await _testResultRepository.GetSavedAsync(
                request.ExecutedBy,
                request.Skip,
                request.Take,
                cancellationToken).ConfigureAwait(false);
            totalCount = results.Count; // Simplified for now
        }
        else if (request.TypologyId.HasValue)
        {
            results = await _testResultRepository.GetByTypologyIdAsync(
                request.TypologyId.Value,
                request.Skip,
                request.Take,
                cancellationToken).ConfigureAwait(false);
            totalCount = await _testResultRepository.GetCountByTypologyIdAsync(
                request.TypologyId.Value,
                cancellationToken).ConfigureAwait(false);
        }
        else if (request.ExecutedBy.HasValue)
        {
            results = await _testResultRepository.GetByExecutedByAsync(
                request.ExecutedBy.Value,
                request.Skip,
                request.Take,
                cancellationToken).ConfigureAwait(false);
            totalCount = results.Count; // Simplified for now
        }
        else if (request.From.HasValue && request.To.HasValue)
        {
            results = await _testResultRepository.GetByDateRangeAsync(
                request.From.Value,
                request.To.Value,
                request.Skip,
                request.Take,
                cancellationToken).ConfigureAwait(false);
            totalCount = results.Count; // Simplified for now
        }
        else
        {
            // Default: Return empty list if no filter specified
            results = new List<AgentTestResult>();
            totalCount = 0;
        }

        var dtos = results.Select(MapToDto).ToList();

        _logger.LogDebug(
            "Returning {Count} test results",
            dtos.Count);

        return new AgentTestResultListDto(dtos, totalCount, request.Skip, request.Take);
    }

    private static AgentTestResultDto MapToDto(AgentTestResult result)
    {
        return new AgentTestResultDto(
            Id: result.Id,
            TypologyId: result.TypologyId,
            StrategyOverride: result.StrategyOverride,
            ModelUsed: result.ModelUsed,
            Query: result.Query,
            Response: result.Response,
            ConfidenceScore: result.ConfidenceScore,
            TokensUsed: result.TokensUsed,
            CostEstimate: result.CostEstimate,
            LatencyMs: result.LatencyMs,
            CitationsJson: result.CitationsJson,
            ExecutedAt: result.ExecutedAt,
            ExecutedBy: result.ExecutedBy,
            Notes: result.Notes,
            IsSaved: result.IsSaved);
    }
}
