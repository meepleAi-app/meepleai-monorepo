using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Seeds test user and E2E test users via MediatR commands.
/// E2E users are always seeded (idempotent); test user only on first run.
/// </summary>
internal static class TestUserSeeder
{
    public static async Task SeedAsync(IMediator mediator, ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("Seeding test user...");
        await mediator.Send(
            new Api.BoundedContexts.Administration.Application.Commands.SeedTestUserCommand(), ct)
            .ConfigureAwait(false);

        logger.LogInformation("Seeding E2E test users...");
        await mediator.Send(
            new Api.BoundedContexts.Administration.Application.Commands.SeedE2ETestUsersCommand(), ct)
            .ConfigureAwait(false);
    }
}
