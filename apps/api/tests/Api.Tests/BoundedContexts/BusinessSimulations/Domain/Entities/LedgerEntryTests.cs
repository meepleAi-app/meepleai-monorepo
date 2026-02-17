using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Domain.Entities;

/// <summary>
/// Unit tests for LedgerEntry entity (Issue #4539 - Epic #3688)
/// </summary>
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
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(date, entry.Date);
        Assert.Equal(LedgerEntryType.Income, entry.Type);
        Assert.Equal(LedgerCategory.Subscription, entry.Category);
        Assert.Equal(amount, entry.Amount);
        Assert.Equal(LedgerEntrySource.Manual, entry.Source);
        Assert.Equal("Monthly subscription payment", entry.Description);
        Assert.Equal(userId, entry.CreatedByUserId);
        Assert.NotEqual(default, entry.CreatedAt);
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
        Assert.Equal(LedgerEntryType.Expense, entry.Type);
        Assert.Equal(LedgerCategory.TokenUsage, entry.Category);
        Assert.Equal(amount, entry.Amount.Amount);
        Assert.Equal("EUR", entry.Amount.Currency);
        Assert.Equal(LedgerEntrySource.Auto, entry.Source);
        Assert.Null(entry.CreatedByUserId);
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
        Assert.Equal(LedgerEntryType.Expense, entry.Type);
        Assert.Equal(LedgerCategory.Infrastructure, entry.Category);
        Assert.Equal(LedgerEntrySource.Manual, entry.Source);
        Assert.Equal(userId, entry.CreatedByUserId);
        Assert.Equal("Server hosting costs", entry.Description);
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
        var ex = Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                futureDate,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Auto));

        Assert.Contains("cannot be in the future", ex.Message);
    }

    [Fact]
    public void Create_WithZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var zeroAmount = Money.Create(0m, "EUR");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                zeroAmount,
                LedgerEntrySource.Auto));

        Assert.Contains("must be greater than zero", ex.Message);
    }

    [Fact]
    public void Create_WithNullAmount_ThrowsArgumentNullException()
    {
        // Arrange
        var date = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                null!,
                LedgerEntrySource.Auto));
    }

    [Fact]
    public void Create_WithDescriptionTooLong_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");
        var longDescription = new string('x', 501);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Auto,
                longDescription));

        Assert.Contains("cannot exceed 500 characters", ex.Message);
    }

    [Fact]
    public void Create_WithMetadataTooLong_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");
        var longMetadata = new string('x', 4001);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Auto,
                metadata: longMetadata));

        Assert.Contains("cannot exceed 4000 characters", ex.Message);
    }

    [Fact]
    public void Create_ManualWithoutUserId_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Manual,
                createdByUserId: null));

        Assert.Contains("Manual entries must have a CreatedByUserId", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.UtcNow;
        var amount = Money.Create(100m, "EUR");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            LedgerEntry.Create(
                date,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                amount,
                LedgerEntrySource.Manual,
                createdByUserId: Guid.Empty));

        Assert.Contains("cannot be empty", ex.Message);
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
        Assert.Equal("Updated description", entry.Description);
        Assert.NotNull(entry.UpdatedAt);
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
        var ex = Assert.Throws<ArgumentException>(() =>
            entry.UpdateDescription(longDescription));

        Assert.Contains("cannot exceed 500 characters", ex.Message);
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
        Assert.Equal(metadata, entry.Metadata);
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
        Assert.Equal(LedgerCategory.Infrastructure, entry.Category);
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
        Assert.Equal("Test description", entry.Description);
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
        Assert.Null(entry.Description);
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
        Assert.Equal(categories.Length, entries.Count);
        Assert.All(entries, e => Assert.Equal(LedgerEntrySource.Auto, e.Source));
    }

    #endregion
}
