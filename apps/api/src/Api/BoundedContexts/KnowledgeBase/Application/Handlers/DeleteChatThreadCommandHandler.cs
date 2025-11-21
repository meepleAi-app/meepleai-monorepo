using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for DeleteChatThreadCommand.
/// Deletes a chat thread after verifying user ownership.
/// </summary>
public class DeleteChatThreadCommandHandler : ICommandHandler<DeleteChatThreadCommand, bool>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteChatThreadCommandHandler> _logger;

    public DeleteChatThreadCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteChatThreadCommandHandler> logger)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(DeleteChatThreadCommand request, CancellationToken cancellationToken)
    {
        // Load thread
        var thread = await _threadRepository.GetByIdAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return false;
        }

        // Verify ownership
        if (thread.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException(
                $"User {request.UserId} does not have permission to delete thread {request.ThreadId}");
        }

        // Delete
        await _threadRepository.DeleteAsync(thread, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted chat thread {ThreadId} for user {UserId}",
            request.ThreadId, request.UserId);

        return true;
    }
}
