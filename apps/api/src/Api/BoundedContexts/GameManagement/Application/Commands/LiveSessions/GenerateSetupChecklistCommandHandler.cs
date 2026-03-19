using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles GenerateSetupChecklistCommand.
/// 1. Checks SetupWizard feature flag
/// 2. Loads session from repository
/// 3. Streams setup guide from KnowledgeBase via RAG
/// 4. Converts streamed steps into SetupChecklistData
/// 5. Sets the checklist on the session domain entity
/// </summary>
internal class GenerateSetupChecklistCommandHandler
    : ICommandHandler<GenerateSetupChecklistCommand, SetupChecklistData>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateSetupChecklistCommandHandler(
        ILiveSessionRepository sessionRepository,
        IMediator mediator,
        IFeatureFlagService featureFlagService,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<SetupChecklistData> Handle(
        GenerateSetupChecklistCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Check feature flag
        var isEnabled = await _featureFlagService
            .IsEnabledAsync("Features:SetupWizard.Enabled")
            .ConfigureAwait(false);

        if (!isEnabled)
        {
            throw new InvalidOperationException("Feature SetupWizard.Enabled is disabled");
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

        // 4. Stream setup guide from KnowledgeBase
        var query = new StreamSetupGuideQuery(session.GameId.ToString()!, command.PlayerCount);
        var steps = new List<SetupStep>();
        var components = new List<SetupComponent>();

        await foreach (var evt in _mediator.CreateStream(query, cancellationToken).ConfigureAwait(false))
        {
            if (evt.Data is StreamingSetupStep setupStep)
            {
                steps.Add(new SetupStep(
                    setupStep.step.stepNumber,
                    setupStep.step.instruction));
            }
        }

        // 5. Build checklist data
        // If no steps were streamed, provide a single fallback step
        if (steps.Count == 0)
        {
            steps.Add(new SetupStep(1, "Follow the rulebook setup instructions."));
        }

        var checklist = new SetupChecklistData(
            command.PlayerCount,
            components,
            steps);

        // 6. Set checklist on session and persist
        session.SetSetupChecklist(checklist);

        await _sessionRepository
            .UpdateAsync(session, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return checklist;
    }
}
