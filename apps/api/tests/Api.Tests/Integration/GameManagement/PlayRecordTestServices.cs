using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// What the PlayRecord command handlers resolve, registered in one place for the test classes
/// that build their container by hand (#4298).
/// <para>
/// Each of those classes used to list the registrations itself. When
/// <c>UpdatePlayRecordCommandHandler</c> gained <c>IPlayRecordVersionRepository</c> (#2437-3)
/// and <c>ILiveSessionRepository</c> (#3146), each list was updated differently or not at all:
/// one class had neither, one had only the first. Their Update tests failed with "Unable to
/// resolve service" before reaching the handler, so nothing about the update was exercised.
/// </para>
/// A new dependency of a PlayRecord handler goes here, once.
/// </summary>
internal static class PlayRecordTestServices
{
    public static IServiceCollection AddPlayRecordHandlerDependencies(
        this IServiceCollection services,
        TimeProvider timeProvider)
    {
        services.AddScoped<IPlayRecordRepository, PlayRecordRepository>();
        services.AddScoped<IPlayRecordVersionRepository, PlayRecordVersionRepository>();
        services.AddScoped<ILiveSessionRepository, LiveSessionRepository>();
        services.AddScoped<PlayRecordPermissionChecker>();
        // #2349: IGameCoreDataProvider is required by CreatePlayRecordCommandHandler. The real
        // SharedGameRepository only needs DbContext + IDomainEventCollector, both provided by
        // IntegrationServiceCollectionBuilder.CreateBase, which also mocks IPrivateGameRepository.
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<IGameCoreDataProvider, GameCoreDataProvider>();
        services.AddSingleton(timeProvider);
        return services;
    }
}
