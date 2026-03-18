using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
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
        services.AddScoped<IGameSuggestionRepository, GameSuggestionRepository>(); // Admin Invitation Flow: game suggestions

        // Register domain services
        services.AddScoped<IGameLibraryQuotaService, GameLibraryQuotaService>();

        // Register event handlers (consumed by background services via DI scope)
        services.AddScoped<GamePreAddedHandler>(); // Admin Invitation Flow: pre-add games to library
        services.AddScoped<GameSuggestedHandler>(); // Admin Invitation Flow: create game suggestions

        // MediatR handlers are auto-registered via assembly scanning in Program.cs

        return services;
    }
}
