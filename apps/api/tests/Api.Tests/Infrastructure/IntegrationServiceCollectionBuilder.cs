using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Pgvector.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Shared ServiceCollection builder for integration tests that don't need the HTTP pipeline.
/// Provides the base DI setup (DbContext, MediatR, UnitOfWork) plus stubs for services
/// that would otherwise cause DI failures when domain events are dispatched during SaveChangesAsync.
///
/// Usage:
///   var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
///   services.AddScoped&lt;IMyRepository, MyRepository&gt;(); // test-specific
///   _serviceProvider = services.BuildServiceProvider();
/// </summary>
internal static class IntegrationServiceCollectionBuilder
{
    /// <summary>
    /// Creates a ServiceCollection with all base services needed for integration tests.
    /// Includes MediatR (full assembly scan) with stubs for event handler dependencies
    /// that aren't registered in the minimal test DI container.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string for the isolated test database.</param>
    /// <returns>A ServiceCollection ready for test-specific repository registrations.</returns>
    public static ServiceCollection CreateBase(string connectionString)
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // DbContext with pgvector support
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Core infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR — registers ALL handlers from the assembly including event handlers
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Stub services for event handlers that fire during SaveChangesAsync.
        // Without these, MediatR fails to resolve handler dependencies when domain events are dispatched.
        // These are the 6 services that cause the most DI failures across the 66 test files:
        services.AddSingleton(_ =>
            Mock.Of<Api.BoundedContexts.Administration.Domain.Services.IDashboardStreamService>());
        services.AddScoped(_ =>
            Mock.Of<Api.BoundedContexts.UserNotifications.Application.Services.INotificationDispatcher>());
        services.AddScoped(_ =>
            Mock.Of<Api.BoundedContexts.GameManagement.Domain.Repositories.IGameSessionRepository>());
        services.AddScoped(_ =>
            Mock.Of<Api.Services.IHybridCacheService>());
        services.AddScoped(_ =>
            Mock.Of<Api.Services.IEmbeddingService>());
        services.AddScoped(_ =>
            Mock.Of<Api.Services.IEmailService>());
        services.AddScoped(_ =>
            Mock.Of<Api.BoundedContexts.UserLibrary.Domain.Repositories.IPrivateGameRepository>());
        services.AddScoped(_ =>
            Mock.Of<Api.SharedKernel.Services.ITierEnforcementService>());

        // TimeProvider — required by handlers like SubmitValidationFeedbackCommandHandler
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
