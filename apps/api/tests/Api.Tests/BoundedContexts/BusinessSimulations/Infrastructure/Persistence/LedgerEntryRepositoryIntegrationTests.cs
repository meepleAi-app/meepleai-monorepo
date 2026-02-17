using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;

/// <summary>
/// Integration tests for LedgerEntryRepository (Issue #4539 - Epic #3688)
/// Tests actual database interactions with in-memory database
/// </summary>
public class LedgerEntryRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly MeepleAiDbContext _context;
    private readonly LedgerEntryRepository _repository;
    private readonly DomainEventCollector _eventCollector;

    public LedgerEntryRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"LedgerEntryRepositoryTests_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        _eventCollector = new DomainEventCollector();
        _repository = new LedgerEntryRepository(_context, _eventCollector);
    }

    public async ValueTask InitializeAsync()
    {
        // Seed test data
        var entries = new[]
        {
            LedgerEntry.CreateAutoEntry(
                DateTime.UtcNow.AddDays(-30),
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                1250m,
                "EUR",
                "January subscriptions"),

            LedgerEntry.CreateAutoEntry(
                DateTime.UtcNow.AddDays(-25),
                LedgerEntryType.Expense,
                LedgerCategory.TokenUsage,
                450m,
                "EUR",
                "OpenRouter API costs"),

            LedgerEntry.CreateManualEntry(
                DateTime.UtcNow.AddDays(-20),
                LedgerEntryType.Expense,
                LedgerCategory.Infrastructure,
                250m,
                Guid.NewGuid(),
                "EUR",
                "Server hosting costs"),

            LedgerEntry.CreateAutoEntry(
                DateTime.UtcNow.AddDays(-15),
                LedgerEntryType.Income,
                LedgerCategory.TokenPurchase,
                500m,
                "EUR",
                "Token top-up purchases"),

            LedgerEntry.CreateManualEntry(
                DateTime.UtcNow.AddDays(-10),
                LedgerEntryType.Expense,
                LedgerCategory.Marketing,
                300m,
                Guid.NewGuid(),
                "EUR",
                "Google Ads campaign")
        };

        _context.LedgerEntries.AddRange(entries);
        await _context.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        return ValueTask.CompletedTask;
    }

    #region CRUD Tests

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddAsync_WithValidEntry_SavesSuccessfully()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            DateTime.UtcNow,
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            100m);

        // Act
        await _repository.AddAsync(entry);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.LedgerEntries.FindAsync(entry.Id);
        Assert.NotNull(saved);
        Assert.Equal(entry.Amount.Amount, saved.Amount.Amount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByIdAsync_WithExistingId_ReturnsEntry()
    {
        // Arrange
        var existingEntry = await _context.LedgerEntries.FirstAsync();

        // Act
        var result = await _repository.GetByIdAsync(existingEntry.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingEntry.Id, result.Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistingId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateAsync_WithModifiedEntry_SavesChanges()
    {
        // Arrange
        var entry = await _context.LedgerEntries.FirstAsync();
        var originalDescription = entry.Description;
        entry.UpdateDescription("Updated description");

        // Act
        await _repository.UpdateAsync(entry);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.LedgerEntries.FindAsync(entry.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated description", updated.Description);
        Assert.NotEqual(originalDescription, updated.Description);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeleteAsync_WithExistingEntry_RemovesEntry()
    {
        // Arrange
        var entry = await _context.LedgerEntries.FirstAsync();

        // Act
        await _repository.DeleteAsync(entry);
        await _context.SaveChangesAsync();

        // Assert
        var deleted = await _context.LedgerEntries.FindAsync(entry.Id);
        Assert.Null(deleted);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExistsAsync_WithExistingId_ReturnsTrue()
    {
        // Arrange
        var entry = await _context.LedgerEntries.FirstAsync();

        // Act
        var exists = await _repository.ExistsAsync(entry.Id);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExistsAsync_WithNonExistingId_ReturnsFalse()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var exists = await _repository.ExistsAsync(nonExistingId);

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region Query Tests

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAllAsync_ReturnsAllEntriesOrderedByDateDescending()
    {
        // Act
        var entries = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(5, entries.Count);
        Assert.True(entries[0].Date >= entries[1].Date);
        Assert.True(entries[1].Date >= entries[2].Date);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByDateRangeAsync_WithValidRange_ReturnsFilteredEntries()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-25);
        var to = DateTime.UtcNow.AddDays(-15);

        // Act
        var (entries, total) = await _repository.GetByDateRangeAsync(from, to);

        // Assert
        Assert.Equal(2, total); // Only entries in -25 to -15 range
        Assert.All(entries, e => Assert.True(e.Date >= from && e.Date <= to));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByDateRangeAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-40);
        var to = DateTime.UtcNow;

        // Act
        var (page1, total1) = await _repository.GetByDateRangeAsync(from, to, page: 1, pageSize: 2);
        var (page2, total2) = await _repository.GetByDateRangeAsync(from, to, page: 2, pageSize: 2);

        // Assert
        Assert.Equal(5, total1);
        Assert.Equal(5, total2);
        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.NotEqual(page1[0].Id, page2[0].Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByTypeAsync_WithIncomeType_ReturnsOnlyIncome()
    {
        // Act
        var (entries, total) = await _repository.GetByTypeAsync(LedgerEntryType.Income);

        // Assert
        Assert.Equal(2, total);
        Assert.All(entries, e => Assert.Equal(LedgerEntryType.Income, e.Type));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByTypeAsync_WithExpenseType_ReturnsOnlyExpenses()
    {
        // Act
        var (entries, total) = await _repository.GetByTypeAsync(LedgerEntryType.Expense);

        // Assert
        Assert.Equal(3, total);
        Assert.All(entries, e => Assert.Equal(LedgerEntryType.Expense, e.Type));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByCategoryAsync_WithSpecificCategory_ReturnsFilteredEntries()
    {
        // Act
        var (entries, total) = await _repository.GetByCategoryAsync(LedgerCategory.Subscription);

        // Assert
        Assert.Equal(1, total);
        Assert.All(entries, e => Assert.Equal(LedgerCategory.Subscription, e.Category));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFilteredAsync_WithMultipleFilters_ReturnsMatchingEntries()
    {
        // Arrange - Look for manual expenses (we have 2: Infrastructure + Marketing)
        var dateFrom = DateTime.UtcNow.AddDays(-25);
        var dateTo = DateTime.UtcNow;

        // Act
        var (entries, total) = await _repository.GetFilteredAsync(
            type: LedgerEntryType.Expense,
            category: null,
            source: LedgerEntrySource.Manual,
            dateFrom: dateFrom,
            dateTo: dateTo);

        // Assert
        Assert.Equal(2, total); // 2 manual expenses: Infrastructure (-20d) + Marketing (-10d)
        Assert.All(entries, e =>
        {
            Assert.Equal(LedgerEntryType.Expense, e.Type);
            Assert.Equal(LedgerEntrySource.Manual, e.Source);
            Assert.True(e.Date >= dateFrom && e.Date <= dateTo);
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetFilteredAsync_WithNoFilters_ReturnsAllEntries()
    {
        // Act
        var (entries, total) = await _repository.GetFilteredAsync();

        // Assert
        Assert.Equal(5, total);
        Assert.Equal(5, entries.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSummaryByDateRangeAsync_CalculatesCorrectTotals()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-40);
        var to = DateTime.UtcNow;

        // Act
        var (totalIncome, totalExpense) = await _repository.GetSummaryByDateRangeAsync(from, to);

        // Assert
        Assert.Equal(1750m, totalIncome);  // 1250 + 500
        Assert.Equal(1000m, totalExpense); // 450 + 250 + 300
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSummaryByDateRangeAsync_WithEmptyRange_ReturnsZeros()
    {
        // Arrange
        var from = DateTime.UtcNow.AddYears(-10);
        var to = DateTime.UtcNow.AddYears(-9);

        // Act
        var (totalIncome, totalExpense) = await _repository.GetSummaryByDateRangeAsync(from, to);

        // Assert
        Assert.Equal(0m, totalIncome);
        Assert.Equal(0m, totalExpense);
    }

    #endregion

    #region Pagination Validation Tests

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByDateRangeAsync_WithInvalidPage_ClampsToMinimum()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-40);
        var to = DateTime.UtcNow;

        // Act
        var (entries, total) = await _repository.GetByDateRangeAsync(from, to, page: 0, pageSize: 2);

        // Assert
        Assert.Equal(2, entries.Count); // Should clamp to page 1
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByDateRangeAsync_WithPageSizeTooLarge_ClampsToMaximum()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-40);
        var to = DateTime.UtcNow;

        // Act
        var (entries, total) = await _repository.GetByDateRangeAsync(from, to, page: 1, pageSize: 200);

        // Assert
        Assert.True(entries.Count <= 100); // Should clamp to max 100
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddAsync_MultipleSequentialAdds_AllSucceed()
    {
        // Arrange & Act - Sequential adds (InMemoryDatabase not thread-safe for concurrent)
        for (int i = 1; i <= 10; i++)
        {
            var entry = LedgerEntry.CreateAutoEntry(
                DateTime.UtcNow,
                LedgerEntryType.Income,
                LedgerCategory.Subscription,
                100m * i);

            await _repository.AddAsync(entry);
            await _context.SaveChangesAsync();
        }

        // Assert
        var allEntries = await _context.LedgerEntries.ToListAsync();
        Assert.Equal(15, allEntries.Count); // 5 seed + 10 added
    }

    #endregion
}
