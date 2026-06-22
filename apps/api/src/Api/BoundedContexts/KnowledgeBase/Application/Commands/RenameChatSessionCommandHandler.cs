using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles the RenameChatSessionCommand.
/// Validates the session exists, updates its title, and returns the updated DTO.
/// Issue #905: SG4 — Sessions CRUD naming disambiguation.
/// </summary>
internal sealed class RenameChatSessionCommandHandler : ICommandHandler<RenameChatSessionCommand, ChatSessionDto>
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RenameChatSessionCommandHandler(
        IChatSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ChatSessionDto> Handle(RenameChatSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Retrieve session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
            throw new NotFoundException("ChatSession", command.SessionId.ToString());

        // Update title (domain validates max 200 chars, non-empty, trim)
        session.SetTitle(command.Title);

        // Persist
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO
        return MapToDto(session);
    }

    private static ChatSessionDto MapToDto(Domain.Entities.ChatSession session)
    {
        var messageDtos = session.Messages.Select(m => new ChatSessionMessageDto(
            Id: m.Id,
            Role: m.Role,
            Content: m.Content,
            Timestamp: m.Timestamp,
            SequenceNumber: m.SequenceNumber,
            Metadata: m.GetMetadata()
        )).ToList();

        return new ChatSessionDto(
            Id: session.Id,
            UserId: session.UserId,
            GameId: session.GameId,
            UserLibraryEntryId: session.UserLibraryEntryId,
            AgentSessionId: session.AgentSessionId,
            AgentId: session.AgentId,
            AgentType: session.AgentType,
            AgentName: session.AgentName,
            Title: session.Title,
            AgentConfigJson: session.AgentConfigJson,
            CreatedAt: session.CreatedAt,
            LastMessageAt: session.LastMessageAt,
            IsArchived: session.IsArchived,
            MessageCount: session.MessageCount,
            Messages: messageDtos);
    }
}
