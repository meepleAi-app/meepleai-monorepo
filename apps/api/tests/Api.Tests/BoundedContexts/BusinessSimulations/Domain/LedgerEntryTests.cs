using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Domain;

/// <summary>
/// Unit tests for LedgerEntry aggregate (Issue #3720)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class LedgerEntryTests
{
    private static readonly DateTime ValidDate = DateTime.UtcNow.AddDays(-1);
    private static readonly Guid ValidUserId = Guid.NewGuid();

    #region Create Factory Method

    [Fact]
    public void Create_WithValidData_ShouldCreateEntry()
    {
        // Arrange
        var amount = Money.InEur(100m);

        // Act
        var entry = LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            amount,
            LedgerEntrySource.Auto);

        // Assert
        entry.Should().NotBeNull();
        entry.Id.Should().NotBe(Guid.Empty);
        entry.Date.Should().Be(ValidDate);
        entry.Type.Should().Be(LedgerEntryType.Income);
        entry.Category.Should().Be(LedgerCategory.Subscription);
        entry.Amount.Should().Be(amount);
        entry.Source.Should().Be(LedgerEntrySource.Auto);
        entry.Description.Should().BeNull();
        entry.Metadata.Should().BeNull();
        entry.CreatedByUserId.Should().BeNull();
        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entry.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithDescription_ShouldTrimAndSet()
    {
        // Act
        var entry = LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Expense,
            LedgerCategory.Infrastructure,
            Money.InEur(50m),
            LedgerEntrySource.Manual,
            description: "  Server hosting cost  ",
            createdByUserId: ValidUserId);

        // Assert
        entry.Description.Should().Be("Server hosting cost");
    }

    [Fact]
    public void Create_WithMetadata_ShouldSetMetadata()
    {
        // Act
        var entry = LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Income,
            LedgerCategory.TokenPurchase,
            Money.InEur(9.99m),
            LedgerEntrySource.Auto,
            metadata: "{\"userId\":\"abc\",\"tokens\":1000}");

        // Assert
        entry.Metadata.Should().Contain("tokens");
    }

    [Fact]
    public void Create_WithNullAmount_ShouldThrow()
    {
        // Act
        var act = () => LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            null!,
            LedgerEntrySource.Auto);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithFutureDate_ShouldThrow()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(10);

        // Act
        var act = () => LedgerEntry.Create(
            futureDate,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            Money.InEur(100m),
            LedgerEntrySource.Auto);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("date");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldThrow()
    {
        // Act
        var act = () => LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            Money.Zero(),
            LedgerEntrySource.Auto);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Create_WithTooLongDescription_ShouldThrow()
    {
        // Arrange
        var longDescription = new string('a', 501);

        // Act
        var act = () => LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            Money.InEur(100m),
            LedgerEntrySource.Auto,
            description: longDescription);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("description");
    }

    [Fact]
    public void Create_WithTooLongMetadata_ShouldThrow()
    {
        // Arrange
        var longMetadata = new string('a', 4001);

        // Act
        var act = () => LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            Money.InEur(100m),
            LedgerEntrySource.Auto,
            metadata: longMetadata);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("metadata");
    }

    [Fact]
    public void Create_ManualSourceWithoutUserId_ShouldThrow()
    {
        // Act
        var act = () => LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Expense,
            LedgerCategory.Operational,
            Money.InEur(100m),
            LedgerEntrySource.Manual,
            createdByUserId: null);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("createdByUserId");
    }

    [Fact]
    public void Create_WithEmptyGuidUserId_ShouldThrow()
    {
        // Act
        var act = () => LedgerEntry.Create(
            ValidDate,
            LedgerEntryType.Expense,
            LedgerCategory.Operational,
            Money.InEur(100m),
            LedgerEntrySource.Manual,
            createdByUserId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("createdByUserId");
    }

    #endregion

    #region CreateAutoEntry

    [Fact]
    public void CreateAutoEntry_ShouldCreateAutoSourceEntry()
    {
        // Act
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate,
            LedgerEntryType.Expense,
            LedgerCategory.TokenUsage,
            0.05m,
            description: "AI token usage");

        // Assert
        entry.Source.Should().Be(LedgerEntrySource.Auto);
        entry.CreatedByUserId.Should().BeNull();
        entry.Amount.Amount.Should().Be(0.05m);
        entry.Amount.Currency.Should().Be("EUR");
    }

    [Fact]
    public void CreateAutoEntry_WithUsdCurrency_ShouldUseUsd()
    {
        // Act
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            19.99m,
            currency: "USD");

        // Assert
        entry.Amount.Currency.Should().Be("USD");
    }

    #endregion

    #region CreateManualEntry

    [Fact]
    public void CreateManualEntry_ShouldCreateManualSourceEntry()
    {
        // Act
        var entry = LedgerEntry.CreateManualEntry(
            ValidDate,
            LedgerEntryType.Expense,
            LedgerCategory.Marketing,
            500m,
            ValidUserId,
            description: "Ad campaign");

        // Assert
        entry.Source.Should().Be(LedgerEntrySource.Manual);
        entry.CreatedByUserId.Should().Be(ValidUserId);
        entry.Amount.Amount.Should().Be(500m);
        entry.Description.Should().Be("Ad campaign");
    }

    #endregion

    #region UpdateDescription

    [Fact]
    public void UpdateDescription_WithValidValue_ShouldUpdateAndSetTimestamp()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate, LedgerEntryType.Income, LedgerCategory.Subscription, 10m);

        // Act
        entry.UpdateDescription("Updated description");

        // Assert
        entry.Description.Should().Be("Updated description");
        entry.UpdatedAt.Should().NotBeNull();
        entry.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateDescription_WithNull_ShouldClearDescription()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate, LedgerEntryType.Income, LedgerCategory.Subscription, 10m,
            description: "Original");

        // Act
        entry.UpdateDescription(null);

        // Assert
        entry.Description.Should().BeNull();
        entry.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateDescription_WithTooLong_ShouldThrow()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate, LedgerEntryType.Income, LedgerCategory.Subscription, 10m);

        // Act
        var act = () => entry.UpdateDescription(new string('x', 501));

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("description");
    }

    #endregion

    #region UpdateMetadata

    [Fact]
    public void UpdateMetadata_WithValidJson_ShouldUpdate()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate, LedgerEntryType.Income, LedgerCategory.Subscription, 10m);

        // Act
        entry.UpdateMetadata("{\"key\":\"value\"}");

        // Assert
        entry.Metadata.Should().Be("{\"key\":\"value\"}");
        entry.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateMetadata_WithTooLong_ShouldThrow()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate, LedgerEntryType.Income, LedgerCategory.Subscription, 10m);

        // Act
        var act = () => entry.UpdateMetadata(new string('x', 4001));

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("metadata");
    }

    #endregion

    #region UpdateCategory

    [Fact]
    public void UpdateCategory_ShouldChangeAndSetTimestamp()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            ValidDate, LedgerEntryType.Expense, LedgerCategory.Other, 100m);

        // Act
        entry.UpdateCategory(LedgerCategory.Infrastructure);

        // Assert
        entry.Category.Should().Be(LedgerCategory.Infrastructure);
        entry.UpdatedAt.Should().NotBeNull();
    }

    #endregion

    #region All Enum Values

    [Theory]
    [InlineData(LedgerEntryType.Income)]
    [InlineData(LedgerEntryType.Expense)]
    public void Create_WithAllTypes_ShouldSucceed(LedgerEntryType type)
    {
        // Act
        var entry = LedgerEntry.CreateAutoEntry(ValidDate, type, LedgerCategory.Other, 10m);

        // Assert
        entry.Type.Should().Be(type);
    }

    [Theory]
    [InlineData(LedgerCategory.Subscription)]
    [InlineData(LedgerCategory.TokenPurchase)]
    [InlineData(LedgerCategory.TokenUsage)]
    [InlineData(LedgerCategory.PlatformFee)]
    [InlineData(LedgerCategory.Refund)]
    [InlineData(LedgerCategory.Operational)]
    [InlineData(LedgerCategory.Marketing)]
    [InlineData(LedgerCategory.Infrastructure)]
    [InlineData(LedgerCategory.Other)]
    public void Create_WithAllCategories_ShouldSucceed(LedgerCategory category)
    {
        // Act
        var entry = LedgerEntry.CreateAutoEntry(ValidDate, LedgerEntryType.Income, category, 10m);

        // Assert
        entry.Category.Should().Be(category);
    }

    #endregion
}
