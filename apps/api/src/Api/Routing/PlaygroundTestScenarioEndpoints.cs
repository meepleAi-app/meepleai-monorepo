using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.PlaygroundTestScenario;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// CRUD endpoints for PlaygroundTestScenario management.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
internal static class PlaygroundTestScenarioEndpoints
{
    public static void MapPlaygroundTestScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/playground/scenarios");

        // GET /api/v1/admin/playground/scenarios
        group.MapGet("/", async (
            [FromQuery] int? category,
            [FromQuery] Guid? agentDefinitionId,
            [FromQuery] bool? activeOnly,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetPlaygroundTestScenariosQuery(
                Category: category.HasValue ? (ScenarioCategory)category.Value : null,
                AgentDefinitionId: agentDefinitionId,
                ActiveOnly: activeOnly ?? true);

            var results = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(new { success = true, scenarios = results, count = results.Count });
        })
        .RequireAdminSession()
        .WithName("GetPlaygroundTestScenarios")
        .WithTags("Playground", "Admin")
        .WithSummary("Get all playground test scenarios")
        .Produces<object>(200)
        .Produces(500);

        // GET /api/v1/admin/playground/scenarios/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            _ = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var query = new GetPlaygroundTestScenarioByIdQuery(id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result is null
                ? Results.NotFound(new { error = $"Scenario {id} not found" })
                : Results.Ok(result);
        })
        .RequireAdminSession()
        .WithName("GetPlaygroundTestScenarioById")
        .WithTags("Playground", "Admin")
        .WithSummary("Get a playground test scenario by ID")
        .Produces<PlaygroundTestScenarioDto>(200)
        .Produces(404)
        .Produces(500);

        // POST /api/v1/admin/playground/scenarios
        group.MapPost("/", async (
            CreatePlaygroundTestScenarioRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new CreatePlaygroundTestScenarioCommand(
                Name: req.Name,
                Description: req.Description,
                Category: req.Category,
                Messages: req.Messages.Select(m => new ScenarioMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    DelayMs = m.DelayMs
                }).ToList(),
                CreatedBy: session.User!.Id,
                ExpectedOutcome: req.ExpectedOutcome,
                AgentDefinitionId: req.AgentDefinitionId,
                Tags: req.Tags);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Created PlaygroundTestScenario {ScenarioId} by user {UserId}",
                result.Id, session.User!.Id);

            return Results.Created($"/api/v1/admin/playground/scenarios/{result.Id}", result);
        })
        .RequireAdminSession()
        .WithName("CreatePlaygroundTestScenario")
        .WithTags("Playground", "Admin")
        .WithSummary("Create a new playground test scenario")
        .Produces<PlaygroundTestScenarioDto>(201)
        .Produces(400)
        .Produces(500);

        // PUT /api/v1/admin/playground/scenarios/{id}
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdatePlaygroundTestScenarioRequest req,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new UpdatePlaygroundTestScenarioCommand(
                Id: id,
                Name: req.Name,
                Description: req.Description,
                Category: req.Category,
                Messages: req.Messages.Select(m => new ScenarioMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    DelayMs = m.DelayMs
                }).ToList(),
                ExpectedOutcome: req.ExpectedOutcome,
                Tags: req.Tags);

            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Updated PlaygroundTestScenario {ScenarioId} by user {UserId}",
                id, session.User!.Id);

            return Results.NoContent();
        })
        .RequireAdminSession()
        .WithName("UpdatePlaygroundTestScenario")
        .WithTags("Playground", "Admin")
        .WithSummary("Update an existing playground test scenario")
        .Produces(204)
        .Produces(400)
        .Produces(404)
        .Produces(500);

        // DELETE /api/v1/admin/playground/scenarios/{id}
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new DeletePlaygroundTestScenarioCommand(id);
            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Deactivated PlaygroundTestScenario {ScenarioId} by user {UserId}",
                id, session.User!.Id);

            return Results.NoContent();
        })
        .RequireAdminSession()
        .WithName("DeletePlaygroundTestScenario")
        .WithTags("Playground", "Admin")
        .WithSummary("Soft-delete a playground test scenario")
        .Produces(204)
        .Produces(404)
        .Produces(500);
    }
}

// Request DTOs
internal sealed record CreatePlaygroundTestScenarioRequest(
    string Name,
    string Description,
    ScenarioCategory Category,
    List<ScenarioMessageRequest> Messages,
    string? ExpectedOutcome = null,
    Guid? AgentDefinitionId = null,
    List<string>? Tags = null
);

internal sealed record UpdatePlaygroundTestScenarioRequest(
    string Name,
    string Description,
    ScenarioCategory Category,
    List<ScenarioMessageRequest> Messages,
    string? ExpectedOutcome = null,
    List<string>? Tags = null
);

internal sealed record ScenarioMessageRequest(
    string Role,
    string Content,
    int? DelayMs = null
);
