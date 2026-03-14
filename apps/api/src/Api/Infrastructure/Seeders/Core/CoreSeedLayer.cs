using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Core seed layer: admin user, AI models.
/// Test users, E2E users, and agent definitions are Dev-only.
/// Runs in all profiles (Prod, Staging, Dev).
/// </summary>
internal sealed class CoreSeedLayer : ISeedLayer
{
    public string Name => "Core";
    public SeedProfile MinimumProfile => SeedProfile.Prod;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        var mediator = context.Services.GetRequiredService<IMediator>();

        context.Logger.LogInformation("[Core] Seeding admin user...");
        await mediator.Send(new SeedAdminUserCommand(), cancellationToken).ConfigureAwait(false);

        context.Logger.LogInformation("[Core] Seeding AI models...");
        await mediator.Send(new SeedAiModelsCommand(), cancellationToken).ConfigureAwait(false);

        // Test users, E2E users, and agent definitions are Dev-only
        if (context.Profile >= SeedProfile.Dev)
        {
            context.Logger.LogInformation("[Core] Seeding test user (Dev only)...");
            await mediator.Send(new SeedTestUserCommand(), cancellationToken).ConfigureAwait(false);

            context.Logger.LogInformation("[Core] Seeding E2E test users (Dev only)...");
            await mediator.Send(new SeedE2ETestUsersCommand(), cancellationToken).ConfigureAwait(false);

            context.Logger.LogInformation("[Core] Seeding agent definitions (Dev only)...");
            await mediator.Send(new SeedAgentDefinitionsCommand(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            context.Logger.LogInformation("[Core] Skipping test/E2E users and agent definitions (profile: {Profile})", context.Profile);
        }
    }
}
