using Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;
using Api.BoundedContexts.Administration.Application.Queries.StagingAllowlist;
using Api.Extensions;
using MediatR;

namespace Api.Routing.Admin;

/// <summary>
/// Admin endpoints for managing the staging email allowlist (#845).
/// Superadmin only. Routes under <c>/api/v1/admin/staging-allowlist</c>.
/// </summary>
/// <remarks>
/// All endpoints use the surrogate <c>Guid Id</c> rather than email as the route key —
/// avoids URL-encoding hazards with email special chars (<c>+</c>, <c>.</c>, <c>@</c>)
/// and prevents email leakage into access logs.
/// </remarks>
internal static class AdminStagingAllowlistEndpoints
{
    public static RouteGroupBuilder MapAdminStagingAllowlistEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/staging-allowlist", HandleGetAllowlist)
            .WithName("GetStagingAllowlist")
            .WithTags("Admin", "DevOps");

        group.MapPost("/admin/staging-allowlist", HandleAddEntry)
            .WithName("AddStagingAllowlistEntry")
            .WithTags("Admin", "DevOps");

        group.MapDelete("/admin/staging-allowlist/{id:guid}", HandleRemoveEntry)
            .WithName("RemoveStagingAllowlistEntry")
            .WithTags("Admin", "DevOps");

        return group;
    }

    private static async Task<IResult> HandleGetAllowlist(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireSuperAdminSession();
        if (!authorized)
        {
            return error!;
        }

        var entries = await mediator.Send(new GetStagingAllowlistQuery(), ct).ConfigureAwait(false);
        return Results.Ok(entries);
    }

    private static async Task<IResult> HandleAddEntry(
        AddStagingAllowlistEntryRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireSuperAdminSession();
        if (!authorized)
        {
            return error!;
        }

        var command = new AddStagingAllowlistEntryCommand(
            Email: request.Email,
            Note: request.Note,
            AddedByUserId: session!.Principal!.Subject.Id);

        var dto = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/admin/staging-allowlist/{dto.Id}", dto);
    }

    private static async Task<IResult> HandleRemoveEntry(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireSuperAdminSession();
        if (!authorized)
        {
            return error!;
        }

        await mediator.Send(
            new RemoveStagingAllowlistEntryCommand(id, session!.Principal!.Subject.Id),
            ct).ConfigureAwait(false);

        return Results.NoContent();
    }
}

/// <summary>Request body for <c>POST /api/v1/admin/staging-allowlist</c>.</summary>
internal sealed record AddStagingAllowlistEntryRequest(string Email, string? Note);
