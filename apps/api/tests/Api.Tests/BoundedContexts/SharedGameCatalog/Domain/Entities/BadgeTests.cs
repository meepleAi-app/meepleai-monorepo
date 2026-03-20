using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Unit tests for Badge entity.
/// </summary>
public class BadgeTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsBadge()
    {
        // Arrange
        var code = "FIRST_CONTRIBUTION";
        var name = "First Steps";
        var description = "Made your first contribution";
        var tier = BadgeTier.Bronze;
        var category = BadgeCategory.Contribution;
        var requirement = BadgeRequirement.ForFirstContribution();

        // Act
        var badge = Badge.Create(code, name, description, tier, category, requirement);

        // Assert
        badge.Should().NotBeNull();
        badge.Id.Should().NotBe(Guid.Empty);
        badge.Code.Should().Be("FIRST_CONTRIBUTION");
        badge.Name.Should().Be(name);
        badge.Description.Should().Be(description);
        badge.Tier.Should().Be(tier);
        badge.Category.Should().Be(category);
        badge.Requirement.Should().Be(requirement);
        badge.IsActive.Should().BeTrue();
        badge.DisplayOrder.Should().Be(0);
        badge.IconUrl.Should().BeNull();
        badge.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        badge.ModifiedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllParameters_ReturnsBadge()
    {
        // Arrange
        var code = "top_contributor";
        var name = "Top Contributor";
        var description = "Ranked in top 10";
        var tier = BadgeTier.Gold;
        var category = BadgeCategory.Engagement;
        var requirement = BadgeRequirement.ForTopContributor(10);
        var iconUrl = "https://example.com/badge.png";
        var displayOrder = 100;

        // Act
        var badge = Badge.Create(code, name, description, tier, category, requirement, iconUrl, displayOrder);

        // Assert
        badge.Code.Should().Be("TOP_CONTRIBUTOR"); // Uppercased
        badge.IconUrl.Should().Be(iconUrl);
        badge.DisplayOrder.Should().Be(displayOrder);
    }

    [Fact]
    public void Create_TrimsAndNormalizesInputs()
    {
        // Arrange
        var code = "  test_badge  ";
        var name = "  Test Badge  ";
        var description = "  Test description  ";
        var iconUrl = "  https://example.com/badge.png  ";

        // Act
        var badge = Badge.Create(
            code,
            name,
            description,
            BadgeTier.Bronze,
            BadgeCategory.Special,
            BadgeRequirement.ForFirstContribution(),
            iconUrl);

        // Assert
        badge.Code.Should().Be("TEST_BADGE");
        badge.Name.Should().Be("Test Badge");
        badge.Description.Should().Be("Test description");
        badge.IconUrl.Should().Be("https://example.com/badge.png");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCode_ThrowsArgumentException(string? invalidCode)
    {
        // Act
        var act = () => Badge.Create(
            invalidCode!,
            "Name",
            "Description",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("code")
            .WithMessage("*Code is required*");
    }

    [Fact]
    public void Create_WithCodeExceeding50Characters_ThrowsArgumentException()
    {
        // Arrange
        var longCode = new string('A', 51);

        // Act
        var act = () => Badge.Create(
            longCode,
            "Name",
            "Description",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("code")
            .WithMessage("*Code cannot exceed 50 characters*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsArgumentException(string? invalidName)
    {
        // Act
        var act = () => Badge.Create(
            "CODE",
            invalidName!,
            "Description",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name is required*");
    }

    [Fact]
    public void Create_WithNameExceeding100Characters_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('A', 101);

        // Act
        var act = () => Badge.Create(
            "CODE",
            longName,
            "Description",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name cannot exceed 100 characters*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDescription_ThrowsArgumentException(string? invalidDescription)
    {
        // Act
        var act = () => Badge.Create(
            "CODE",
            "Name",
            invalidDescription!,
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description is required*");
    }

    [Fact]
    public void Create_WithDescriptionExceeding500Characters_ThrowsArgumentException()
    {
        // Arrange
        var longDescription = new string('A', 501);

        // Act
        var act = () => Badge.Create(
            "CODE",
            "Name",
            longDescription,
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description cannot exceed 500 characters*");
    }

    [Fact]
    public void Create_WithNullRequirement_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Badge.Create(
            "CODE",
            "Name",
            "Description",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("requirement");
    }

    #endregion

    #region UpdateDetails Tests

    [Fact]
    public void UpdateDetails_WithValidParameters_UpdatesBadge()
    {
        // Arrange
        var badge = CreateTestBadge();
        var newName = "Updated Name";
        var newDescription = "Updated description";
        var newIconUrl = "https://example.com/new-icon.png";
        var newDisplayOrder = 50;

        // Act
        badge.UpdateDetails(newName, newDescription, newIconUrl, newDisplayOrder);

        // Assert
        badge.Name.Should().Be(newName);
        badge.Description.Should().Be(newDescription);
        badge.IconUrl.Should().Be(newIconUrl);
        badge.DisplayOrder.Should().Be(newDisplayOrder);
        badge.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateDetails_TrimsInputs()
    {
        // Arrange
        var badge = CreateTestBadge();

        // Act
        badge.UpdateDetails(
            "  New Name  ",
            "  New Description  ",
            "  https://example.com/icon.png  ",
            10);

        // Assert
        badge.Name.Should().Be("New Name");
        badge.Description.Should().Be("New Description");
        badge.IconUrl.Should().Be("https://example.com/icon.png");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithEmptyName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var badge = CreateTestBadge();

        // Act
        var act = () => badge.UpdateDetails(invalidName!, "Description", null, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name is required*");
    }

    [Fact]
    public void UpdateDetails_WithNameExceeding100Characters_ThrowsArgumentException()
    {
        // Arrange
        var badge = CreateTestBadge();
        var longName = new string('A', 101);

        // Act
        var act = () => badge.UpdateDetails(longName, "Description", null, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Name cannot exceed 100 characters*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithEmptyDescription_ThrowsArgumentException(string? invalidDescription)
    {
        // Arrange
        var badge = CreateTestBadge();

        // Act
        var act = () => badge.UpdateDetails("Name", invalidDescription!, null, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description is required*");
    }

    [Fact]
    public void UpdateDetails_WithDescriptionExceeding500Characters_ThrowsArgumentException()
    {
        // Arrange
        var badge = CreateTestBadge();
        var longDescription = new string('A', 501);

        // Act
        var act = () => badge.UpdateDetails("Name", longDescription, null, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description cannot exceed 500 characters*");
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public void Deactivate_WhenActive_DeactivatesBadge()
    {
        // Arrange
        var badge = CreateTestBadge();
        badge.IsActive.Should().BeTrue();

        // Act
        badge.Deactivate();

        // Assert
        badge.IsActive.Should().BeFalse();
        badge.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_DoesNotUpdateModifiedAt()
    {
        // Arrange
        var badge = CreateTestBadge();
        badge.Deactivate();
        var firstModifiedAt = badge.ModifiedAt;
        Thread.Sleep(10);

        // Act
        badge.Deactivate();

        // Assert
        badge.IsActive.Should().BeFalse();
        badge.ModifiedAt.Should().Be(firstModifiedAt);
    }

    [Fact]
    public void Activate_WhenInactive_ActivatesBadge()
    {
        // Arrange
        var badge = CreateTestBadge();
        badge.Deactivate();

        // Act
        badge.Activate();

        // Assert
        badge.IsActive.Should().BeTrue();
        badge.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Activate_WhenAlreadyActive_DoesNotUpdateModifiedAt()
    {
        // Arrange
        var badge = CreateTestBadge();
        badge.ModifiedAt.Should().BeNull(); // Newly created

        // Act
        badge.Activate();

        // Assert
        badge.IsActive.Should().BeTrue();
        badge.ModifiedAt.Should().BeNull(); // Still null
    }

    #endregion

    #region Tier and Category Tests

    [Theory]
    [InlineData(BadgeTier.Bronze)]
    [InlineData(BadgeTier.Silver)]
    [InlineData(BadgeTier.Gold)]
    [InlineData(BadgeTier.Platinum)]
    [InlineData(BadgeTier.Diamond)]
    public void Create_WithDifferentTiers_SetsTierCorrectly(BadgeTier tier)
    {
        // Act
        var badge = Badge.Create(
            "CODE",
            "Name",
            "Description",
            tier,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        // Assert
        badge.Tier.Should().Be(tier);
    }

    [Theory]
    [InlineData(BadgeCategory.Contribution)]
    [InlineData(BadgeCategory.Quality)]
    [InlineData(BadgeCategory.Engagement)]
    [InlineData(BadgeCategory.Special)]
    public void Create_WithDifferentCategories_SetsCategoryCorrectly(BadgeCategory category)
    {
        // Act
        var badge = Badge.Create(
            "CODE",
            "Name",
            "Description",
            BadgeTier.Bronze,
            category,
            BadgeRequirement.ForFirstContribution());

        // Assert
        badge.Category.Should().Be(category);
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var code = "TEST_CODE";
        var name = "Test Name";
        var description = "Test Description";
        var iconUrl = "https://example.com/icon.png";
        var tier = BadgeTier.Gold;
        var category = BadgeCategory.Quality;
        var isActive = false;
        var displayOrder = 50;
        var requirement = BadgeRequirement.ForContributionCount(10);
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var modifiedAt = DateTime.UtcNow;

        // Act
        var badge = new Badge(
            id, code, name, description, iconUrl, tier, category,
            isActive, displayOrder, requirement, createdAt, modifiedAt);

        // Assert
        badge.Id.Should().Be(id);
        badge.Code.Should().Be(code);
        badge.Name.Should().Be(name);
        badge.Description.Should().Be(description);
        badge.IconUrl.Should().Be(iconUrl);
        badge.Tier.Should().Be(tier);
        badge.Category.Should().Be(category);
        badge.IsActive.Should().Be(isActive);
        badge.DisplayOrder.Should().Be(displayOrder);
        badge.Requirement.Should().Be(requirement);
        badge.CreatedAt.Should().Be(createdAt);
        badge.ModifiedAt.Should().Be(modifiedAt);
    }

    #endregion

    #region Helper Methods

    private static Badge CreateTestBadge()
    {
        return Badge.Create(
            "TEST_BADGE",
            "Test Badge",
            "Test description",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());
    }

    #endregion
}