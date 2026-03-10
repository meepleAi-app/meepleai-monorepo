using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

/// <summary>
/// Aggregate root representing an email template with versioning.
/// Supports multiple locales, placeholder substitution, and version management.
/// Only one version per (name, locale) pair can be active at a time.
/// Issue #52: P4.1 Domain entity for admin email template management.
/// </summary>
internal sealed class EmailTemplate : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Locale { get; private set; }
    public string Subject { get; private set; }
    public string HtmlBody { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? LastModifiedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private EmailTemplate() : base() { }
#pragma warning restore CS8618

    private EmailTemplate(
        Guid id,
        string name,
        string locale,
        string subject,
        string htmlBody,
        int version,
        bool isActive,
        Guid? lastModifiedBy)
        : base(id)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name.Trim().ToLowerInvariant() : throw new ArgumentException("Name cannot be empty", nameof(name));
        Locale = !string.IsNullOrWhiteSpace(locale) ? locale.Trim().ToLowerInvariant() : throw new ArgumentException("Locale cannot be empty", nameof(locale));
        Subject = !string.IsNullOrWhiteSpace(subject) ? subject : throw new ArgumentException("Subject cannot be empty", nameof(subject));
        HtmlBody = !string.IsNullOrWhiteSpace(htmlBody) ? htmlBody : throw new ArgumentException("HtmlBody cannot be empty", nameof(htmlBody));
        Version = version;
        IsActive = isActive;
        LastModifiedBy = lastModifiedBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = null;
    }

    /// <summary>
    /// Factory method to create a new email template (version 1, active by default).
    /// </summary>
    public static EmailTemplate Create(string name, string locale, string subject, string htmlBody, Guid createdBy)
    {
        return new EmailTemplate(
            Guid.NewGuid(),
            name,
            locale,
            subject,
            htmlBody,
            version: 1,
            isActive: true,
            lastModifiedBy: createdBy);
    }

    /// <summary>
    /// Creates a new version of this template with updated content.
    /// The new version starts as inactive until explicitly published.
    /// </summary>
    public EmailTemplate CreateNewVersion(string subject, string htmlBody, Guid modifiedBy)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject cannot be empty", nameof(subject));
        if (string.IsNullOrWhiteSpace(htmlBody))
            throw new ArgumentException("HtmlBody cannot be empty", nameof(htmlBody));

        return new EmailTemplate(
            Guid.NewGuid(),
            Name,
            Locale,
            subject,
            htmlBody,
            version: Version + 1,
            isActive: false,
            lastModifiedBy: modifiedBy);
    }

    /// <summary>
    /// Activates this template version.
    /// Caller must ensure all other versions of the same (name, locale) are deactivated first.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates this template version.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the content of this template in-place.
    /// </summary>
    public void UpdateContent(string subject, string htmlBody, Guid modifiedBy)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject cannot be empty", nameof(subject));
        if (string.IsNullOrWhiteSpace(htmlBody))
            throw new ArgumentException("HtmlBody cannot be empty", nameof(htmlBody));

        Subject = subject;
        HtmlBody = htmlBody;
        LastModifiedBy = modifiedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reconstitutes the aggregate from persistence.
    /// Used by repository when mapping from database entity.
    /// </summary>
    internal static EmailTemplate Reconstitute(
        Guid id,
        string name,
        string locale,
        string subject,
        string htmlBody,
        int version,
        bool isActive,
        Guid? lastModifiedBy,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        var template = new EmailTemplate(id, name, locale, subject, htmlBody, version, isActive, lastModifiedBy);
        template.CreatedAt = createdAt;
        template.UpdatedAt = updatedAt;
        return template;
    }
}
