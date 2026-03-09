using Api.BoundedContexts.Administration.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Administration.Domain;

/// <summary>
/// Unit tests for UserAiConsent entity domain logic (Issue #5512)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class UserAiConsentTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();
    private const string TestVersion = "1.0.0";

    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldCreateConsent()
    {
        // Act
        var consent = UserAiConsent.Create(TestUserId, true, true, TestVersion);

        // Assert
        consent.Should().NotBeNull();
        consent.Id.Should().NotBeEmpty();
        consent.UserId.Should().Be(TestUserId);
        consent.ConsentedToAiProcessing.Should().BeTrue();
        consent.ConsentedToExternalProviders.Should().BeTrue();
        consent.ConsentVersion.Should().Be(TestVersion);
        consent.ConsentedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        consent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        consent.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        // Act
        var act = () => UserAiConsent.Create(Guid.Empty, true, true, TestVersion);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidVersion_ShouldThrow(string? version)
    {
        // Act
        var act = () => UserAiConsent.Create(TestUserId, true, true, version!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("consentVersion");
    }

    [Fact]
    public void Create_WithNoConsent_ShouldCreateWithFalseFlags()
    {
        // Act
        var consent = UserAiConsent.Create(TestUserId, false, false, TestVersion);

        // Assert
        consent.ConsentedToAiProcessing.Should().BeFalse();
        consent.ConsentedToExternalProviders.Should().BeFalse();
    }

    #endregion

    #region UpdateConsent Tests

    [Fact]
    public void UpdateConsent_ShouldUpdateFlagsAndTimestamp()
    {
        // Arrange
        var consent = UserAiConsent.Create(TestUserId, false, false, "0.9.0");
        var originalCreatedAt = consent.CreatedAt;

        // Act
        consent.UpdateConsent(true, true, "1.0.0");

        // Assert
        consent.ConsentedToAiProcessing.Should().BeTrue();
        consent.ConsentedToExternalProviders.Should().BeTrue();
        consent.ConsentVersion.Should().Be("1.0.0");
        consent.ConsentedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        consent.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        consent.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateConsent_WithInvalidVersion_ShouldThrow(string? version)
    {
        // Arrange
        var consent = UserAiConsent.Create(TestUserId, true, true, TestVersion);

        // Act
        var act = () => consent.UpdateConsent(true, true, version!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("consentVersion");
    }

    #endregion

    #region WithdrawConsent Tests

    [Fact]
    public void WithdrawConsent_ShouldSetBothFlagsToFalse()
    {
        // Arrange
        var consent = UserAiConsent.Create(TestUserId, true, true, TestVersion);

        // Act
        consent.WithdrawConsent();

        // Assert
        consent.ConsentedToAiProcessing.Should().BeFalse();
        consent.ConsentedToExternalProviders.Should().BeFalse();
        consent.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    #endregion
}
