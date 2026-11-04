using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Feedback utente su una risposta chat KB.
/// KB-06: User Feedback on Chat Response.
/// </summary>
public sealed class KbUserFeedback : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid ChatSessionId { get; private set; }
    public Guid MessageId { get; private set; }
    /// <summary>"helpful" | "not_helpful"</summary>
    public string Outcome { get; private set; } = string.Empty;
    /// <summary>Commento opzionale (max 500 char).</summary>
    public string? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }
    /// <summary>
    /// #4168 (ADR-095 D2): la versione del profilo che ha prodotto la risposta valutata, e la sua
    /// lingua. Null per i feedback anteriori a #4168 e per le risposte salvate senza provenienza.
    /// </summary>
    public Guid? ProfileVersionId { get; private set; }
    /// <inheritdoc cref="ProfileVersionId"/>
    public string? Language { get; private set; }

    private KbUserFeedback() : base() { }

    public static KbUserFeedback Create(
        Guid userId, Guid gameId, Guid chatSessionId, Guid messageId,
        string outcome, string? comment, Guid? profileVersionId = null, string? language = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        if (outcome is not "helpful" and not "not_helpful")
            throw new ArgumentException("Outcome must be 'helpful' or 'not_helpful'", nameof(outcome));

        if (comment?.Length > 500)
            throw new ArgumentException("Comment must not exceed 500 characters.", nameof(comment));

        return new KbUserFeedback
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            ChatSessionId = chatSessionId,
            MessageId = messageId,
            Outcome = outcome,
            Comment = comment,
            ProfileVersionId = profileVersionId,
            Language = language,
            CreatedAt = DateTime.UtcNow
        };
    }
}
