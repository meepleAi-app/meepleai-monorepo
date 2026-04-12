using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Api.DevTools;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> that register services with
/// mock-aware dispatch proxies backed by <see cref="MockAwareProxy{TService}"/>.
/// </summary>
internal static class MockAwareServiceCollectionExtensions
{
    /// <summary>
    /// Registers a service with a mock-aware proxy that dispatches at runtime
    /// between <typeparamref name="TReal"/> and <typeparamref name="TMock"/> based
    /// on <see cref="IMockToggleReader"/> state.
    /// </summary>
    /// <typeparam name="TService">The service interface to expose in the DI container.</typeparam>
    /// <typeparam name="TReal">The real (production) implementation of <typeparamref name="TService"/>.</typeparam>
    /// <typeparam name="TMock">The mock (test/dev) implementation of <typeparamref name="TService"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceName">
    /// The toggle key checked via <see cref="IMockToggleReader.IsMocked"/>.
    /// Typically matches the corresponding entry in <see cref="KnownMockServices.All"/>.
    /// </param>
    /// <param name="lifetime">
    /// The <see cref="ServiceLifetime"/> for all three registrations (proxy, real, mock).
    /// </param>
    /// <returns>The original <paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <b>CRITICAL — lifetime must match the real service:</b> if the existing
    /// <typeparamref name="TService"/> registration is <see cref="ServiceLifetime.Scoped"/>,
    /// pass <see cref="ServiceLifetime.Scoped"/> here. A mismatch (e.g. Singleton wrapping
    /// Scoped) causes captive-dependency bugs: state leaks across requests and the built-in
    /// scope validation throws in Development mode.
    /// </para>
    /// <para>
    /// This method <b>removes</b> any prior <typeparamref name="TService"/> registration so
    /// the proxy becomes the single canonical binding for that interface. Both
    /// <typeparamref name="TReal"/> and <typeparamref name="TMock"/> are registered under
    /// their concrete types to allow the proxy factory to resolve them independently.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddMockAwareService<TService, TReal, TMock>(
        this IServiceCollection services,
        string serviceName,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
        where TReal : class, TService
        where TMock : class, TService
    {
        services.Add(new ServiceDescriptor(typeof(TReal), typeof(TReal), lifetime));
        services.Add(new ServiceDescriptor(typeof(TMock), typeof(TMock), lifetime));

        // Remove any existing TService registration (the real binding we are wrapping)
        // so the proxy becomes the sole canonical registration for TService.
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (existing is not null)
        {
            services.Remove(existing);
        }

        services.Add(new ServiceDescriptor(
            typeof(TService),
            sp => MockAwareProxy<TService>.Create(
                sp.GetRequiredService<TReal>(),
                sp.GetRequiredService<TMock>(),
                sp.GetRequiredService<IMockToggleReader>(),
                serviceName),
            lifetime));

        return services;
    }
}
