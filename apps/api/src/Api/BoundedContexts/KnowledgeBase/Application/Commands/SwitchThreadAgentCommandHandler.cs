using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles switching the agent type on a chat thread mid-conversation (Issue #4465).
/// </summary>
internal class SwitchThreadAgentCommandHandler : ICommandHandler<SwitchThreadAgentCommand, ChatThreadDto>
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SwitchThreadAgentCommandHandler(
        IChatThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ChatThreadDto> Handle(SwitchThreadAgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var thread = await _threadRepository.GetByIdAsync(command.ThreadId, cancellationToken).ConfigureAwait(false);
        if (thread == null)
            throw new InvalidOperationException($"Thread with ID {command.ThreadId} not found");

        // #4998: pre-check state so a closed thread maps to 409 (not 500), as in CloseThreadCommandHandler
        // (#3224); the domain guard in SwitchAgent() stays as defense-in-depth.
        if (thread.Status.IsClosed)
            throw new ConflictException("Cannot switch the intent of a closed thread");

        thread.SwitchAgent(command.AgentType);

        await _threadRepository.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(thread);
    }

    private static ChatThreadDto MapToDto(Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread thread)
    {
        var messageDtos = thread.Messages.Select(ChatMessageDtoMapping.ToDto).ToList();

        return new ChatThreadDto(
            Id: thread.Id,
            UserId: thread.UserId,
            GameId: thread.GameId,
            PrivateGameId: thread.PrivateGameId,
            AgentId: thread.AgentId,
            Title: thread.Title,
            Status: thread.Status.Value,
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            MessageCount: thread.MessageCount,
            Messages: messageDtos,
            AgentType: thread.AgentType // #4998: the caller sees the intent it has just written
        );
    }
}
