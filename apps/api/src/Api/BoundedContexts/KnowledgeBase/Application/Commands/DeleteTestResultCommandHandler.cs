using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for DeleteTestResultCommand.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal sealed class DeleteTestResultCommandHandler : IRequestHandler<DeleteTestResultCommand, bool>
{
    private readonly IAgentTestResultRepository _testResultRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteTestResultCommandHandler> _logger;

    public DeleteTestResultCommandHandler(
        IAgentTestResultRepository testResultRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteTestResultCommandHandler> logger)
    {
        _testResultRepository = testResultRepository ?? throw new ArgumentNullException(nameof(testResultRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        DeleteTestResultCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Deleting test result {TestResultId} requested by {RequestedBy}",
            request.Id,
            request.RequestedBy);

        var exists = await _testResultRepository.ExistsAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            _logger.LogWarning(
                "Test result {TestResultId} not found",
                request.Id);
            return false;
        }

        await _testResultRepository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Test result {TestResultId} deleted successfully",
            request.Id);

        return true;
    }
}
