using System.Reflection;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Fluent builder for creating test ShareLink entities.
/// Provides convenient methods for constructing share links with various configurations.
/// </summary>
internal class ShareLinkBuilder
{
    private Guid _threadId = Guid.NewGuid();
    private Guid _creatorId = Guid.NewGuid();
    private ShareLinkRole _role = ShareLinkRole.View;
    private DateTime _expiresAt = DateTime.UtcNow.AddDays(7);
    private string? _label;
    private bool _revoked;
    private int _accessCount;

    /// <summary>
    /// Sets the thread ID.
    /// </summary>
    public ShareLinkBuilder WithThreadId(Guid threadId)
    {
        _threadId = threadId;
        return this;
    }

    /// <summary>
    /// Sets the creator ID.
    /// </summary>
    public ShareLinkBuilder WithCreatorId(Guid creatorId)
    {
        _creatorId = creatorId;
        return this;
    }

    /// <summary>
    /// Sets the share link role (View or Comment).
    /// </summary>
    public ShareLinkBuilder WithRole(ShareLinkRole role)
    {
        _role = role;
        return this;
    }

    /// <summary>
    /// Sets the role to View-only.
    /// </summary>
    public ShareLinkBuilder AsViewOnly()
    {
        _role = ShareLinkRole.View;
        return this;
    }

    /// <summary>
    /// Sets the role to Comment (can add messages).
    /// </summary>
    public ShareLinkBuilder AsComment()
    {
        _role = ShareLinkRole.Comment;
        return this;
    }

    /// <summary>
    /// Sets the expiration timestamp.
    /// </summary>
    public ShareLinkBuilder WithExpiresAt(DateTime expiresAt)
    {
        _expiresAt = expiresAt;
        return this;
    }

    /// <summary>
    /// Sets expiration to a specific duration from now.
    /// </summary>
    public ShareLinkBuilder ExpiresIn(TimeSpan duration)
    {
        _expiresAt = DateTime.UtcNow.Add(duration);
        return this;
    }

    /// <summary>
    /// Sets the share link to already be expired.
    /// </summary>
    public ShareLinkBuilder AsExpired()
    {
        _expiresAt = DateTime.UtcNow.AddSeconds(-1);
        return this;
    }

    /// <summary>
    /// Sets the label for the share link.
    /// </summary>
    public ShareLinkBuilder WithLabel(string label)
    {
        _label = label;
        return this;
    }

    /// <summary>
    /// Marks the share link as revoked after creation.
    /// </summary>
    public ShareLinkBuilder AsRevoked()
    {
        _revoked = true;
        return this;
    }

    /// <summary>
    /// Sets the access count (for testing analytics).
    /// </summary>
    public ShareLinkBuilder WithAccessCount(int count)
    {
        _accessCount = count;
        return this;
    }

    /// <summary>
    /// Builds the ShareLink entity.
    /// </summary>
    public ShareLink Build()
    {
        // Use a future expiration for creation, then update if needed
        var effectiveExpiresAt = _expiresAt <= DateTime.UtcNow
            ? DateTime.UtcNow.AddDays(1) // Temporary valid expiration for creation
            : _expiresAt;

        var shareLink = ShareLink.Create(
            threadId: _threadId,
            creatorId: _creatorId,
            role: _role,
            expiresAt: effectiveExpiresAt,
            label: _label
        );

        // Record access count
        for (var i = 0; i < _accessCount; i++)
        {
            shareLink.RecordAccess();
        }

        // Revoke if needed
        if (_revoked)
        {
            shareLink.Revoke();
        }

        return shareLink;
    }

    /// <summary>
    /// Creates a default view-only share link.
    /// </summary>
    public static ShareLink CreateDefault() => new ShareLinkBuilder().Build();

    /// <summary>
    /// Creates a comment-enabled share link.
    /// </summary>
    public static ShareLink CreateWithComment() => new ShareLinkBuilder().AsComment().Build();

    /// <summary>
    /// Creates a revoked share link.
    /// </summary>
    public static ShareLink CreateRevoked() => new ShareLinkBuilder().AsRevoked().Build();

    /// <summary>
    /// Creates a share link with specific thread and creator.
    /// </summary>
    public static ShareLink CreateFor(Guid threadId, Guid creatorId) =>
        new ShareLinkBuilder()
            .WithThreadId(threadId)
            .WithCreatorId(creatorId)
            .Build();

    /// <summary>
    /// Builds a ShareLink entity with ExpiresAt set to a past date using reflection.
    /// This creates a truly expired entity for testing expiration behavior deterministically.
    /// </summary>
    /// <param name="expiredAt">The past expiration date (defaults to 1 hour ago)</param>
    public ShareLink BuildExpired(DateTime? expiredAt = null)
    {
        var pastExpiration = expiredAt ?? DateTime.UtcNow.AddHours(-1);

        // Create with valid future expiration first
        var shareLink = ShareLink.Create(
            threadId: _threadId,
            creatorId: _creatorId,
            role: _role,
            expiresAt: DateTime.UtcNow.AddDays(7),
            label: _label
        );

        // Use reflection to set ExpiresAt to past date (bypassing domain validation for testing)
        var expiresAtProperty = typeof(ShareLink).GetProperty("ExpiresAt",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (expiresAtProperty == null)
            throw new InvalidOperationException("Could not find ExpiresAt property on ShareLink");

        // Get the backing field setter
        var backingField = typeof(ShareLink).GetField("<ExpiresAt>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (backingField != null)
        {
            backingField.SetValue(shareLink, pastExpiration);
        }
        else
        {
            // Try setting via property setter if available
            expiresAtProperty.SetValue(shareLink, pastExpiration);
        }

        // Record access count
        for (var i = 0; i < _accessCount; i++)
        {
            shareLink.RecordAccess();
        }

        // Revoke if needed
        if (_revoked)
        {
            shareLink.Revoke();
        }

        return shareLink;
    }

    /// <summary>
    /// Creates an expired share link for testing expiration behavior.
    /// Uses reflection to set ExpiresAt to a past date deterministically.
    /// </summary>
    public static ShareLink CreateExpired() => new ShareLinkBuilder().BuildExpired();

    /// <summary>
    /// Creates an expired share link with specific thread and creator.
    /// </summary>
    public static ShareLink CreateExpiredFor(Guid threadId, Guid creatorId) =>
        new ShareLinkBuilder()
            .WithThreadId(threadId)
            .WithCreatorId(creatorId)
            .BuildExpired();
}
