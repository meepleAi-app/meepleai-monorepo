using Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.CustomPipeline;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing endpoints for RAG pipeline CRUD.
/// Issue #5312: Pipeline save endpoint + frontend wiring.
/// </summary>
internal static class RagPipelineEndpoints
{
    public static RouteGroupBuilder MapRagPipelineEndpoints(this RouteGroupBuilder group)
    {
        var pipelineGroup = group.MapGroup("/rag/pipelines")
            .WithTags("RAG Pipelines");

        // GET /api/v1/rag/pipelines - List user pipelines
        pipelineGroup.MapGet("/", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.TryGetActiveSession();
            if (!authorized) return error!;

            var query = new ListUserPipelinesQuery(session!.User!.Id);
            var pipelines = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(pipelines);
        })
        .WithName("ListUserPipelines")
        .WithSummary("List user's saved pipelines")
        .Produces<IReadOnlyList<CustomPipelineData>>()
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/v1/rag/pipelines/{id} - Get pipeline by ID
        pipelineGroup.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.TryGetActiveSession();
            if (!authorized) return error!;

            var query = new GetCustomPipelineQuery(id);
            var pipeline = await mediator.Send(query, ct).ConfigureAwait(false);

            return pipeline is not null
                ? Results.Ok(pipeline)
                : Results.NotFound(new { message = "Pipeline not found" });
        })
        .WithName("GetPipelineById")
        .WithSummary("Get a pipeline by ID")
        .Produces<CustomPipelineData>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/v1/rag/pipelines - Save new pipeline
        pipelineGroup.MapPost("/", async (
            [FromBody] SavePipelineRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.TryGetActiveSession();
            if (!authorized) return error!;

            var command = new SaveCustomPipelineCommand
            {
                Name = request.Name,
                Description = request.Description,
                Pipeline = request.Pipeline,
                UserId = session!.User!.Id,
                IsPublished = false,
                Tags = request.Tags ?? [],
            };

            var pipelineId = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Created($"/api/v1/rag/pipelines/{pipelineId}", new { id = pipelineId });
        })
        .WithName("SavePipeline")
        .WithSummary("Save a new pipeline definition")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // DELETE /api/v1/rag/pipelines/{id} - Delete pipeline
        pipelineGroup.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.TryGetActiveSession();
            if (!authorized) return error!;

            var command = new DeleteCustomPipelineCommand(id, session!.User!.Id);
            var deleted = await mediator.Send(command, ct).ConfigureAwait(false);

            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { message = "Pipeline not found" });
        })
        .WithName("DeletePipeline")
        .WithSummary("Delete a pipeline")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        return group;
    }
}

/// <summary>
/// Request DTO for saving a pipeline definition.
/// </summary>
internal sealed record SavePipelineRequest(
    string Name,
    string? Description,
    PipelineDefinition Pipeline,
    string[]? Tags
);
