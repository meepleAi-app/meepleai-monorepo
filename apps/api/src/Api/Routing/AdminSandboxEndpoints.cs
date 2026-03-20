using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for the RAG Sandbox Dashboard.
/// Provides document listing, deletion, chunk preview, and pipeline metrics.
/// </summary>
internal static class AdminSandboxEndpoints
{
    public static RouteGroupBuilder MapAdminSandboxEndpoints(this RouteGroupBuilder group)
    {
        var sandboxGroup = group.MapGroup("/admin/sandbox")
            .WithTags("Admin", "Sandbox")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/sandbox/shared-games/{gameId}/documents
        // Returns all PDF documents for a shared game
        sandboxGroup.MapGet("/shared-games/{gameId:guid}/documents", HandleGetDocumentsByGame)
            .WithName("GetSandboxDocumentsByGame")
            .WithSummary("Get PDF documents for a shared game (Admin Sandbox)")
            .WithDescription("Returns all PDF documents associated with a shared game for the RAG sandbox dashboard.")
            .Produces<IReadOnlyList<PdfDocumentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/sandbox/pdfs/{id}
        // Admin delete of a PDF document (hard delete with vector cleanup)
        sandboxGroup.MapDelete("/pdfs/{id:guid}", HandleDeletePdf)
            .WithName("DeleteSandboxPdf")
            .WithSummary("Delete a PDF document (Admin Sandbox)")
            .WithDescription("Permanently deletes a PDF document and its associated vectors from Qdrant. Used for admin sandbox cleanup.")
            .Produces<PdfDeleteResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/sandbox/pdfs/{id}/chunks/preview
        // Returns paginated chunk previews for a specific PDF document
        sandboxGroup.MapGet("/pdfs/{id:guid}/chunks/preview", HandleGetChunksPreview)
            .WithName("GetSandboxPdfChunksPreview")
            .WithSummary("Get chunk previews for a PDF document (Admin Sandbox)")
            .WithDescription("Returns paginated text chunk previews from the vector index for a specific PDF document. Supports page, pageSize, and search query parameters.")
            .Produces<PaginatedChunksResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/sandbox/pdfs/{id}/pipeline-metrics
        // Returns pipeline processing timing metrics for a PDF
        sandboxGroup.MapGet("/pdfs/{id:guid}/pipeline-metrics", HandleGetPipelineMetrics)
            .WithName("GetSandboxPdfPipelineMetrics")
            .WithSummary("Get pipeline processing metrics for a PDF document (Admin Sandbox)")
            .WithDescription("Returns per-step timing data for the PDF processing pipeline including upload, extraction, chunking, embedding, and indexing phases.")
            .Produces<PdfPipelineMetricsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/sandbox/apply-config
        // Stores sandbox configuration in Redis with 24h TTL
        sandboxGroup.MapPost("/apply-config", HandleApplyConfig)
            .WithName("ApplySandboxConfig")
            .WithSummary("Apply sandbox configuration for RAG debug chat (Admin Sandbox)")
            .WithDescription("Stores sandbox RAG pipeline configuration (strategy, weights, model, temperature) in Redis with 24h TTL for use during debug chat sessions.")
            .Produces<ApplySandboxConfigResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<IResult> HandleApplyConfig(
        ApplySandboxConfigRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new ApplySandboxConfigCommand(
            AdminUserId: session!.User!.Id,
            GameId: request.GameId,
            Config: new SandboxConfigOverrideDto(
                Strategy: request.Strategy,
                DenseWeight: request.DenseWeight,
                TopK: request.TopK,
                RerankingEnabled: request.RerankingEnabled,
                Temperature: request.Temperature,
                MaxTokens: request.MaxTokens,
                Model: request.Model,
                SystemPromptOverride: request.SystemPromptOverride,
                ChunkingStrategy: request.ChunkingStrategy,
                ChunkSize: request.ChunkSize,
                ChunkOverlap: request.ChunkOverlap
            ));

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetDocumentsByGame(
        Guid gameId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetPdfDocumentsByGameQuery(gameId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeletePdf(
        Guid id,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var command = new DeletePdfCommand(id.ToString());
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Admin sandbox PDF deletion failed for {PdfId}: {Message}", id, result.Message);
            return Results.NotFound(new { error = "not_found", message = result.Message });
        }

        logger.LogInformation("Admin sandbox deleted PDF {PdfId}", id);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetChunksPreview(
        Guid id,
        int? page,
        int? pageSize,
        string? search,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetPdfChunksPreviewQuery(
            PdfId: id,
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            Search: search);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetPipelineMetrics(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetPdfPipelineMetricsQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        if (result is null)
        {
            return Results.NotFound(new { error = "not_found", message = $"PDF document '{id}' not found" });
        }

        return Results.Ok(result);
    }
}

/// <summary>
/// Request body for applying sandbox configuration.
/// </summary>
internal record ApplySandboxConfigRequest(
    Guid GameId,
    string? Strategy = null,
    double? DenseWeight = null,
    int? TopK = null,
    bool? RerankingEnabled = null,
    double? Temperature = null,
    int? MaxTokens = null,
    string? Model = null,
    string? SystemPromptOverride = null,
    string? ChunkingStrategy = null,
    int? ChunkSize = null,
    int? ChunkOverlap = null
);
