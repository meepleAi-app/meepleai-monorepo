using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetTestResultByIdQuery.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal sealed class GetTestResultByIdQueryHandler : IRequestHandler<GetTestResultByIdQuery, AgentTestResultDto?>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly ILogger<GetTestResultByIdQueryHandler> _logger;

    public GetTestResultByIdQueryHandler(
        IAgentTestResultRepository testResultRepository,
        ILogger<GetTestResultByIdQueryHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentTestResultDto?> Handle(
        GetTestResultByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Getting test result {TestResultId}",
            request.Id);

        var result = await _testResultRepository.GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (result == null)
        {
            _logger.LogWarning(
                "Test result {TestResultId} not found",
                request.Id);
            return null;
        }

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
