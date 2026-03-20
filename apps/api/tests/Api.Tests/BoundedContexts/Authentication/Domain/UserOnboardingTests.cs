using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        user.OnboardingWizardSeenAt.Should().BeNull();

        user.MarkOnboardingWizardSeen();

        user.OnboardingWizardSeenAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkOnboardingWizardSeen_IsIdempotent()
    {
        var user = CreateTestUser();
        user.MarkOnboardingWizardSeen();
        var firstTimestamp = user.OnboardingWizardSeenAt;

        user.MarkOnboardingWizardSeen();

        user.OnboardingWizardSeenAt.Should().Be(firstTimestamp);
    }

    [Fact]
    public void DismissOnboarding_SetsTimestamp()
    {
        var user = CreateTestUser();
        user.OnboardingDismissedAt.Should().BeNull();

        user.DismissOnboarding();

        user.OnboardingDismissedAt.Should().NotBeNull();
    }

    [Fact]
    public void DismissOnboarding_IsIdempotent()
    {
        var user = CreateTestUser();
        user.DismissOnboarding();
        var firstTimestamp = user.OnboardingDismissedAt;

        user.DismissOnboarding();

        user.OnboardingDismissedAt.Should().Be(firstTimestamp);
    }

    [Fact]
    public void UpdateAvatarUrl_SetsValue()
    {
        var user = CreateTestUser();
        user.AvatarUrl.Should().BeNull();

        user.UpdateAvatarUrl("https://example.com/avatar.png");

        user.AvatarUrl.Should().Be("https://example.com/avatar.png");
    }

    [Fact]
    public void UpdateBio_SetsValue()
    {
        var user = CreateTestUser();
        user.Bio.Should().BeNull();

        user.UpdateBio("Board game enthusiast");

        user.Bio.Should().Be("Board game enthusiast");
    }

    [Fact]
    public void WizardSeen_And_Dismiss_AreIndependent()
    {
        var user = CreateTestUser();

        user.MarkOnboardingWizardSeen();
        user.OnboardingWizardSeenAt.Should().NotBeNull();
        user.OnboardingDismissedAt.Should().BeNull();

        user.DismissOnboarding();
        user.OnboardingWizardSeenAt.Should().NotBeNull();
        user.OnboardingDismissedAt.Should().NotBeNull();
    }
}
