using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.BoundedContexts.UserLibrary.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection extensions for UserLibrary bounded context.
/// </summary>
internal static class UserLibraryServiceExtensions
{
    /// <summary>
    /// Registers all UserLibrary bounded context services.
    /// </summary>
    public static IServiceCollection AddUserLibraryContext(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IUserLibraryRepository, UserLibraryRepository>();
        services.AddScoped<ILibraryShareLinkRepository, LibraryShareLinkRepository>();
        services.AddScoped<IGameLabelRepository, GameLabelRepository>();
        services.AddScoped<IPrivateGameRepository, PrivateGameRepository>(); // Issue #3662: Private games
        services.AddScoped<IProposalMigrationRepository, ProposalMigrationRepository>(); // Issue #3666: Migration choice flow
        services.AddScoped<IWishlistRepository, WishlistRepository>(); // Issue #3917: Wishlist management
        services.AddScoped<IUserCollectionRepository, UserCollectionRepository>(); // Issue #4263: Generic user collections

        // Register domain services
        services.AddScoped<IGameLibraryQuotaService, GameLibraryQuotaService>();

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
