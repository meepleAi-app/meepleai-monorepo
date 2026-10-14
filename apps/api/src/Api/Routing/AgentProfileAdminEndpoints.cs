using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentProfileAdmin;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentProfileAdmin;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// La pagina admin «Agente» (ADR-095 D8, #4169 6c): leggere il profilo dell'agente, salvarne la bozza,
/// pubblicarla, tornare a una versione archiviata. Solo admin.
/// </summary>
internal static class AgentProfileAdminEndpoints
{
    public static RouteGroupBuilder MapAgentProfileAdminEndpoints(this RouteGroupBuilder group)
    {
        var profile = group.MapGroup("/admin/agent-profile")
            .WithTags("Admin - Agent profile");

        // GET /api/v1/admin/agent-profile
        profile.MapGet("/", HandleGet)
            .WithName("GetAgentProfile")
            .WithSummary("Get the agent profile")
            .WithDescription("The published version, the draft if any, and the version history.")
            .Produces<AgentProfileAdminDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/agent-profile/draft
        profile.MapPut("/draft", HandleSaveDraft)
            .WithName("SaveAgentProfileDraft")
            .WithSummary("Save the agent profile draft")
            .WithDescription("Creates the draft, or updates it, from the editable sections: identity, model, generation, prompts. Retrieval, sources and the session assistant are kept as they are.")
            .Produces<AgentProfileAdminDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/agent-profile/draft/publish
        profile.MapPost("/draft/publish", HandlePublish)
            .WithName("PublishAgentProfileDraft")
            .WithSummary("Publish the agent profile draft")
            .WithDescription("Publishes the draft and archives the published version. Refused with 409 when a model has no price in the model catalog.")
            .Produces<AgentProfileAdminDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        // POST /api/v1/admin/agent-profile/versions/{versionNumber}/rollback
        profile.MapPost("/versions/{versionNumber:int}/rollback", HandleRollback)
            .WithName("RollbackAgentProfile")
            .WithSummary("Roll the agent profile back to an archived version")
            .WithDescription("Publishes the content of an archived version as a new version.")
            .Produces<AgentProfileAdminDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> HandleGet(HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        return Results.Ok(await mediator.Send(new GetAgentProfileAdminQuery(), ct).ConfigureAwait(false));
    }

    private static async Task<IResult> HandleSaveDraft(
        [FromBody] SaveAgentProfileDraftRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new SaveAgentProfileDraftCommand(
            request.Name,
            request.Description,
            request.Persona,
            request.PrimaryModelId,
            request.FallbackModelId,
            request.Temperature,
            request.MaxResponseTokens,
            request.CitationStyle,
            request.SystemPrompts ?? new Dictionary<string, string>(StringComparer.Ordinal),
            request.FallbackLanguage);
        return Results.Ok(await mediator.Send(command, ct).ConfigureAwait(false));
    }

    private static async Task<IResult> HandlePublish(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new PublishAgentProfileDraftCommand(), ct).ConfigureAwait(false);
        logger.LogInformation(
            "Admin {AdminId} published agent profile version {VersionNumber}",
            session!.Principal!.Subject.Id,
            result.Published.VersionNumber);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRollback(
        int versionNumber,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new RollbackAgentProfileCommand(versionNumber), ct).ConfigureAwait(false);
        logger.LogInformation(
            "Admin {AdminId} rolled the agent profile back to version {RolledBackTo}, published as version {VersionNumber}",
            session!.Principal!.Subject.Id,
            versionNumber,
            result.Published.VersionNumber);
        return Results.Ok(result);
    }
}

/// <summary>
/// Il corpo di <c>PUT /admin/agent-profile/draft</c>: solo le sezioni che la pagina modifica. Un campo
/// in più nel JSON, come la soglia o le categorie, non ha dove finire e si ignora.
/// </summary>
internal sealed record SaveAgentProfileDraftRequest(
    string Name,
    string? Description,
    string? Persona,
    string PrimaryModelId,
    string? FallbackModelId,
    decimal? Temperature,
    int? MaxResponseTokens,
    CitationStyle? CitationStyle,
    IReadOnlyDictionary<string, string>? SystemPrompts,
    string FallbackLanguage);
