using Microsoft.Extensions.DependencyInjection;

namespace Api.Infrastructure.EventBroadcasting;

/// <summary>
/// DI extension that registers the SSE event broadcasting infrastructure introduced
/// by F4.1 issue #1718 (Admin Monitor — Live Event Log).
///
/// <para>
/// Call <see cref="AddEventBroadcasting"/> once from <c>Program.cs</c> (or a bounded-context
/// extension) to wire all three components:
/// <list type="number">
///   <item><see cref="IEventBroadcaster"/> / <see cref="ChannelEventBroadcaster"/> — singleton
///         fan-out hub.</item>
///   <item><see cref="DomainEventBroadcastInterceptor"/> — scoped EF Core
///         <c>SaveChangesInterceptor</c> that bridges committed
///         <c>DomainEventLogEntity</c> rows to the broadcaster.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Interceptor wiring:</b> The <see cref="DomainEventBroadcastInterceptor"/> must be
/// added to <c>DbContextOptionsBuilder.AddInterceptors</c> <em>in addition to</em>
/// <c>AuditingSaveChangesInterceptor</c>. The call site in
/// <c>InfrastructureServiceExtensions.AddDatabaseServices</c> resolves it from the DI
/// container the same way as the audit interceptor — via
/// <c>sp.GetRequiredService&lt;DomainEventBroadcastInterceptor&gt;()</c>.
/// </para>
/// </summary>
internal static class EventBroadcastingServiceExtensions
{
    /// <summary>
    /// Registers the event broadcasting services:
    /// <list type="bullet">
    ///   <item><see cref="IEventBroadcaster"/> as a singleton backed by
    ///         <see cref="ChannelEventBroadcaster"/>.</item>
    ///   <item><see cref="DomainEventBroadcastInterceptor"/> as a scoped
    ///         EF Core interceptor (one per request / DbContext lifetime).</item>
    /// </list>
    ///
    /// <para>
    /// After calling this method, the caller is responsible for adding
    /// <see cref="DomainEventBroadcastInterceptor"/> to the EF Core
    /// <c>DbContextOptionsBuilder</c> so that it intercepts
    /// <c>SaveChangesAsync</c> calls. Example:
    /// <code>
    /// services.AddDbContext&lt;MeepleAiDbContext&gt;((sp, opts) =&gt;
    /// {
    ///     // ...other options...
    ///     opts.AddInterceptors(
    ///         sp.GetRequiredService&lt;AuditingSaveChangesInterceptor&gt;(),
    ///         sp.GetRequiredService&lt;DomainEventBroadcastInterceptor&gt;());
    /// });
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    internal static IServiceCollection AddEventBroadcasting(this IServiceCollection services)
    {
        // Singleton: one fan-out hub shared across all HTTP requests and background services.
        // Thread-safe by design — ChannelEventBroadcaster uses System.Threading.Channels internally.
        services.AddSingleton<IEventBroadcaster, ChannelEventBroadcaster>();

        // Scoped: one interceptor per request / DbContext lifetime.
        // Aligns with DbContext's scoped lifetime so instance-level state (_pendingEntities)
        // is never shared across concurrent requests.
        services.AddScoped<DomainEventBroadcastInterceptor>();

        return services;
    }
}
