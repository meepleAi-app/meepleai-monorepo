using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.GameManagement.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for GameManagement bounded context.
/// </summary>
public static class GameManagementServiceExtensions
{
    /// <summary>
    /// Registers all GameManagement bounded context services.
    /// </summary>
    public static IServiceCollection AddGameManagementContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddScoped<IGameFAQRepository, GameFAQRepository>(); // ISSUE-2028

        // Register Unit of Work (shared across bounded contexts)
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain services (Issue #1185, #869)
        services.AddScoped<RuleSpecVersioningDomainService>();
        services.AddScoped<RuleAtomParsingDomainService>();
        services.AddScoped<RuleSpecDiffDomainService>();
        services.AddScoped<MoveValidationDomainService>();

        // Issue #2055: Register collaborative editing services
        services.AddScoped<IEditorLockService, EditorLockService>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
