using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Entity representing a versioned prompt template associated with an agent typology.
/// Supports template evolution with version tracking and current/historical distinction.
/// Issue #3175
/// </summary>
internal sealed class TypologyPromptTemplate : Entity<Guid>
{
    private Guid _id;
    private Guid _typologyId;
    private string _content = string.Empty;
    private readonly int _version;
    private bool _isCurrent;
    private Guid _createdBy;
    private readonly DateTime _createdAt;

    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the typology this template belongs to.
    /// </summary>
    public Guid TypologyId => _typologyId;

    /// <summary>
    /// Gets the prompt template content.
    /// </summary>
    public string Content => _content;

    /// <summary>
    /// Gets the template version number (starts at 1, increments on updates).
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// Gets whether this is the current active template for the typology.
    /// </summary>
    public bool IsCurrent => _isCurrent;

    /// <summary>
    /// Gets the user who created this template version.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private TypologyPromptTemplate() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal TypologyPromptTemplate(
        Guid id,
        Guid typologyId,
        string content,
        int version,
        bool isCurrent,
        Guid createdBy,
        DateTime createdAt) : base(id)
    {
        _id = id;
        _typologyId = typologyId;
        _content = content;
        _version = version;
        _isCurrent = isCurrent;
        _createdBy = createdBy;
        _createdAt = createdAt;
    }

    /// <summary>
    /// Creates a new typology prompt template with validation.
    /// </summary>
    public static TypologyPromptTemplate Create(
        Guid typologyId,
        string content,
        int version,
        Guid createdBy,
        bool isCurrent = false)
    {
        if (typologyId == Guid.Empty)
            throw new ArgumentException("TypologyId cannot be empty", nameof(typologyId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (content.Length > 10000)
            throw new ArgumentException("Content cannot exceed 10000 characters", nameof(content));

        if (version < 1)
            throw new ArgumentException("Version must be at least 1", nameof(version));

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        return new TypologyPromptTemplate(
            Guid.NewGuid(),
            typologyId,
            content.Trim(),
            version,
            isCurrent,
            createdBy,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Sets this template as the current active template.
    /// </summary>
    public void SetAsCurrent()
    {
        _isCurrent = true;
    }

    /// <summary>
    /// Deactivates this template (e.g., when a new version becomes current).
    /// </summary>
    public void Deactivate()
    {
        _isCurrent = false;
    }

    /// <summary>
    /// Updates the template content (creates new version in practice).
    /// </summary>
    public void UpdateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (content.Length > 10000)
            throw new ArgumentException("Content cannot exceed 10000 characters", nameof(content));

        _content = content.Trim();
    }
}
