using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Infrastructure;

/// <summary>
/// Unit tests for LedgerEntryRepository using InMemory database (Issue #3720)
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class LedgerEntryRepositoryTests
{
    private static readonly DateTime BaseDate = DateTime.UtcNow.AddDays(-30);

    private static MeepleAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    private static LedgerEntryRepository CreateRepository(MeepleAiDbContext dbContext)
    {
        return new LedgerEntryRepository(dbContext, new Mock<IDomainEventCollector>().Object);
    }

    private static LedgerEntry CreateTestEntry(
        DateTime? date = null,
        LedgerEntryType type = LedgerEntryType.Income,
        LedgerCategory category = LedgerCategory.Subscription,
        decimal amount = 100m)
    {
        return LedgerEntry.CreateAutoEntry(
            date ?? BaseDate,
            type,
            category,
            amount,
            description: "Test entry");
    }

    #region AddAsync and GetByIdAsync

    [Fact]
    public async Task AddAsync_WithValidEntry_ShouldPersist()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);
        var entry = CreateTestEntry();

        // Act
        await repo.AddAsync(entry);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await repo.GetByIdAsync(entry.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(entry.Id);
        retrieved.Type.Should().Be(LedgerEntryType.Income);
        retrieved.Category.Should().Be(LedgerCategory.Subscription);
        retrieved.Amount.Amount.Should().Be(100m);
        retrieved.Amount.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        // Act
        var result = await repo.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_WithMultipleEntries_ShouldReturnAllOrderedByDateDesc()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(1)));
        await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(3)));
        await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(2)));
        await dbContext.SaveChangesAsync();

        // Act
        var results = await repo.GetAllAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Date.Should().BeAfter(results[1].Date);
        results[1].Date.Should().BeAfter(results[2].Date);
    }

    [Fact]
    public async Task GetAllAsync_WithNoEntries_ShouldReturnEmpty()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        // Act
        var results = await repo.GetAllAsync();

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);
        var entry = CreateTestEntry();
        await repo.AddAsync(entry);
        await dbContext.SaveChangesAsync();

        // Act
        entry.UpdateDescription("Updated description");
        await repo.UpdateAsync(entry);
        await dbContext.SaveChangesAsync();

        // Assert - use fresh context to verify persistence
        using var dbContext2 = CreateDbContext();
        // InMemory DB is scoped to the same name, so we need to re-read from same context
        var retrieved = await dbContext.LedgerEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entry.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be("Updated description");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEntry()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);
        var entry = CreateTestEntry();
        await repo.AddAsync(entry);
        await dbContext.SaveChangesAsync();

        // Act
        await repo.DeleteAsync(entry);
        await dbContext.SaveChangesAsync();

        // Assert
        var exists = await repo.ExistsAsync(entry.Id);
        exists.Should().BeFalse();
    }

    #endregion

    #region ExistsAsync

    [Fact]
    public async Task ExistsAsync_WithExistingEntry_ShouldReturnTrue()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);
        var entry = CreateTestEntry();
        await repo.AddAsync(entry);
        await dbContext.SaveChangesAsync();

        // Act
        var exists = await repo.ExistsAsync(entry.Id);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentEntry_ShouldReturnFalse()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        // Act
        var exists = await repo.ExistsAsync(Guid.NewGuid());

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region GetByDateRangeAsync

    [Fact]
    public async Task GetByDateRangeAsync_ShouldReturnEntriesInRange()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(1)));
        await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(5)));
        await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(10)));
        await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(15)));
        await dbContext.SaveChangesAsync();

        // Act
        var (entries, total) = await repo.GetByDateRangeAsync(
            BaseDate, BaseDate.AddDays(10));

        // Assert
        total.Should().Be(3);
        entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByDateRangeAsync_WithPagination_ShouldRespectPageSize()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        for (int i = 0; i < 5; i++)
            await repo.AddAsync(CreateTestEntry(date: BaseDate.AddDays(i)));
        await dbContext.SaveChangesAsync();

        // Act
        var (entries, total) = await repo.GetByDateRangeAsync(
            BaseDate.AddDays(-1), BaseDate.AddDays(10),
            page: 1, pageSize: 2);

        // Assert
        total.Should().Be(5);
        entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByDateRangeAsync_WithInvalidPage_ShouldDefaultToPage1()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);
        await repo.AddAsync(CreateTestEntry());
        await dbContext.SaveChangesAsync();

        // Act
        var (entries, _) = await repo.GetByDateRangeAsync(
            BaseDate.AddDays(-1), BaseDate.AddDays(1),
            page: 0, pageSize: 20);

        // Assert
        entries.Should().HaveCount(1);
    }

    #endregion

    #region GetByTypeAsync

    [Fact]
    public async Task GetByTypeAsync_ShouldFilterByType()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        await repo.AddAsync(CreateTestEntry(type: LedgerEntryType.Income));
        await repo.AddAsync(CreateTestEntry(type: LedgerEntryType.Income));
        await repo.AddAsync(CreateTestEntry(type: LedgerEntryType.Expense));
        await dbContext.SaveChangesAsync();

        // Act
        var (entries, total) = await repo.GetByTypeAsync(LedgerEntryType.Income);

        // Assert
        total.Should().Be(2);
        entries.Should().AllSatisfy(e => e.Type.Should().Be(LedgerEntryType.Income));
    }

    #endregion

    #region GetByCategoryAsync

    [Fact]
    public async Task GetByCategoryAsync_ShouldFilterByCategory()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        await repo.AddAsync(CreateTestEntry(category: LedgerCategory.Subscription));
        await repo.AddAsync(CreateTestEntry(category: LedgerCategory.Subscription));
        await repo.AddAsync(CreateTestEntry(category: LedgerCategory.Marketing));
        await dbContext.SaveChangesAsync();

        // Act
        var (entries, total) = await repo.GetByCategoryAsync(LedgerCategory.Subscription);

        // Assert
        total.Should().Be(2);
        entries.Should().AllSatisfy(e => e.Category.Should().Be(LedgerCategory.Subscription));
    }

    #endregion

    #region GetSummaryByDateRangeAsync

    [Fact]
    public async Task GetSummaryByDateRangeAsync_ShouldSumByType()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        await repo.AddAsync(CreateTestEntry(type: LedgerEntryType.Income, amount: 100m));
        await repo.AddAsync(CreateTestEntry(type: LedgerEntryType.Income, amount: 200m));
        await repo.AddAsync(CreateTestEntry(type: LedgerEntryType.Expense, amount: 50m));
        await repo.AddAsync(CreateTestEntry(type: LedgerEntryType.Expense, amount: 75m));
        await dbContext.SaveChangesAsync();

        // Act
        var (totalIncome, totalExpense) = await repo.GetSummaryByDateRangeAsync(
            BaseDate.AddDays(-1), BaseDate.AddDays(1));

        // Assert
        totalIncome.Should().Be(300m);
        totalExpense.Should().Be(125m);
    }

    [Fact]
    public async Task GetSummaryByDateRangeAsync_WithNoEntries_ShouldReturnZeros()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repo = CreateRepository(dbContext);

        // Act
        var (totalIncome, totalExpense) = await repo.GetSummaryByDateRangeAsync(
            BaseDate.AddDays(-1), BaseDate.AddDays(1));

        // Assert
        totalIncome.Should().Be(0m);
        totalExpense.Should().Be(0m);
    }

    #endregion
}
