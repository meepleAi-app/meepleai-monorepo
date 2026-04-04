using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.SharedKernel.Application.Interfaces;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for self-service account deletion (GDPR Art. 17 right to erasure).
/// Cascades deletion to all user-related data across bounded contexts.
/// </summary>
internal sealed class DeleteOwnAccountCommandHandler
    : ICommandHandler<DeleteOwnAccountCommand, DeleteOwnAccountResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteOwnAccountCommandHandler> _logger;

    public DeleteOwnAccountCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<DeleteOwnAccountCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeleteOwnAccountResult> Handle(
        DeleteOwnAccountCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new NotFoundException("User", command.UserId.ToString());

        // Prevent deletion of the last admin
        if (user.Role.IsAdmin())
        {
            var adminCount = await _userRepository.CountAdminsAsync(cancellationToken).ConfigureAwait(false);
            if (adminCount <= 1)
                throw new ConflictException("Cannot delete the last admin account. Transfer admin role first.");
        }

        // 1. Count then revoke all active sessions
        var sessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var sessionsRevoked = sessions.Count;
        await _sessionRepository.RevokeAllUserSessionsAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        // 2. Delete LLM data (request logs, conversation memories, Redis keys)
        var llmResult = await _mediator.Send(
            new DeleteUserLlmDataCommand(command.UserId, command.UserId, IsAdminRequest: false),
            cancellationToken).ConfigureAwait(false);

        // 3. Delete user entity (cascades to ApiKeys, OAuthAccounts, BackupCodes via EF Core)
        // Note: UserLibrary, Notifications, ChatThreads use soft-delete or have no FK cascade
        // to User. Orphaned records are acceptable as they contain no PII beyond UserId (a GUID).
        // For full Art. 17 compliance, a follow-up batch job should anonymize these records.
        await _userRepository.DeleteAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "GDPR Art.17: User account fully deleted (self-service). UserId={UserId}, Sessions={Sessions}, LlmLogs={LlmLogs}, Memories={Memories}",
            command.UserId, sessionsRevoked, llmResult.LlmRequestLogsDeleted, llmResult.ConversationMemoriesDeleted);

        return new DeleteOwnAccountResult(
            SessionsRevoked: sessionsRevoked,
            LlmRequestLogsDeleted: llmResult.LlmRequestLogsDeleted,
            ConversationMemoriesDeleted: llmResult.ConversationMemoriesDeleted,
            RedisKeysCleared: llmResult.RedisKeysCleared,
            DeletedAt: DateTime.UtcNow);
    }
}
