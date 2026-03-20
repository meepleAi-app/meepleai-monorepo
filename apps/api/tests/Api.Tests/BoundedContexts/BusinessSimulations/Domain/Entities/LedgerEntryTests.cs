using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Unit tests for LedgerEntry entity (Issue #4539 - Epic #3688)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public class LedgerEntryTests
{
    #region Factory Method Tests

    [Fact]
    public void Create_WithValidParameters_CreatesLedgerEntry()
    {
        // Arrange
        var date = DateTime.UtcNow.AddDays(-1);
        var amount = Money.Create(100.50m, "EUR");
        var userId = Guid.NewGuid();

        // Act
        var entry = LedgerEntry.Create(
            date,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            amount,
            LedgerEntrySource.Manual,
            "Monthly subscription payment",
            null,
            userId);

        // Assert
        entry.Id.Should().NotBe(Guid.Empty);
        entry.Date.Should().Be(date);
        entry.Type.Should().Be(LedgerEntryType.Income);
        entry.Category.Should().Be(LedgerCategory.Subscription);
        entry.Amount.Should().Be(amount);
        entry.Source.Should().Be(LedgerEntrySource.Manual);
        entry.Description.Should().Be("Monthly subscription payment");
        entry.CreatedByUserId.Should().Be(userId);
        entry.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public void CreateAutoEntry_WithValidParameters_CreatesAutomaticEntry()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = 50.25m;

        // Act
        var entry = LedgerEntry.CreateAutoEntry(
            date,
            LedgerEntryType.Expense,
            LedgerCategory.TokenUsage,
            amount,
            "EUR",
            "OpenRouter API costs");

        // Assert
        entry.Type.Should().Be(LedgerEntryType.Expense);
        entry.Category.Should().Be(LedgerCategory.TokenUsage);
        entry.Amount.Amount.Should().Be(amount);
        entry.Amount.Currency.Should().Be("EUR");
        entry.Source.Should().Be(LedgerEntrySource.Auto);
        entry.CreatedByUserId.Should().BeNull();
    }

    [Fact]
    public void CreateManualEntry_WithValidParameters_CreatesManualEntry()
    {
        // Arrange
        var date = DateTime.UtcNow.AddDays(-2);
        var amount = 250.00m;
        var userId = Guid.NewGuid();

        // Act
        var entry = LedgerEntry.CreateManualEntry(
            date,
            LedgerEntryType.Expense,
            LedgerCategory.Infrastructure,
            amount,
            userId,
            "EUR",
            "Server hosting costs");

        // Assert
        entry.Type.Should().Be(LedgerEntryType.Expense);
        entry.Category.Should().Be(LedgerCategory.Infrastructure);
        entry.Source.Should().Be(LedgerEntrySource.Manual);
        entry.CreatedByUserId.Should().Be(userId);
        entry.Description.Should().Be("Server hosting costs");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithFutureDate_ThrowsArgumentException()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(2);
        var amount = Money.Create(100m, "EUR");

        // Act & Assert
        var act = () =>
            LedgerEntry.Create(
                futureDate,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Auto);
        var ex = act.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("cannot be in the future");
    }

    [Fact]
    public void Create_WithZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var zeroAmount = Money.Create(0m, "EUR");

        // Act & Assert
        var act2 = () =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                zeroAmount,
                LedgerEntrySource.Auto);
        var ex = act2.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("must be greater than zero");
    }

    [Fact]
    public void Create_WithNullAmount_ThrowsArgumentNullException()
    {
        // Arrange
        var date = DateTime.UtcNow;

        // Act & Assert
        var act3 = () =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                null!,
                LedgerEntrySource.Auto);
        act3.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithDescriptionTooLong_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");
        var longDescription = new string('x', 501);

        // Act & Assert
        var act4 = () =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Auto,
                longDescription);
        var ex = act4.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("cannot exceed 500 characters");
    }

    [Fact]
    public void Create_WithMetadataTooLong_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");
        var longMetadata = new string('x', 4001);

        // Act & Assert
        var act5 = () =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Auto,
                metadata: longMetadata);
        var ex = act5.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("cannot exceed 4000 characters");
    }

    [Fact]
    public void Create_ManualWithoutUserId_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");

        // Act & Assert
        var act6 = () =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Manual,
                createdByUserId: null);
        var ex = act6.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("Manual entries must have a CreatedByUserId");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");

        // Act & Assert
        var act7 = () =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Manual,
                createdByUserId: Guid.Empty);
        var ex = act7.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("cannot be empty");
    }

    #endregion

    #region Update Method Tests

    [Fact]
    public void UpdateDescription_WithValidDescription_UpdatesSuccessfully()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            DateTime.UtcNow,
            LedgerEntryType.Expense,
            LedgerCategory.TokenUsage,
            50m);

        // Act
        entry.UpdateDescription("Updated description");

        // Assert
        entry.Description.Should().Be("Updated description");
        entry.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateDescription_WithTooLongDescription_ThrowsArgumentException()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            DateTime.UtcNow,
            LedgerEntryType.Expense,
            LedgerCategory.TokenUsage,
            50m);
        var longDescription = new string('x', 501);

        // Act & Assert
        var act8 = () =>
            entry.UpdateDescription(longDescription);
        var ex = act8.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().Contain("cannot exceed 500 characters");
    }

    [Fact]
    public void UpdateMetadata_WithValidMetadata_UpdatesSuccessfully()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            DateTime.UtcNow,
            LedgerEntryType.Expense,
            LedgerCategory.TokenUsage,
            50m);
        var metadata = "{\"model\":\"gpt-4o\",\"tokens\":1250}";

        // Act
        entry.UpdateMetadata(metadata);

        // Assert
        entry.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void UpdateCategory_WithNewCategory_UpdatesSuccessfully()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            DateTime.UtcNow,
            LedgerEntryType.Expense,
            LedgerCategory.TokenUsage,
            50m);

        // Act
        entry.UpdateCategory(LedgerCategory.Infrastructure);

        // Assert
        entry.Category.Should().Be(LedgerCategory.Infrastructure);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithWhitespaceDescription_TrimsDescription()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");
        var description = "  Test description  ";

        // Act
        var entry = LedgerEntry.Create(
            date,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            amount,
            LedgerEntrySource.Auto,
            description);

        // Assert
        entry.Description.Should().Be("Test description");
    }

    [Fact]
    public void Create_WithNullDescription_AllowsNull()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");

        // Act
        var entry = LedgerEntry.Create(
            date,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            amount,
            LedgerEntrySource.Auto,
            description: null);

        // Assert
        entry.Description.Should().BeNull();
    }

    [Fact]
    public void CreateAutoEntry_WithAllCategories_CreatesSuccessfully()
    {
        // Arrange & Act
        var categories = Enum.GetValues<LedgerCategory>();
        var entries = categories.Select(category =>
            LedgerEntry.CreateAutoEntry(
                DateTime.UtcNow,
                LedgerEntryType.Expense,
                category,
                100m)).ToList();

        // Assert
        entries.Count.Should().Be(categories.Length);
        entries.Should().AllSatisfy(e => e.Source.Should().Be(LedgerEntrySource.Auto));
    }

    #endregion
}
