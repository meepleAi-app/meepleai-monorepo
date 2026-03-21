using System.Reflection;
using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.Tests.BoundedContexts.Administration.TestHelpers;

/// <summary>
/// Fluent builder for creating test UserProfile read-only projection instances.
/// Uses reflection to set private properties since UserProfile is a read-only EF projection.
/// </summary>
internal class UserProfileBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = "test@example.com";
    private string? _displayName = "Test User";
    private string? _avatarUrl;
    private string? _bio;
    private string _role = "user";
    private string _tier = "free";
    private string _status = "Active";
    private DateTime _createdAt = DateTime.UtcNow;
    private int _level = 1;
    private int _experiencePoints;
    private bool _emailVerified;
    private bool _isTwoFactorEnabled;
    private bool _isSuspended;
    private bool _isContributor;
    private bool _isDemoAccount;

    public UserProfileBuilder WithId(Guid id) { _id = id; return this; }
    public UserProfileBuilder WithEmail(string email) { _email = email; return this; }
    public UserProfileBuilder WithDisplayName(string? displayName) { _displayName = displayName; return this; }
    public UserProfileBuilder WithRole(string role) { _role = role; return this; }
    public UserProfileBuilder WithTier(string tier) { _tier = tier; return this; }
    public UserProfileBuilder WithStatus(string status) { _status = status; return this; }
    public UserProfileBuilder WithCreatedAt(DateTime createdAt) { _createdAt = createdAt; return this; }
    public UserProfileBuilder WithLevel(int level) { _level = level; return this; }
    public UserProfileBuilder WithExperiencePoints(int xp) { _experiencePoints = xp; return this; }
    public UserProfileBuilder WithEmailVerified(bool verified = true) { _emailVerified = verified; return this; }
    public UserProfileBuilder WithTwoFactor(bool enabled = true) { _isTwoFactorEnabled = enabled; return this; }
    public UserProfileBuilder WithSuspended(bool suspended = true) { _isSuspended = suspended; return this; }
    public UserProfileBuilder WithContributor(bool contributor = true) { _isContributor = contributor; return this; }
    public UserProfileBuilder WithDemoAccount(bool demo = true) { _isDemoAccount = demo; return this; }

    public UserProfileBuilder AsAdmin() { _role = "admin"; return this; }
    public UserProfileBuilder AsEditor() { _role = "editor"; return this; }

    public UserProfile Build()
    {
        var profile = (UserProfile)Activator.CreateInstance(typeof(UserProfile), nonPublic: true)!;
        SetPrivate(profile, nameof(UserProfile.Id), _id);
        SetPrivate(profile, nameof(UserProfile.Email), _email);
        SetPrivate(profile, nameof(UserProfile.DisplayName), _displayName);
        SetPrivate(profile, nameof(UserProfile.AvatarUrl), _avatarUrl);
        SetPrivate(profile, nameof(UserProfile.Bio), _bio);
        SetPrivate(profile, nameof(UserProfile.Role), _role);
        SetPrivate(profile, nameof(UserProfile.Tier), _tier);
        SetPrivate(profile, nameof(UserProfile.Status), _status);
        SetPrivate(profile, nameof(UserProfile.CreatedAt), _createdAt);
        SetPrivate(profile, nameof(UserProfile.Level), _level);
        SetPrivate(profile, nameof(UserProfile.ExperiencePoints), _experiencePoints);
        SetPrivate(profile, nameof(UserProfile.EmailVerified), _emailVerified);
        SetPrivate(profile, nameof(UserProfile.IsTwoFactorEnabled), _isTwoFactorEnabled);
        SetPrivate(profile, nameof(UserProfile.IsSuspended), _isSuspended);
        SetPrivate(profile, nameof(UserProfile.IsContributor), _isContributor);
        SetPrivate(profile, nameof(UserProfile.IsDemoAccount), _isDemoAccount);
        return profile;
    }

    private static void SetPrivate(UserProfile profile, string propertyName, object? value)
    {
        var prop = typeof(UserProfile).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        prop?.SetValue(profile, value);
    }
}
