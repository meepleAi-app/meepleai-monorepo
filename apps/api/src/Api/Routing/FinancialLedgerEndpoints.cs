using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Financial Ledger CRUD endpoints for admin users (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal static class FinancialLedgerEndpoints
{
    public static RouteGroupBuilder MapFinancialLedgerEndpoints(this RouteGroupBuilder group)
    {
        var ledgerGroup = group.MapGroup("/admin/financial-ledger")
            .WithTags("Admin - Financial Ledger");

        // GET /api/v1/admin/financial-ledger - List entries (paginated + filtered)
        ledgerGroup.MapGet("/", HandleGetLedgerEntries)
            .WithName("GetLedgerEntries")
            .WithSummary("Get paginated and filtered ledger entries")
            .Produces<LedgerEntriesResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/financial-ledger/summary - Get income/expense summary
        ledgerGroup.MapGet("/summary", HandleGetLedgerSummary)
            .WithName("GetLedgerSummary")
            .WithSummary("Get income vs expense summary for a date range")
            .Produces<LedgerSummaryDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/financial-ledger/{id} - Get single entry
        ledgerGroup.MapGet("/{id:guid}", HandleGetLedgerEntryById)
            .WithName("GetLedgerEntryById")
            .WithSummary("Get a single ledger entry by ID")
            .Produces<LedgerEntryDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/financial-ledger - Create manual entry
        ledgerGroup.MapPost("/", HandleCreateManualLedgerEntry)
            .WithName("CreateManualLedgerEntry")
            .WithSummary("Create a new manual ledger entry")
            .Produces<CreateLedgerEntryResponseDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/financial-ledger/{id} - Update entry
        ledgerGroup.MapPut("/{id:guid}", HandleUpdateLedgerEntry)
            .WithName("UpdateLedgerEntry")
            .WithSummary("Update an existing ledger entry (description, category, metadata)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/v1/admin/financial-ledger/{id} - Delete manual entry only
        ledgerGroup.MapDelete("/{id:guid}", HandleDeleteLedgerEntry)
            .WithName("DeleteLedgerEntry")
            .WithSummary("Delete a manual ledger entry (auto entries are protected)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleGetLedgerEntries(
        HttpContext context,
        IMediator mediator,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] LedgerEntryType? type = null,
        [FromQuery] LedgerCategory? category = null,
        [FromQuery] LedgerEntrySource? source = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetLedgerEntriesQuery(
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100),
            type, category, source, dateFrom, dateTo);

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetLedgerSummary(
        HttpContext context,
        IMediator mediator,
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetLedgerSummaryQuery(dateFrom, dateTo);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetLedgerEntryById(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetLedgerEntryByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateManualLedgerEntry(
        CreateLedgerEntryRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation(
            "Admin {AdminId} creating manual ledger entry: {Type} {Amount} {Currency}",
            userId, request.Type, request.Amount, request.Currency);

        var command = new CreateManualLedgerEntryCommand(
            request.Date,
            request.Type,
            request.Category,
            request.Amount,
            request.Currency,
            request.Description,
            userId);

        var entryId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/admin/financial-ledger/{entryId}",
            new CreateLedgerEntryResponseDto(entryId));
    }

    private static async Task<IResult> HandleUpdateLedgerEntry(
        Guid id,
        UpdateLedgerEntryRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new UpdateLedgerEntryCommand(
            id,
            request.Description,
            request.Category,
            request.Metadata);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleDeleteLedgerEntry(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} deleting ledger entry {EntryId}",
            session!.User!.Id, id);

        var command = new DeleteLedgerEntryCommand(id);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}

/// <summary>
/// Request DTO for creating a manual ledger entry
/// </summary>
internal sealed record CreateLedgerEntryRequest(
    DateTime Date,
    LedgerEntryType Type,
    LedgerCategory Category,
    decimal Amount,
    string Currency,
    string? Description);

/// <summary>
/// Request DTO for updating a ledger entry
/// </summary>
internal sealed record UpdateLedgerEntryRequest(
    string? Description,
    LedgerCategory? Category,
    string? Metadata);

/// <summary>
/// Response DTO after creating a ledger entry
/// </summary>
internal sealed record CreateLedgerEntryResponseDto(Guid Id);
