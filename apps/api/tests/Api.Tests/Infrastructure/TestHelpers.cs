using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Helper methods for integration tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a MeepleAiDbContext with migrations applied for integration tests.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <returns>Configured and migrated DbContext</returns>
    public static async Task<MeepleAiDbContext> CreateDbContextAndMigrateAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .EnableSensitiveDataLogging()
            .Options;

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();

        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

        // Ensure migrations are applied
        await dbContext.Database.MigrateAsync(CancellationToken.None);

        return dbContext;
    }
}
