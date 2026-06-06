using Api.BoundedContexts.Testing.Application.Commands;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Api.Routing.Admin;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-4) — Admin endpoint group for E2E entity seeding.
/// Routes under <c>/api/v1/admin/test/seed</c>. Triple-gated:
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>Env <c>E2E_SEEDING_ENABLED=true</c> required.</item>
/// <item><c>ASPNETCORE_ENVIRONMENT != Production</c> (Program.cs startup fail-fast).</item>
/// <item><see cref="RequireAdminSessionFilter"/> group-level (401/403 enforcement).</item>
/// </list>
/// CQRS compliance: all handlers dispatch via <see cref="IMediator"/>; zero
/// direct service injection.
/// </remarks>
internal static class AdminTestSeedEndpoints
{
    public static RouteGroupBuilder MapAdminTestSeedEndpoints(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/game-night", HandleSeedGameNight)
            .WithName("AdminTestSeed_GameNight")
            .WithTags("Admin", "TestSeeding");

        group.MapPost("/session", HandleSeedSession)
            .WithName("AdminTestSeed_Session")
            .WithTags("Admin", "TestSeeding");

        group.MapPost("/player", HandleSeedPlayer)
            .WithName("AdminTestSeed_Player")
            .WithTags("Admin", "TestSeeding");

        group.MapPost("/cleanup", HandleCleanup)
            .WithName("AdminTestSeed_Cleanup")
            .WithTags("Admin", "TestSeeding");

        return group;
    }

    private static async Task<IResult> HandleSeedGameNight(
        SeedTestGameNightCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSeedSession(
        SeedTestSessionCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSeedPlayer(
        SeedTestPlayerCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCleanup(
        CleanupTestEntitiesCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
