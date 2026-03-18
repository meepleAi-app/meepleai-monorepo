using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Handles <see cref="OpenStructuredDisputeCommand"/>.
/// 1. Checks feature flag
/// 2. Loads session
/// 3. Verifies session has GameId
/// 4. Creates RuleDispute via factory method
/// 5. Links related disputes from game history
/// 6. Persists and returns dispute ID
/// </summary>
internal sealed class OpenStructuredDisputeCommandHandler
    : ICommandHandler<OpenStructuredDisputeCommand, Guid>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IRuleDisputeRepository _disputeRepository;
    private readonly IFeatureFlagService _featureFlagService;

    public OpenStructuredDisputeCommandHandler(
        ILiveSessionRepository sessionRepository,
        IRuleDisputeRepository disputeRepository,
        IFeatureFlagService featureFlagService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _disputeRepository = disputeRepository ?? throw new ArgumentNullException(nameof(disputeRepository));
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
    }

    public async Task<Guid> Handle(
        OpenStructuredDisputeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Check feature flag
        var isEnabled = await _featureFlagService
            .IsEnabledAsync("Features:Arbitro.StructuredDisputes")
            .ConfigureAwait(false);

        if (!isEnabled)
        {
            throw new InvalidOperationException("Feature Arbitro.StructuredDisputes is disabled");
        }

        // 2. Get session
        var session = await _sessionRepository
            .GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // 3. Verify session has a GameId
        if (session.GameId is null)
        {
            throw new InvalidOperationException("Session has no associated game");
        }

        // 4. Create dispute via factory method
        var dispute = RuleDispute.Open(
            command.SessionId,
            session.GameId.Value,
            command.InitiatorPlayerId,
            command.InitiatorClaim);

        // 5. Query cross-session disputes by GameId and set related dispute IDs (last 3)
        var gameDisputes = await _disputeRepository
            .GetByGameIdAsync(session.GameId.Value, cancellationToken)
            .ConfigureAwait(false);

        var relatedIds = gameDisputes
            .OrderByDescending(d => d.CreatedAt)
            .Take(3)
            .Select(d => d.Id)
            .ToList();

        if (relatedIds.Count > 0)
        {
            dispute.SetRelatedDisputeIds(relatedIds);
        }

        // 6. Save and return ID
        await _disputeRepository
            .AddAsync(dispute, cancellationToken)
            .ConfigureAwait(false);

        return dispute.Id;
    }
}
