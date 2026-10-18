using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Entity representing a message in a chat thread.
/// Changed from ValueObject to Entity to support update/delete operations (Issue #1184).
/// </summary>
internal sealed class ChatMessage
{
    public Guid Id { get; private set; }
    public string Content { get; private set; }
    public string Role { get; private set; } // "user" or "assistant"
    public DateTime Timestamp { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public bool IsInvalidated { get; private set; }
    public int SequenceNumber { get; private set; }

    // Issue #4362: Agent-specific metadata
    public string? AgentType { get; private set; }
    public float? Confidence { get; private set; }
    public string? CitationsJson { get; private set; }
    public int? TokenCount { get; private set; }

    // #4168 (ADR-095 D2): la versione del profilo che ha prodotto la risposta, e la sua lingua
    public Guid? ProfileVersionId { get; private set; }
    public string? Language { get; private set; }

    // #4397: il codice del testo che il turno dell'assistente ha scritto al posto del modello; null se il testo è del modello
    public string? FallbackCode { get; private set; }

    // #4456: il costo del turno dell'assistente; null se il messaggio non ne ha uno (domande, messaggi salvati prima)
    public decimal? CostUsd { get; private set; }

    // Known roles
    public static readonly string UserRole = "user";
    public static readonly string AssistantRole = "assistant";

    // Private constructor for EF Core
    private ChatMessage()
    {
        Content = string.Empty;
        Role = string.Empty;
    }

    public ChatMessage(string content, string role, int sequenceNumber, DateTime? timestamp = null, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Message content cannot be empty");

        var trimmed = content.Trim();
        if (trimmed.Length > 10000)
            throw new ValidationException("Message content cannot exceed 10,000 characters");

        if (!string.Equals(role, UserRole, StringComparison.Ordinal) && !string.Equals(role, AssistantRole, StringComparison.Ordinal))
            throw new ValidationException($"Role must be '{UserRole}' or '{AssistantRole}'");

        Id = id ?? Guid.NewGuid();
        Content = trimmed;
        Role = role;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp ?? DateTime.UtcNow;
        IsDeleted = false;
        IsInvalidated = false;
    }

    public bool IsUserMessage => string.Equals(Role, UserRole, StringComparison.Ordinal);
    public bool IsAssistantMessage => string.Equals(Role, AssistantRole, StringComparison.Ordinal);

    /// <summary>
    /// Updates the message content. Only user messages can be edited.
    /// </summary>
    public void UpdateContent(string newContent)
    {
        if (!IsUserMessage)
            throw new InvalidOperationException("Only user messages can be edited");

        if (IsDeleted)
            throw new InvalidOperationException("Cannot edit deleted message");

        if (string.IsNullOrWhiteSpace(newContent))
            throw new ValidationException("Message content cannot be empty");

        var trimmed = newContent.Trim();
        if (trimmed.Length > 10000)
            throw new ValidationException("Message content cannot exceed 10,000 characters");

        Content = trimmed;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-deletes the message.
    /// </summary>
    public void Delete(Guid deletedByUserId)
    {
        if (IsDeleted)
            return; // Idempotent

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedByUserId = deletedByUserId;
    }

    /// <summary>
    /// Marks the message as invalidated (for AI responses after user message edit/delete).
    /// </summary>
    public void Invalidate()
    {
        if (!IsAssistantMessage)
            throw new InvalidOperationException("Only assistant messages can be invalidated");

        IsInvalidated = true;
    }

    /// <summary>
    /// Sets agent-specific metadata on an assistant message (Issue #4362).
    /// Called after SSE stream completes to persist agent response metadata.
    /// </summary>
    public void SetAgentMetadata(string? agentType = null, float? confidence = null, string? citationsJson = null, int? tokenCount = null)
    {
        if (!IsAssistantMessage)
            throw new InvalidOperationException("Agent metadata can only be set on assistant messages");

        AgentType = agentType;
        Confidence = confidence;
        CitationsJson = citationsJson;
        TokenCount = tokenCount;
    }

    /// <summary>
    /// #4168 (ADR-095 D2): registra quale versione del profilo ha prodotto la risposta e in che lingua,
    /// così il feedback e le valutazioni possono riferirsi a quella versione.
    /// </summary>
    public void SetAnswerProvenance(Guid? profileVersionId, string? language)
    {
        if (!IsAssistantMessage)
            throw new InvalidOperationException("Answer provenance can only be set on assistant messages");

        ProfileVersionId = profileVersionId;
        Language = language;
    }

    /// <summary>
    /// #4397: segna una risposta dell'assistente che non viene dal modello, ma dal turno che l'ha scritta al
    /// suo posto (un guasto, una scadenza, una risposta vuota). Così non torna al modello nella storia.
    /// </summary>
    public void MarkAsFallback(string code)
    {
        if (!IsAssistantMessage)
            throw new InvalidOperationException("Only an assistant message can be a fallback");

        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("A fallback needs its code");

        FallbackCode = code;
    }

    /// <summary>
    /// #4456: il costo del turno dell'assistente che ha prodotto la risposta, sommato su tutte le sue chiamate al
    /// modello, con i token. Il costo di una partita è la somma sui messaggi del suo thread. È una stima: un turno
    /// finito in eccezione prima del salvataggio non lascia messaggio, e il suo costo resta solo in llm_cost_logs.
    /// Scrive anche <see cref="TokenCount"/>, quindi va chiamato dopo <see cref="SetAgentMetadata"/>.
    /// </summary>
    public void SetTurnCost(decimal costUsd, int tokenCount)
    {
        if (!IsAssistantMessage)
            throw new InvalidOperationException("Only an assistant message has a turn cost");

        if (costUsd < 0 || tokenCount < 0)
            throw new ValidationException("A turn cost cannot be negative");

        CostUsd = costUsd;
        TokenCount = tokenCount;
    }

    public override string ToString() => $"[{Role}] {Content.Substring(0, Math.Min(50, Content.Length))}...";
}
