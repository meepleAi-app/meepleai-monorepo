using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Maps between Session domain entity and SessionEntity persistence entity.
/// </summary>
internal static class SessionMapper
{
    public static SessionEntity ToEntity(Session domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new SessionEntity
        {
            Id = domain.Id,
            UserId = domain.UserId,
            GameId = domain.GameId,
            SessionCode = domain.SessionCode,
            SessionType = domain.SessionType.ToString(),
            Status = domain.Status.ToString(),
            SessionDate = domain.SessionDate,
            Location = domain.Location,
            FinalizedAt = domain.FinalizedAt,
            IsDeleted = domain.IsDeleted,
            DeletedAt = domain.DeletedAt,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            CreatedBy = domain.CreatedBy,
            UpdatedBy = domain.UpdatedBy,
            RowVersion = domain.RowVersion,
            Participants = domain.Participants.Select(ParticipantMapper.ToEntity).ToList()
        };
    }

    public static Session ToDomain(SessionEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Use reflection to create domain entity (private constructor)
        var session = (Session)Activator.CreateInstance(typeof(Session), true)!;

        typeof(Session).GetProperty(nameof(Session.Id))!.SetValue(session, entity.Id);
        typeof(Session).GetProperty(nameof(Session.UserId))!.SetValue(session, entity.UserId);
        typeof(Session).GetProperty(nameof(Session.GameId))!.SetValue(session, entity.GameId);
        typeof(Session).GetProperty(nameof(Session.SessionCode))!.SetValue(session, entity.SessionCode);
        typeof(Session).GetProperty(nameof(Session.SessionType))!.SetValue(session, Enum.Parse<SessionType>(entity.SessionType));
        typeof(Session).GetProperty(nameof(Session.Status))!.SetValue(session, Enum.Parse<SessionStatus>(entity.Status));
        typeof(Session).GetProperty(nameof(Session.SessionDate))!.SetValue(session, entity.SessionDate);
        typeof(Session).GetProperty(nameof(Session.Location))!.SetValue(session, entity.Location);
        typeof(Session).GetProperty(nameof(Session.FinalizedAt))!.SetValue(session, entity.FinalizedAt);
        typeof(Session).GetProperty(nameof(Session.IsDeleted))!.SetValue(session, entity.IsDeleted);
        typeof(Session).GetProperty(nameof(Session.DeletedAt))!.SetValue(session, entity.DeletedAt);
        typeof(Session).GetProperty(nameof(Session.CreatedAt))!.SetValue(session, entity.CreatedAt);
        typeof(Session).GetProperty(nameof(Session.UpdatedAt))!.SetValue(session, entity.UpdatedAt);
        typeof(Session).GetProperty(nameof(Session.CreatedBy))!.SetValue(session, entity.CreatedBy);
        typeof(Session).GetProperty(nameof(Session.UpdatedBy))!.SetValue(session, entity.UpdatedBy);
        typeof(Session).GetProperty(nameof(Session.RowVersion))!.SetValue(session, entity.RowVersion);

        // Map participants (need to access private _participants field)
        #pragma warning disable S3011 // Reflection is required for domain entity hydration from persistence
        var participantsField = typeof(Session).GetField("_participants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        #pragma warning restore S3011
        var participantsList = (List<Participant>)participantsField.GetValue(session)!;
        participantsList.Clear();
        participantsList.AddRange(entity.Participants.Select(ParticipantMapper.ToDomain));

        return session;
    }
}

/// <summary>
/// Maps between Participant domain entity and ParticipantEntity persistence entity.
/// </summary>
internal static class ParticipantMapper
{
    public static ParticipantEntity ToEntity(Participant domain)
    {
        return new ParticipantEntity
        {
            Id = domain.Id,
            SessionId = domain.SessionId,
            UserId = domain.UserId,
            DisplayName = domain.DisplayName,
            IsOwner = domain.IsOwner,
            JoinOrder = domain.JoinOrder,
            FinalRank = domain.FinalRank,
            CreatedAt = domain.CreatedAt
        };
    }

    public static Participant ToDomain(ParticipantEntity entity)
    {
        var participant = (Participant)Activator.CreateInstance(typeof(Participant), true)!;

        typeof(Participant).GetProperty(nameof(Participant.Id))!.SetValue(participant, entity.Id);
        typeof(Participant).GetProperty(nameof(Participant.SessionId))!.SetValue(participant, entity.SessionId);
        typeof(Participant).GetProperty(nameof(Participant.UserId))!.SetValue(participant, entity.UserId);
        typeof(Participant).GetProperty(nameof(Participant.DisplayName))!.SetValue(participant, entity.DisplayName);
        typeof(Participant).GetProperty(nameof(Participant.IsOwner))!.SetValue(participant, entity.IsOwner);
        typeof(Participant).GetProperty(nameof(Participant.JoinOrder))!.SetValue(participant, entity.JoinOrder);
        typeof(Participant).GetProperty(nameof(Participant.FinalRank))!.SetValue(participant, entity.FinalRank);
        typeof(Participant).GetProperty(nameof(Participant.CreatedAt))!.SetValue(participant, entity.CreatedAt);

        return participant;
    }
}

/// <summary>
/// Maps between ScoreEntry domain entity and ScoreEntryEntity persistence entity.
/// </summary>
internal static class ScoreEntryMapper
{
    public static ScoreEntryEntity ToEntity(ScoreEntry domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new ScoreEntryEntity
        {
            Id = domain.Id,
            SessionId = domain.SessionId,
            ParticipantId = domain.ParticipantId,
            RoundNumber = domain.RoundNumber,
            Category = domain.Category,
            ScoreValue = domain.ScoreValue,
            Timestamp = domain.Timestamp,
            CreatedBy = domain.CreatedBy
        };
    }

    public static ScoreEntry ToDomain(ScoreEntryEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var scoreEntry = (ScoreEntry)Activator.CreateInstance(typeof(ScoreEntry), true)!;

        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.Id))!.SetValue(scoreEntry, entity.Id);
        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.SessionId))!.SetValue(scoreEntry, entity.SessionId);
        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.ParticipantId))!.SetValue(scoreEntry, entity.ParticipantId);
        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.RoundNumber))!.SetValue(scoreEntry, entity.RoundNumber);
        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.Category))!.SetValue(scoreEntry, entity.Category);
        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.ScoreValue))!.SetValue(scoreEntry, entity.ScoreValue);
        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.Timestamp))!.SetValue(scoreEntry, entity.Timestamp);
        typeof(ScoreEntry).GetProperty(nameof(ScoreEntry.CreatedBy))!.SetValue(scoreEntry, entity.CreatedBy);

        return scoreEntry;
    }
}
