using Api.SharedKernel.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to send a chat message in a session.
/// Issue #4760 - Shared Chat
/// </summary>
public record SendSessionChatMessageCommand(
    Guid SessionId,
    Guid SenderId,
    string Content,
    int? TurnNumber,
    string? MentionsJson
) : IRequest<SendChatMessageResult>
{
    /// <summary>
    /// IDOR guard (#3756): utente autenticato che effettua la chiamata. L'endpoint lo deriva dal
    /// principal e sovrascrive qualunque valore arrivato dal body; l'handler verifica via
    /// <c>ISessionAccessGuard</c> che sia owner o partecipante registrato della sessione.
    /// </summary>
    public Guid RequestedBy { get; init; }
}

public record SendChatMessageResult(Guid MessageId, int SequenceNumber);

/// <summary>
/// Command to send a system event message.
/// </summary>
public record SendSystemEventCommand(
    Guid SessionId,
    string Content,
    int? TurnNumber
) : IRequest<SendChatMessageResult>;

/// <summary>
/// Image attachment for the ask-agent command (multipart upload).
/// </summary>
public record ChatImageAttachment(byte[] Data, string MediaType, string? FileName);

/// <summary>
/// Command to ask the RAG agent a question in session context.
/// Supports optional image attachments for vision-based game state analysis.
/// </summary>
/// <param name="SessionId">The session the question is asked in.</param>
/// <param name="SenderId">The participant the message is written as. Chosen by the client, so it says nothing about who is asking.</param>
/// <param name="Question">The question. May be empty on a vision turn with an image.</param>
/// <param name="TurnNumber">The turn of the game the question belongs to, if any.</param>
/// <param name="Images">Optional board photos for vision-based game state analysis.</param>
/// <param name="RequestedBy">
/// The authenticated caller (#4776): set by the endpoint from the principal, overriding whatever the body carried.
/// The RAG ownership gate (ADR-096) and the copyright tier are judged on THIS user, never on the user behind
/// <paramref name="SenderId"/>: a participant id is readable by everyone at the table, and a guest has no account.
/// Absent (<see cref="Guid.Empty"/>) means nobody: the gate denies and the tier is the most restrictive.
/// </param>
public record AskSessionAgentCommand(
    Guid SessionId,
    Guid SenderId,
    string Question,
    int? TurnNumber,
    List<ChatImageAttachment>? Images = null,
    Guid RequestedBy = default
) : IRequest<AskSessionAgentResult>;

public record AskSessionAgentResult(
    Guid MessageId,
    string Answer,
    string AgentType,
    float? Confidence,
    string? CitationsJson,
    GroundingStatus GroundingStatus
);

/// <summary>
/// Command to delete a chat message.
/// </summary>
/// <param name="MessageId">Message to soft-delete.</param>
/// <param name="RequesterUserId">Authenticated caller — must own the participant that sent the message (#2655 IDOR guard). Never sourced from the client query string.</param>
public record DeleteChatMessageCommand(
    Guid MessageId,
    Guid RequesterUserId
) : IRequest<Unit>;
