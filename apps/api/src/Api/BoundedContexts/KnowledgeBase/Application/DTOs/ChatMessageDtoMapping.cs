using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Il messaggio di chat come lo vede il suo proprietario. Un solo punto di mappatura: con una copia
/// per handler, i metadati dell'agente (#2500) e poi la provenienza della risposta (#4168) arrivavano
/// solo alle letture che qualcuno si era ricordato di aggiornare, e non al thread restituito dalle
/// scritture.
/// </summary>
internal static class ChatMessageDtoMapping
{
    internal static ChatMessageDto ToDto(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new ChatMessageDto(
            Id: message.Id,
            Content: message.Content,
            Role: message.Role,
            Timestamp: message.Timestamp,
            SequenceNumber: message.SequenceNumber,
            UpdatedAt: message.UpdatedAt,
            IsDeleted: message.IsDeleted,
            DeletedAt: message.DeletedAt,
            DeletedByUserId: message.DeletedByUserId,
            IsInvalidated: message.IsInvalidated,
            AgentType: message.AgentType,
            Confidence: message.Confidence,
            CitationsJson: message.CitationsJson,
            TokenCount: message.TokenCount,
            ProfileVersionId: message.ProfileVersionId,
            Language: message.Language);
    }
}
