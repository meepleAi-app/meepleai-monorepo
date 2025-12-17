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
internal static class DbContextHelper
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


}

