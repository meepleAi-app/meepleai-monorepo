using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Fluent builder for creating test User entities.
/// Provides convenient methods for constructing users with various configurations.
/// </summary>
internal class UserBuilder
{
    private Guid _id = Guid.NewGuid();
    private Email _email = new("test@example.com");
    private string _displayName = "Test User";
    private PasswordHash _passwordHash = PasswordHash.Create("DefaultPassword123!");
    private Role _role = Role.User;
    private UserTier? _tier;
    private bool _enable2FA;
    private string? _totpSecret;

    /// <summary>
    /// Sets the user ID.
    /// </summary>
    public UserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the user email.
    /// </summary>
    public UserBuilder WithEmail(string email)
    {
        _email = new Email(email);
        return this;
    }

    /// <summary>
    /// Sets the user email using an Email value object.
    /// </summary>
    public UserBuilder WithEmail(Email email)
    {
        _email = email;
        return this;
    }

    /// <summary>
    /// Sets the display name.
    /// </summary>
    public UserBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>
    /// Sets the password (creates hash automatically).
    /// </summary>
    public UserBuilder WithPassword(string plaintextPassword)
    {
        _passwordHash = PasswordHash.Create(plaintextPassword);
        return this;
    }

    /// <summary>
    /// Sets the password hash directly.
    /// </summary>
    public UserBuilder WithPasswordHash(PasswordHash passwordHash)
    {
        _passwordHash = passwordHash;
        return this;
    }

    /// <summary>
    /// Sets the user role.
    /// </summary>
    public UserBuilder WithRole(Role role)
    {
        _role = role;
        return this;
    }

    /// <summary>
    /// Sets the user as admin.
    /// </summary>
    public UserBuilder AsAdmin()
    {
        _role = Role.Admin;
        return this;
    }

    /// <summary>
    /// Sets the user as editor.
    /// </summary>
    public UserBuilder AsEditor()
    {
        _role = Role.Editor;
        return this;
    }

    /// <summary>
    /// Sets the user tier.
    /// </summary>
    public UserBuilder WithTier(UserTier tier)
    {
        _tier = tier;
        return this;
    }

    /// <summary>
    /// Enables two-factor authentication.
    /// </summary>
    public UserBuilder With2FA(string encryptedSecret = "encrypted_test_secret")
    {
        _enable2FA = true;
        _totpSecret = encryptedSecret;
        return this;
    }

    /// <summary>
    /// Creates a user with OAuth integration (links OAuth account after creation).
    /// </summary>
    public UserBuilder WithOAuth(string provider = "google", string providerUserId = "oauth_user_123")
    {
        // OAuth linking must be done after user creation
        // This method is a marker for tests that need OAuth setup
        return this;
    }

    /// <summary>
    /// Builds the User entity.
    /// </summary>
    public User Build()
    {
        var user = new User(_id, _email, _displayName, _passwordHash, _role, _tier);

        if (_enable2FA && _totpSecret != null)
        {
            user.Enable2FA(TotpSecret.FromEncrypted(_totpSecret));
        }

        return user;
    }

    /// <summary>
    /// Creates a default test user with standard configuration.
    /// </summary>
    public static User CreateDefault() => new UserBuilder().Build();

    /// <summary>
    /// Creates an admin user with default configuration.
    /// </summary>
    public static User CreateAdmin() => new UserBuilder().AsAdmin().Build();

    /// <summary>
    /// Creates an editor user with default configuration.
    /// </summary>
    public static User CreateEditor() => new UserBuilder().AsEditor().Build();
}
