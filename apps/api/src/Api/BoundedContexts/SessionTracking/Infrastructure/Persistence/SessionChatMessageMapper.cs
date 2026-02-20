using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Maps between SessionChatMessage domain entity and SessionChatMessageEntity persistence entity.
/// Issue #4760
/// </summary>
internal static class SessionChatMessageMapper
{
    public static SessionChatMessageEntity ToEntity(SessionChatMessage domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new SessionChatMessageEntity
        {
            Id = domain.Id,
            SessionId = domain.SessionId,
            SenderId = domain.SenderId,
            Content = domain.Content,
            MessageType = domain.MessageType.ToString(),
            TurnNumber = domain.TurnNumber,
            SequenceNumber = domain.SequenceNumber,
            AgentType = domain.AgentType,
            Confidence = domain.Confidence,
            CitationsJson = domain.CitationsJson,
            MentionsJson = domain.MentionsJson,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            IsDeleted = domain.IsDeleted,
            DeletedAt = domain.DeletedAt,
        };
    }

    public static SessionChatMessage ToDomain(SessionChatMessageEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var message = (SessionChatMessage)Activator.CreateInstance(typeof(SessionChatMessage), true)!;

        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.Id))!.SetValue(message, entity.Id);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.SessionId))!.SetValue(message, entity.SessionId);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.SenderId))!.SetValue(message, entity.SenderId);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.Content))!.SetValue(message, entity.Content);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.MessageType))!.SetValue(message, Enum.Parse<SessionChatMessageType>(entity.MessageType));
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.TurnNumber))!.SetValue(message, entity.TurnNumber);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.SequenceNumber))!.SetValue(message, entity.SequenceNumber);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.AgentType))!.SetValue(message, entity.AgentType);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.Confidence))!.SetValue(message, entity.Confidence);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.CitationsJson))!.SetValue(message, entity.CitationsJson);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.MentionsJson))!.SetValue(message, entity.MentionsJson);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.CreatedAt))!.SetValue(message, entity.CreatedAt);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.UpdatedAt))!.SetValue(message, entity.UpdatedAt);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.IsDeleted))!.SetValue(message, entity.IsDeleted);
        typeof(SessionChatMessage).GetProperty(nameof(SessionChatMessage.DeletedAt))!.SetValue(message, entity.DeletedAt);

        return message;
    }
}
