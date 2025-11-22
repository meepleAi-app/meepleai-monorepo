using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests.Helpers;

/// <summary>
/// Helper for creating test DbContext instances with proper configuration.
/// </summary>
public static class DbContextHelper
{
    /// <summary>
    /// Creates an InMemory MeepleAiDbContext for unit testing.
    /// </summary>
    /// <param name="databaseName">Unique database name for isolation (default: random Guid)</param>
    /// <returns>Configured DbContext for testing</returns>
    public static MeepleAiDbContext CreateInMemoryDbContext(string? databaseName = null)
    {
        var dbName = databaseName ?? Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();

        // Setup event collector to return empty list by default
        eventCollectorMock.Setup(x => x.GetAndClearEvents()).Returns(new List<IDomainEvent>().AsReadOnly());

        return new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
    }

    /// <summary>
    /// Creates a mock of MeepleAiDbContext with configurable behavior.
    /// Note: For most tests, prefer CreateInMemoryDbContext() for real EF Core behavior.
    /// WARNING: DbContext cannot be mocked with Moq due to constructor requirements.
    /// This method is deprecated and returns a real in-memory context instead.
    /// </summary>
    [Obsolete("Use CreateInMemoryDbContext() instead. DbContext cannot be mocked with Moq.")]
    public static MeepleAiDbContext CreateMockDbContext()
    {
        // Return a real in-memory context instead of trying to mock
        return CreateInMemoryDbContext();
    }
}
