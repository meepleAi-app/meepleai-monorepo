using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Seeds admin user from admin.secret. Fatal if fails.
/// Wraps the existing SeedAdminUserCommand MediatR dispatch.
/// </summary>
internal static class AdminUserSeeder
{
    public static async Task SeedAsync(IMediator mediator, ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("Seeding admin user...");
        await mediator.Send(
            new Api.BoundedContexts.Administration.Application.Commands.SeedAdminUserCommand(), ct)
            .ConfigureAwait(false);
    }
}
