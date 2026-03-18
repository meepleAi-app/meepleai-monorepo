using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Handles <see cref="CastVoteOnDisputeCommand"/>.
/// 1. Checks feature flag for democratic override
/// 2. Loads the dispute
/// 3. Delegates vote recording to domain
/// 4. Persists the updated dispute
/// </summary>
internal sealed class CastVoteOnDisputeCommandHandler
    : ICommandHandler<CastVoteOnDisputeCommand>
{
    private readonly IRuleDisputeRepository _disputeRepository;
    private readonly IFeatureFlagService _featureFlagService;

    public CastVoteOnDisputeCommandHandler(
        IRuleDisputeRepository disputeRepository,
        IFeatureFlagService featureFlagService)
    {
        _disputeRepository = disputeRepository ?? throw new ArgumentNullException(nameof(disputeRepository));
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
    }

    public async Task Handle(
        CastVoteOnDisputeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Check feature flag
        var isEnabled = await _featureFlagService
            .IsEnabledAsync("Features:Arbitro.DemocraticOverride")
            .ConfigureAwait(false);

        if (!isEnabled)
        {
            throw new InvalidOperationException("Feature Arbitro.DemocraticOverride is disabled");
        }

        // 2. Get dispute
        var dispute = await _disputeRepository
            .GetByIdAsync(command.DisputeId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("RuleDispute", command.DisputeId.ToString());

        // 3. Cast vote (domain validates duplicate votes)
        dispute.CastVote(command.PlayerId, command.AcceptsVerdict);

        // 4. Update
        await _disputeRepository
            .UpdateAsync(dispute, cancellationToken)
            .ConfigureAwait(false);
    }
}
