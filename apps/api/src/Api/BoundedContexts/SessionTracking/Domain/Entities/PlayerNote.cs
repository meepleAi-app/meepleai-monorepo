using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Player note entity representing a note for a participant in a session.
/// </summary>
public class PlayerNote
{
    /// <summary>
    /// Note unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Session reference.
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Participant reference.
    /// </summary>
    public Guid ParticipantId { get; private set; }

    /// <summary>
    /// Note type: Private, Shared, or Template.
    /// </summary>
    public NoteType NoteType { get; private set; }

    /// <summary>
    /// Template key for template-based notes.
    /// </summary>
    [MaxLength(50)]
    public string? TemplateKey { get; private set; }

    /// <summary>
    /// Note content (supports rich text/markdown).
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Flag to hide note visually (e.g., hidden cards, secret objectives).
    /// </summary>
    public bool IsHidden { get; private set; }

    /// <summary>
    /// When the note was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// When the note was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private PlayerNote()
    {
    }

    /// <summary>
    /// Factory method to create a player note.
    /// </summary>
    /// <param name="sessionId">Session reference.</param>
    /// <param name="participantId">Participant reference.</param>
    /// <param name="noteType">Note type.</param>
    /// <param name="content">Note content.</param>
    /// <param name="templateKey">Optional template key for template notes.</param>
    /// <param name="isHidden">Whether note is hidden.</param>
    /// <returns>New player note instance.</returns>
    public static PlayerNote Create(
        Guid sessionId,
        Guid participantId,
        NoteType noteType,
        string content,
        string? templateKey = null,
        bool isHidden = false)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID cannot be empty.", nameof(participantId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        if (content.Length > 10000)
            throw new ArgumentException("Content cannot exceed 10000 characters.", nameof(content));

        if (noteType == NoteType.Template && string.IsNullOrWhiteSpace(templateKey))
            throw new ArgumentException("Template key required for template notes.", nameof(templateKey));

        if (!string.IsNullOrWhiteSpace(templateKey) && templateKey.Length > 50)
            throw new ArgumentException("Template key cannot exceed 50 characters.", nameof(templateKey));

        return new PlayerNote
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParticipantId = participantId,
            NoteType = noteType,
            Content = content.Trim(),
            TemplateKey = !string.IsNullOrWhiteSpace(templateKey) ? templateKey.Trim() : null,
            IsHidden = isHidden,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates the note content.
    /// </summary>
    /// <param name="newContent">New content.</param>
    public void UpdateContent(string newContent)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("Content cannot be empty.", nameof(newContent));

        if (newContent.Length > 10000)
            throw new ArgumentException("Content cannot exceed 10000 characters.", nameof(newContent));

        Content = newContent.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Toggles the hidden state of the note.
    /// </summary>
    public void ToggleHidden()
    {
        IsHidden = !IsHidden;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Shows the note (sets IsHidden to false).
    /// </summary>
    public void Show()
    {
        IsHidden = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Hides the note (sets IsHidden to true).
    /// </summary>
    public void Hide()
    {
        IsHidden = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Note type enumeration.
/// </summary>
public enum NoteType
{
    /// <summary>
    /// Private note (visible only to participant).
    /// </summary>
    Private = 0,

    /// <summary>
    /// Shared note (visible to all session participants).
    /// </summary>
    Shared = 1,

    /// <summary>
    /// Template note (pre-defined template content).
    /// </summary>
    Template = 2
}