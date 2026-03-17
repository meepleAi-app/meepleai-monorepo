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

        // Test users, E2E users, and agent definitions disabled — only admin + games in dev seed
        context.Logger.LogInformation("[Core] Skipping test/E2E users and agent definitions (clean seed)");
    }
}
