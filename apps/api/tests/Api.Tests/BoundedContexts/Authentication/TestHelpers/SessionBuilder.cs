using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Fluent builder for creating test Session entities.
/// Provides convenient methods for constructing sessions with various configurations.
/// </summary>
public class SessionBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _userId = Guid.NewGuid();
    private SessionToken _token = SessionToken.Generate();
    private TimeSpan? _lifetime;
    private string? _ipAddress;
    private string? _userAgent;
    private bool _isRevoked;

    /// <summary>
    /// Sets the session ID.
    /// </summary>
    public SessionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the user ID for this session.
    /// </summary>
    public SessionBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the user ID from a User entity.
    /// </summary>
    public SessionBuilder ForUser(User user)
    {
        _userId = user.Id;
        return this;
    }

    /// <summary>
    /// Sets the session token.
    /// </summary>
    public SessionBuilder WithToken(SessionToken token)
    {
        _token = token;
        return this;
    }

    /// <summary>
    /// Sets the session lifetime (duration before expiration).
    /// </summary>
    public SessionBuilder WithLifetime(TimeSpan lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Sets the session to expire after a specified number of days.
    /// </summary>
    public SessionBuilder ExpiresInDays(int days)
    {
        _lifetime = TimeSpan.FromDays(days);
        return this;
    }

    /// <summary>
    /// Sets the session to be expired (negative lifetime).
    /// </summary>
    public SessionBuilder Expired()
    {
        _lifetime = TimeSpan.FromDays(-1);
        return this;
    }

    /// <summary>
    /// Sets the IP address for this session.
    /// </summary>
    public SessionBuilder WithIpAddress(string ipAddress)
    {
        _ipAddress = ipAddress;
        return this;
    }

    /// <summary>
    /// Sets the user agent for this session.
    /// </summary>
    public SessionBuilder WithUserAgent(string userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    /// <summary>
    /// Marks the session as revoked.
    /// </summary>
    public SessionBuilder Revoked()
    {
        _isRevoked = true;
        return this;
    }

    /// <summary>
    /// Builds the Session entity.
    /// </summary>
    public Session Build()
    {
        var session = new Session(_id, _userId, _token, _lifetime, _ipAddress, _userAgent);

        if (_isRevoked)
        {
            session.Revoke();
        }

        return session;
    }

    /// <summary>
    /// Creates a default test session with standard configuration.
    /// </summary>
    public static Session CreateDefault() => new SessionBuilder().Build();

    /// <summary>
    /// Creates a session for a specific user.
    /// </summary>
    public static Session CreateForUser(User user) => new SessionBuilder().ForUser(user).Build();

    /// <summary>
    /// Creates an expired session for testing expiration scenarios.
    /// </summary>
    public static Session CreateExpired() => new SessionBuilder().Expired().Build();
}

