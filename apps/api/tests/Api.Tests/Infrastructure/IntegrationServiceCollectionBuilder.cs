using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Persistence;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
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
///
/// <para><b>⚠ Domain event dispatch trap (#2389 audit follow-up):</b></para>
/// <para>The default <c>IOptions&lt;DomainEventOutboxOptions&gt;</c> binds
/// <c>Mode = DomainEventDispatchMode.OutboxOnly</c> (steady-state post-T9 cutover, see
/// <c>docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md</c>). In that
/// mode <c>MeepleAiDbContext.SaveChangesAsync</c> persists raised domain events to
/// <c>domain_event_outbox</c> and the <c>DomainEventOutboxProcessor</c> BackgroundService
/// drains them asynchronously — but that processor is NOT registered in this minimal
/// integration setup.</para>
/// <para>ITs that depend on <see cref="MediatR.INotificationHandler{T}"/> handlers firing
/// inline on SaveChangesAsync (spy handlers, broadcast handlers, side-effect handlers) MUST
/// override the options to <see cref="DomainEventDispatchMode.Hybrid"/> after calling
/// <c>CreateBase</c>:
/// <code>
/// services.AddSingleton&lt;IOptions&lt;DomainEventOutboxOptions&gt;&gt;(
///     Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }));
/// </code>
/// Without this override the spy handler silently never fires and assertions read 0.
/// See <c>SessionScoresUpdatedSignalRBroadcastIntegrationTests</c> and
/// <c>FinalizeSessionSingleDispatchIntegrationTests</c> for the canonical pattern.</para>
/// </summary>
internal static class IntegrationServiceCollectionBuilder
{
    /// <summary>
    /// Creates a ServiceCollection with all base services needed for integration tests.
    /// Includes MediatR (full assembly scan) with stubs for event handler dependencies
    /// that aren't registered in the minimal test DI container.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string for the isolated test database.</param>
    /// <param name="useHybridCachePassthrough">
    /// When true, registers <see cref="PassthroughHybridCache"/> instead of <c>Mock.Of&lt;IHybridCacheService&gt;()</c>.
    /// Required for tests that exercise read→write→read of the same cached key in a single run,
    /// because the default Moq mock returns null without invoking the factory and the read
    /// would miss the write. See issue #2162 follow-up.
    /// </param>
    /// <param name="useNoTrackingDefault">
    /// When true, configures <c>UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)</c> on
    /// the test <see cref="MeepleAiDbContext"/>, matching the production default set in
    /// <c>InfrastructureServiceExtensions.cs</c> (PERF-06). By default this builder leaves EF Core's
    /// tracking-by-default behavior in place, which SILENTLY MASKS handler bugs where a query is
    /// missing an explicit <c>.AsTracking()</c> call — mutations on the returned entity would be
    /// saved in a tracking-by-default test DbContext but silently dropped in production. Set this
    /// to true for any test whose whole point is proving persistence of field mutations after a
    /// plain (non-<c>.AsTracking()</c>) query. See issue #3269 follow-up (NoTracking gotcha).
    /// </param>
    /// <returns>A ServiceCollection ready for test-specific repository registrations.</returns>
    public static ServiceCollection CreateBase(
        string connectionString,
        bool useHybridCachePassthrough = false,
        bool useNoTrackingDefault = false)
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // SP5 Admin Security S1 T3: register the audit snapshot sink + interceptor so that
        // AuditLoggingBehavior can resolve ScopedAuditSnapshotSink and the interceptor is wired
        // into the DbContext for all integration tests that exercise [AuditableAction] commands.
        services.AddScoped<ScopedAuditSnapshotSink>();
        services.AddScoped<IAuditSnapshotSink>(sp => sp.GetRequiredService<ScopedAuditSnapshotSink>());
        services.AddScoped<AuditingSaveChangesInterceptor>();

        // DbContext with pgvector support + audit interceptor
        services.AddDbContext<MeepleAiDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, o =>
            {
                o.UseVector();
                // PR2 follow-up to #1684: Testcontainers on Docker Desktop Windows produces
                // transient EndOfStreamException/Npgsql connection drops under load. Without
                // EnableRetryOnFailure, those surface as test failures instead of being retried.
                // Pattern mirrors FrontendSdkTestFactory (PR #1684).
                o.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
            });
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());

            if (useNoTrackingDefault)
            {
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
        });

        // Core infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // MediatR — registers ALL handlers from the assembly including event handlers.
        // Issue #1534: the open-generic DomainEventAuditHandler<TEvent> is also auto-registered
        // by RegisterServicesFromAssembly (MediatR maps open-generic INotificationHandler<>
        // implementations directly). No explicit AddTransient line needed.
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
        if (useHybridCachePassthrough)
        {
            services.AddScoped<Api.Services.IHybridCacheService, PassthroughHybridCache>();
        }
        else
        {
            services.AddScoped(_ =>
                Mock.Of<Api.Services.IHybridCacheService>());
        }
        services.AddScoped(_ =>
            Mock.Of<Api.Services.IEmbeddingService>());
        services.AddScoped(_ =>
            Mock.Of<Api.Services.IEmailService>());
        services.AddScoped(_ =>
            Mock.Of<Api.BoundedContexts.UserLibrary.Domain.Repositories.IPrivateGameRepository>());
        services.AddScoped(_ =>
            Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IAgentDefinitionRepository>());
        // ITierEnforcementService — must be fully set up to avoid NullRef in UploadPdfCommandHandler.
        // Bare Mock.Of<>() returns false for CanPerformAsync, null for GetUsageAsync → NullRef at handler line 97.
        var tierMock = new Mock<ITierEnforcementService>();
        tierMock.Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        tierMock.Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.Unlimited);
        tierMock.Setup(t => t.GetUsageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageSnapshot(0, 100, 0, 100, 0, 100, 0, 100, 0, 100, 0, 100, true, 0, 100));
        services.AddScoped<ITierEnforcementService>(_ => tierMock.Object);

        // TimeProvider — required by handlers like SubmitValidationFeedbackCommandHandler
        services.AddSingleton(TimeProvider.System);

        // AuditService — required by handlers/workers that emit audit log entries
        // (e.g. MechanicRecalcBackgroundService.WriteCompletionAuditAsync — ADR-051 Sprint 2 / Task 12).
        // The service writes to the AuditLogs table which already exists via migrations, so the
        // real implementation is safe and gives us realistic FK/serialization coverage.
        services.AddScoped<Api.Services.AuditService>();

        return services;
    }
}
