using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Infrastructure;

/// <summary>
/// Design-time factory for creating MeepleAiDbContext instances.
/// Used by EF Core migrations and tooling.
/// </summary>
public class MeepleAiDbContextFactory : IDesignTimeDbContextFactory<MeepleAiDbContext>
{
    /// <summary>
    /// Creates a new instance of MeepleAiDbContext for design-time operations.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A configured MeepleAiDbContext instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the database connection string is not configured via
    /// CONNECTIONSTRINGS__POSTGRES or POSTGRES_CONNECTION_STRING environment variable.
    /// </exception>
    public MeepleAiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Database connection string not configured. " +
                "Set CONNECTIONSTRINGS__POSTGRES or POSTGRES_CONNECTION_STRING environment variable.");

        optionsBuilder.UseNpgsql(connectionString);

        return new MeepleAiDbContext(optionsBuilder.Options);
    }
}
