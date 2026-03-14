using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

/// <summary>
/// Domain tests for User onboarding and profile methods.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class UserOnboardingTests
{
    private static User CreateTestUser()
    {
        return new User(
            Guid.NewGuid(),
            new Email("test@example.com"),
            "Test User",
            PasswordHash.Create("TestPassword123!"),
            Role.User
        );
    }

    [Fact]
    public void MarkOnboardingWizardSeen_SetsTimestamp()
    {
        var user = CreateTestUser();
        Assert.Null(user.OnboardingWizardSeenAt);

        user.MarkOnboardingWizardSeen();

        Assert.NotNull(user.OnboardingWizardSeenAt);
    }

    [Fact]
    public void MarkOnboardingWizardSeen_IsIdempotent()
    {
        var user = CreateTestUser();
        user.MarkOnboardingWizardSeen();
        var firstTimestamp = user.OnboardingWizardSeenAt;

        user.MarkOnboardingWizardSeen();

        Assert.Equal(firstTimestamp, user.OnboardingWizardSeenAt);
    }

    [Fact]
    public void DismissOnboarding_SetsTimestamp()
    {
        var user = CreateTestUser();
        Assert.Null(user.OnboardingDismissedAt);

        user.DismissOnboarding();

        Assert.NotNull(user.OnboardingDismissedAt);
    }

    [Fact]
    public void DismissOnboarding_IsIdempotent()
    {
        var user = CreateTestUser();
        user.DismissOnboarding();
        var firstTimestamp = user.OnboardingDismissedAt;

        user.DismissOnboarding();

        Assert.Equal(firstTimestamp, user.OnboardingDismissedAt);
    }

    [Fact]
    public void UpdateAvatarUrl_SetsValue()
    {
        var user = CreateTestUser();
        Assert.Null(user.AvatarUrl);

        user.UpdateAvatarUrl("https://example.com/avatar.png");

        Assert.Equal("https://example.com/avatar.png", user.AvatarUrl);
    }

    [Fact]
    public void UpdateBio_SetsValue()
    {
        var user = CreateTestUser();
        Assert.Null(user.Bio);

        user.UpdateBio("Board game enthusiast");

        Assert.Equal("Board game enthusiast", user.Bio);
    }

    [Fact]
    public void WizardSeen_And_Dismiss_AreIndependent()
    {
        var user = CreateTestUser();

        user.MarkOnboardingWizardSeen();
        Assert.NotNull(user.OnboardingWizardSeenAt);
        Assert.Null(user.OnboardingDismissedAt);

        user.DismissOnboarding();
        Assert.NotNull(user.OnboardingWizardSeenAt);
        Assert.NotNull(user.OnboardingDismissedAt);
    }
}
