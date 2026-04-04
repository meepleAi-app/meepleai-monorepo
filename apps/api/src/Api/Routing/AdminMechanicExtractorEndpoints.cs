using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.Filters;
using Api.Middleware;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for the Mechanic Extractor (Variant C workflow).
/// Allows admins to extract game mechanics from rulebook PDFs
/// using a human+AI assistant workflow that ensures copyright compliance.
/// </summary>
internal static class AdminMechanicExtractorEndpoints
{
    public static void MapAdminMechanicExtractorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/mechanic-extractor")
            .WithTags("Admin - Mechanic Extractor")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/mechanic-extractor/draft?sharedGameId=...&pdfDocumentId=...
        // Load existing draft for a game+PDF pair
        group.MapGet("/draft", async (
            Guid sharedGameId,
            Guid pdfDocumentId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetMechanicDraftQuery(sharedGameId, pdfDocumentId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result is not null
                ? Results.Ok(result)
                : Results.NotFound();
        })
        .WithName("AdminGetMechanicDraft")
        .WithSummary("Load existing mechanic draft for game+PDF")
        .WithDescription("Retrieves the current work-in-progress mechanic extraction draft");

        // POST /api/v1/admin/mechanic-extractor/draft
        // Create or update a mechanic draft (auto-save)
        group.MapPost("/draft", async (
            SaveMechanicDraftRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin saving mechanic draft for game {SharedGameId}",
                request.SharedGameId);

            var command = new SaveMechanicDraftCommand(
                request.SharedGameId,
                request.PdfDocumentId,
                request.GameTitle,
                request.UserId,
                request.SummaryNotes,
                request.MechanicsNotes,
                request.VictoryNotes,
                request.ResourcesNotes,
                request.PhasesNotes,
                request.QuestionsNotes);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminSaveMechanicDraft")
        .WithSummary("Create or update mechanic draft (auto-save)")
        .WithDescription("Saves the current state of the mechanic extraction draft");

        // POST /api/v1/admin/mechanic-extractor/ai-assist
        // Request AI assistance for a specific section
        group.MapPost("/ai-assist", async (
            AiAssistRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin requesting AI assist for draft {DraftId}, section {Section}",
                request.DraftId,
                LogValueSanitizer.Sanitize(request.Section));

            var command = new AiAssistMechanicDraftCommand(
                request.DraftId,
                request.Section,
                request.HumanNotes,
                request.GameTitle);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminAiAssistMechanicDraft")
        .WithSummary("Request AI assistance for a section")
        .WithDescription("AI generates original text from human notes only (Variant C copyright firewall)");

        // POST /api/v1/admin/mechanic-extractor/accept-draft
        // Accept an AI-generated draft for a specific section
        group.MapPost("/accept-draft", async (
            AcceptDraftRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin accepting draft for {DraftId}, section {Section}",
                request.DraftId,
                LogValueSanitizer.Sanitize(request.Section));

            var command = new AcceptMechanicDraftCommand(
                request.DraftId,
                request.Section,
                request.AcceptedDraft);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminAcceptMechanicDraft")
        .WithSummary("Accept AI-generated draft for a section")
        .WithDescription("Stores the accepted AI draft for the specified section");

        // POST /api/v1/admin/mechanic-extractor/finalize
        // Finalize draft into RulebookAnalysis
        group.MapPost("/finalize", async (
            FinalizeRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Admin finalizing mechanic draft {DraftId}",
                request.DraftId);

            var command = new FinalizeMechanicAnalysisCommand(
                request.DraftId,
                request.UserId);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/admin/mechanic-extractor/analysis/{result.Id}", result);
        })
        .WithName("AdminFinalizeMechanicAnalysis")
        .WithSummary("Finalize draft into RulebookAnalysis")
        .WithDescription("Converts the completed draft into an active RulebookAnalysis entry");
    }
}

// Request DTOs for endpoint binding
internal record SaveMechanicDraftRequest(
    Guid SharedGameId,
    Guid PdfDocumentId,
    string GameTitle,
    Guid UserId,
    string SummaryNotes,
    string MechanicsNotes,
    string VictoryNotes,
    string ResourcesNotes,
    string PhasesNotes,
    string QuestionsNotes);

internal record AiAssistRequest(
    Guid DraftId,
    string Section,
    string HumanNotes,
    string GameTitle);

internal record AcceptDraftRequest(
    Guid DraftId,
    string Section,
    string AcceptedDraft);

internal record FinalizeRequest(
    Guid DraftId,
    Guid UserId);
