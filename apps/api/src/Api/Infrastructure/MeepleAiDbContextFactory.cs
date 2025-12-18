using System;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Infrastructure;

/// <summary>
/// Design-time factory for creating MeepleAiDbContext instances.
/// Used by EF Core migrations and tooling.
/// </summary>
internal class MeepleAiDbContextFactory : IDesignTimeDbContextFactory<MeepleAiDbContext>
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

        // Issue #921: Allow dummy connection for migrations without real DB
        // Issue #2112: Try both uppercase (Windows) and camelCase (Linux CI) for env var
        var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")  // Linux CI compatibility
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Database=meepleai_migrations;Username=postgres;Password=postgres"; // Dummy for migrations

        optionsBuilder.UseNpgsql(connectionString);

        // Create no-op dependencies for design-time operations (migrations)
        var mediator = new NoOpMediator();
        var eventCollector = new NoOpEventCollector();
        return new MeepleAiDbContext(optionsBuilder.Options, mediator, eventCollector);
    }

    /// <summary>
    /// No-op mediator for design-time DbContext creation.
    /// Domain events are not dispatched during migrations.
    /// </summary>
    private sealed class NoOpMediator : IMediator
    {
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            // MA0025: NotSupportedException for intentionally unsupported operations
            throw new NotSupportedException("Stream not supported in design-time context");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Stream not supported in design-time context");
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
            throw new NotSupportedException("Send not supported in design-time context");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            throw new NotSupportedException("Send not supported in design-time context");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Send not supported in design-time context");
        }
    }

    /// <summary>
    /// No-op event collector for design-time DbContext creation.
    /// Domain events are not collected during migrations.
    /// </summary>
    private sealed class NoOpEventCollector : IDomainEventCollector
    {
        public void CollectEventsFrom(IAggregateRoot aggregate)
        {
            // No-op: don't collect events during migrations
        }

        public IReadOnlyList<IDomainEvent> GetAndClearEvents()
        {
            return Array.Empty<IDomainEvent>();
        }
    }
}
