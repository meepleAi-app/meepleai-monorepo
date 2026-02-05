using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Query to get all visible notes in a session for a participant.
/// Returns own notes and revealed notes from others.
/// </summary>
public record GetSessionNotesQuery(
    Guid SessionId,
    Guid RequesterId
) : IRequest<GetSessionNotesResponse>;

/// <summary>
/// Response for GetSessionNotesQuery.
/// </summary>
public record GetSessionNotesResponse(
    Guid SessionId,
    IReadOnlyList<SessionNoteDto> Notes);

/// <summary>
/// DTO for session note.
/// </summary>
public record SessionNoteDto(
    Guid Id,
    Guid SessionId,
    Guid ParticipantId,
    string? Content,  // Null if not owner and not revealed
    bool IsOwner,
    bool IsRevealed,
    string? ObscuredText,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Query to get a specific note.
/// </summary>
public record GetNoteByIdQuery(
    Guid NoteId,
    Guid RequesterId
) : IRequest<SessionNoteDto?>;

/// <summary>
/// Query to get notes by participant in a session.
/// </summary>
public record GetParticipantNotesQuery(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId
) : IRequest<GetParticipantNotesResponse>;

/// <summary>
/// Response for GetParticipantNotesQuery.
/// </summary>
public record GetParticipantNotesResponse(
    Guid SessionId,
    Guid ParticipantId,
    IReadOnlyList<SessionNoteDto> Notes);
