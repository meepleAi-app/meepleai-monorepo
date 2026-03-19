using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for CreateChatSessionCommand.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class CreateChatSessionCommandHandler : IRequestHandler<CreateChatSessionCommand, Guid>
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRagAccessService _ragAccessService;
    private readonly ILogger<CreateChatSessionCommandHandler> _logger;

    public CreateChatSessionCommandHandler(
        IChatSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        IRagAccessService ragAccessService,
        ILogger<CreateChatSessionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        CreateChatSessionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // RAG access enforcement
        {
            var userRole = Enum.TryParse<UserRole>(request.UserRole, ignoreCase: true, out var parsedRole)
                ? parsedRole : UserRole.User;
            var canAccess = await _ragAccessService.CanAccessRagAsync(
                request.UserId, request.GameId, userRole, cancellationToken).ConfigureAwait(false);
            if (!canAccess)
                throw new ForbiddenException("Accesso RAG non autorizzato");
        }

        _logger.LogInformation(
            "Creating chat session for user {UserId} and game {GameId}",
            request.UserId,
            request.GameId);

        // Auto-archive: sliding window when tier limit is reached (TierLimit > 0 means enforced)
        if (request.TierLimit > 0)
        {
            var currentCount = await _sessionRepository
                .CountByUserIdAsync(request.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (currentCount >= request.TierLimit)
            {
                var oldest = await _sessionRepository
                    .GetOldestActiveByUserIdAsync(request.UserId, cancellationToken)
                    .ConfigureAwait(false);

                if (oldest != null)
                {
                    oldest.Archive();
                    await _sessionRepository.UpdateAsync(oldest, cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation(
                        "Auto-archived oldest chat session {SessionId} for user {UserId} (tier limit={Limit})",
                        oldest.Id,
                        request.UserId,
                        request.TierLimit);
                }
            }
        }

        var sessionId = Guid.NewGuid();

        var session = new ChatSession(
            id: sessionId,
            userId: request.UserId,
            gameId: request.GameId,
            title: request.Title,
            userLibraryEntryId: request.UserLibraryEntryId,
            agentSessionId: request.AgentSessionId,
            agentConfigJson: request.AgentConfigJson,
            agentId: request.AgentId,
            agentType: request.AgentType,
            agentName: request.AgentName);

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Chat session {SessionId} created successfully",
            sessionId);

        return sessionId;
    }
}
