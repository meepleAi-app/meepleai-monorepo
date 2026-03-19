using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for SaveTestResultCommand.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal sealed class SaveTestResultCommandHandler : IRequestHandler<SaveTestResultCommand, Guid>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SaveTestResultCommandHandler> _logger;

    public SaveTestResultCommandHandler(
        IAgentTestResultRepository testResultRepository,
        IUnitOfWork unitOfWork,
        ILogger<SaveTestResultCommandHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        SaveTestResultCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Saving test result for typology {TypologyId} by user {ExecutedBy}",
            request.TypologyId,
            request.ExecutedBy);

        var testResult = AgentTestResult.Create(
            typologyId: request.TypologyId,
            query: request.Query,
            response: request.Response,
            modelUsed: request.ModelUsed,
            confidenceScore: request.ConfidenceScore,
            tokensUsed: request.TokensUsed,
            costEstimate: request.CostEstimate,
            latencyMs: request.LatencyMs,
            executedBy: request.ExecutedBy,
            strategyOverride: request.StrategyOverride,
            citationsJson: request.CitationsJson);

        await _testResultRepository.AddAsync(testResult, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Test result {TestResultId} saved successfully",
            testResult.Id);

        return testResult.Id;
    }
}
