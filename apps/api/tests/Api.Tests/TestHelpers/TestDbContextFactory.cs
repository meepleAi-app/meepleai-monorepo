using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Factory for creating test DbContext instances.
/// Solves Issue #2430: Provides properly configured DbContext for unit tests.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a test DbContext with InMemoryDatabase and mocked dependencies.
    /// Use this instead of Mock&lt;MeepleAiDbContext&gt; which fails with "cannot instantiate proxy".
    /// </summary>
    /// <param name="databaseName">Unique database name (defaults to random GUID)</param>
    /// <returns>Configured MeepleAiDbContext ready for testing</returns>
    public static MeepleAiDbContext CreateInMemoryDbContext(string? databaseName = null)
    {
        var dbName = databaseName ?? Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = CreateMockEventCollector();

        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    /// <summary>
    /// Creates a mock IDomainEventCollector that returns empty event list.
    /// Issue #2430: Prevents NullReferenceException in SaveChangesAsync.
    /// </summary>
    public static Mock<IDomainEventCollector> CreateMockEventCollector()
    {
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());
        return mockEventCollector;
    }

    /// <summary>
    /// Creates a mock IMediator for testing.
    /// </summary>
    public static Mock<IMediator> CreateMockMediator()
    {
        return new Mock<IMediator>();
    }
}
