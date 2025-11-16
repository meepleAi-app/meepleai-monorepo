using System;
using MediatR;
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

        // Create a no-op mediator for design-time operations (migrations)
        var mediator = new NoOpMediator();
        return new MeepleAiDbContext(optionsBuilder.Options, mediator);
    }

    /// <summary>
    /// No-op mediator for design-time DbContext creation.
    /// Domain events are not dispatched during migrations.
    /// </summary>
    private class NoOpMediator : IMediator
    {
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Stream not supported in design-time context");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Stream not supported in design-time context");
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            // No-op: don't dispatch events during migrations
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            // No-op: don't dispatch events during migrations
            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Send not supported in design-time context");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            throw new NotImplementedException("Send not supported in design-time context");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Send not supported in design-time context");
        }
    }
}
