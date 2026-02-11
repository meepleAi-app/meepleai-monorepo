using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.DependencyInjection;

/// <summary>
/// DI registration for BusinessSimulations bounded context.
/// Issue #3720: Financial Ledger Data Model (Epic #3688)
/// </summary>
internal static class BusinessSimulationsServiceExtensions
{
    public static IServiceCollection AddBusinessSimulationsContext(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();

        return services;
    }
}
