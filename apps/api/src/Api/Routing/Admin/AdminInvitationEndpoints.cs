using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.Commands.RevokeInvitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries.Invitation;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Extensions;
using Api.Infrastructure.Security;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin invitation management endpoints (Issue #124).
/// Covers sending, listing, resending, and revoking invitations.
/// </summary>
internal static class AdminInvitationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapInvitationEndpoints(group);
    }

    // ISSUE-124: Invitation system endpoints
    private static void MapInvitationEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/admin/users/invite", HandleSendInvitation)
            .WithName("SendInvitation")
            .WithTags("Admin")
            .Produces<InvitationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/admin/users/bulk/invite", HandleBulkSendInvitations)
            .WithName("BulkSendInvitations")
            .WithTags("Admin")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<BulkInviteResponse>(StatusCodes.Status200OK);

        group.MapPost("/admin/users/invitations/{id}/resend", HandleResendInvitation)
            .WithName("ResendInvitation")
            .WithTags("Admin")
            .Produces<InvitationDto>(StatusCodes.Status200OK);

        group.MapGet("/admin/users/invitations", HandleGetInvitations)
            .WithName("GetInvitations")
            .WithTags("Admin")
            .Produces<GetInvitationsResponse>(StatusCodes.Status200OK);

        group.MapGet("/admin/users/invitations/stats", HandleGetInvitationStats)
            .WithName("GetInvitationStats")
            .WithTags("Admin")
            .Produces<InvitationStatsResponse>(StatusCodes.Status200OK);

        group.MapDelete("/admin/users/invitations/{id:guid}", HandleRevokeInvitation)
            .WithName("RevokeInvitation")
            .WithTags("Admin")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // ISSUE-124: New invitation flow endpoints (provision + invite in one step)
        group.MapPost("/admin/invitations", HandleProvisionAndInvite)
            .WithName("ProvisionAndInviteUser")
            .WithTags("Admin", "Invitations")
            .WithSummary("Provision a pending user and send invitation email")
            .WithDescription("Creates a user in Pending status and sends an invitation email in one operation. Supports game suggestions and custom messages.")
            .Produces<InvitationDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/admin/invitations", HandleGetPendingInvitations)
            .WithName("GetPendingInvitations")
            .WithTags("Admin", "Invitations")
            .WithSummary("Get invitations with optional status filter")
            .WithDescription("Returns invitations, optionally filtered by status (Pending, Accepted, Expired, Revoked).")
            .Produces<List<InvitationDto>>(StatusCodes.Status200OK);

        group.MapGet("/admin/invitations/{id:guid}", HandleGetInvitationById)
            .WithName("GetInvitationById")
            .WithTags("Admin", "Invitations")
            .WithSummary("Get a single invitation by ID")
            .Produces<InvitationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/admin/invitations/{id:guid}/resend", HandleResendInvitationNew)
            .WithName("ResendInvitationNew")
            .WithTags("Admin", "Invitations")
            .WithSummary("Resend an invitation email")
            .Produces<InvitationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/admin/invitations/{id:guid}", HandleRevokeInvitationNew)
            .WithName("RevokeInvitationNew")
            .WithTags("Admin", "Invitations")
            .WithSummary("Revoke an invitation")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleSendInvitation(
        SendInvitationRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} sending invitation to {Email} with role {Role}",
            session!.User!.Id, DataMasking.MaskEmail(request.Email), request.Role);

        var command = new SendInvitationCommand(request.Email, request.Role, session.User.Id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Invitation {InvitationId} sent to {Email}", result.Id, DataMasking.MaskEmail(request.Email));
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleBulkSendInvitations(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);
        var file = form.Files.GetFile("file");

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "A CSV file is required" });
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var csvContent = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Admin {AdminId} bulk-sending invitations from CSV ({Size} bytes)",
            session!.User!.Id, file.Length);

        var command = new BulkSendInvitationsCommand(csvContent, session.User.Id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Bulk invitation: {SuccessCount} sent, {FailCount} failed",
            result.Successful.Count, result.Failed.Count);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleResendInvitation(
        string id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        if (!Guid.TryParse(id, out var invitationId))
        {
            return Results.BadRequest(new { error = "Invalid invitation ID format" });
        }

        logger.LogInformation("Admin {AdminId} resending invitation {InvitationId}",
            session!.User!.Id, invitationId);

        var command = new ResendInvitationCommand(invitationId, session.User.Id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Invitation {InvitationId} resent successfully", result.Id);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetInvitations(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        string? status = null,
        int page = 1,
        int pageSize = 50)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetInvitationsQuery(status, page, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetInvitationStats(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetInvitationStatsQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRevokeInvitation(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} revoking invitation {InvitationId}",
            session!.User!.Id, id);

        var command = new RevokeInvitationCommand(
            InvitationId: id,
            AdminUserId: session.User.Id
        );

        var success = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!success)
        {
            return Results.NotFound(new { error = "Invitation not found or not pending" });
        }

        logger.LogInformation("Invitation {InvitationId} revoked successfully", id);
        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> HandleProvisionAndInvite(
        ProvisionAndInviteRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} provisioning and inviting {Email} with role {Role}",
            session!.User!.Id, DataMasking.MaskEmail(request.Email), request.Role);

        var command = new ProvisionAndInviteUserCommand(
            Email: request.Email,
            DisplayName: request.DisplayName,
            Role: request.Role,
            Tier: request.Tier ?? "free",
            CustomMessage: request.CustomMessage,
            ExpiresInDays: request.ExpiresInDays ?? 7,
            GameSuggestions: request.GameSuggestions ?? new List<GameSuggestionDto>(),
            InvitedByUserId: session.User.Id);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Invitation {InvitationId} created for {Email} via provision-and-invite",
            result.Id, DataMasking.MaskEmail(request.Email));
        return Results.Created($"/api/v1/admin/invitations/{result.Id}", result);
    }

    private static async Task<IResult> HandleGetPendingInvitations(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        string? status = null)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetPendingInvitationsQuery(status);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetInvitationById(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetInvitationByIdQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleResendInvitationNew(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} resending invitation {InvitationId} (new path)",
            session!.User!.Id, id);

        var command = new ResendInvitationCommand(id, session.User.Id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        logger.LogInformation("Invitation {InvitationId} resent successfully (new path)", result.Id);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRevokeInvitationNew(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation("Admin {AdminId} revoking invitation {InvitationId} (new path)",
            session!.User!.Id, id);

        var command = new RevokeInvitationCommand(
            InvitationId: id,
            AdminUserId: session.User.Id);

        var success = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!success)
        {
            return Results.NotFound(new { error = "Invitation not found or not pending" });
        }

        logger.LogInformation("Invitation {InvitationId} revoked successfully (new path)", id);
        return Results.Ok(new { success = true });
    }
}
