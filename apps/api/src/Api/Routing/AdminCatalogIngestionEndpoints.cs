using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for bulk catalog ingestion (Excel import, enrichment, export).
/// Issue: Admin Bulk Excel Import.
/// </summary>
internal static class AdminCatalogIngestionEndpoints
{
    public static RouteGroupBuilder MapAdminCatalogIngestionEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/v1/admin/catalog-ingestion/excel-import
        // Upload an Excel file to create skeleton SharedGame entries
        group.MapPost("/excel-import", async (
            IFormFile file,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new ImportGamesFromExcelCommand(file, session!.User!.Id), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .RequireRateLimiting("BulkImportAdmin")
        .WithName("ExcelImport")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Import games from Excel file";
            operation.Description = "Parses .xlsx file, creates skeleton SharedGame entries. Deduplicates by title and BggId.";
            return operation;
        });

        return group;
    }
}
