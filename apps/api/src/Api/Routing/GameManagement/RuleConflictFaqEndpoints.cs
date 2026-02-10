using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.RuleConflictFAQs;
using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing.GameManagement;

/// <summary>
/// API endpoints for RuleConflictFAQ management.
/// Issue #3966: REST API for conflict FAQ CRUD operations.
/// </summary>
internal static class RuleConflictFaqEndpoints
{
    public static RouteGroupBuilder MapRuleConflictFaqEndpoints(this RouteGroupBuilder group)
    {
        var faqGroup = group.MapGroup("/games/{gameId}/rule-conflict-faqs")
            .WithTags("RuleConflictFAQs");

        // Commands (Admin/Editor)
        faqGroup.MapPost("", HandleCreateFaq)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Editor"))
            .Produces<RuleConflictFaqDto>(201)
            .Produces(400)
            .Produces(404)
            .Produces(409)
            .Produces(401)
            .Produces(403)
            .WithSummary("Create conflict FAQ entry")
            .WithDescription("Creates a new FAQ resolution for a specific rule conflict pattern. Admin/Editor only.");

        faqGroup.MapPut("{id}", HandleUpdateFaqResolution)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Editor"))
            .Produces<RuleConflictFaqDto>(200)
            .Produces(400)
            .Produces(404)
            .Produces(401)
            .Produces(403)
            .WithSummary("Update FAQ resolution")
            .WithDescription("Updates the resolution text of an existing FAQ entry. Admin/Editor only.");

        faqGroup.MapDelete("{id}", HandleDeleteFaq)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .Produces(204)
            .Produces(404)
            .Produces(401)
            .Produces(403)
            .WithSummary("Delete conflict FAQ")
            .WithDescription("Permanently deletes a FAQ entry. Admin only.");

        // Queries (Public)
        faqGroup.MapGet("", HandleGetAllFaqs)
            .AllowAnonymous()
            .Produces<PagedResult<RuleConflictFaqDto>>(200)
            .Produces(400)
            .WithSummary("List all FAQs for game")
            .WithDescription("Retrieves all FAQ entries for a game, ordered by usage count (most used first). Public readonly access.");

        faqGroup.MapGet("pattern/{pattern}", HandleGetFaqByPattern)
            .AllowAnonymous()
            .Produces<RuleConflictFaqDto>(200)
            .Produces(404)
            .WithSummary("Get FAQ by pattern")
            .WithDescription("Finds FAQ resolution for a specific conflict pattern. Public readonly access.");

        return group;
    }

    private static async Task<IResult> HandleCreateFaq(
        [FromRoute] Guid gameId,
        [FromBody] CreateRuleConflictFaqDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateRuleConflictFaqCommand(
            gameId,
            dto.ConflictType,
            dto.Pattern,
            dto.Resolution,
            dto.Priority);

        var faqId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        // Fetch created FAQ for response
        var query = new GetRuleConflictFaqByPatternQuery(gameId, dto.Pattern);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/games/{gameId}/rule-conflict-faqs/{faqId}", result);
    }

    private static async Task<IResult> HandleUpdateFaqResolution(
        [FromRoute] Guid gameId,
        [FromRoute] Guid id,
        [FromBody] UpdateRuleConflictFaqResolutionDto dto,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRuleConflictFaqResolutionCommand(id, dto.Resolution);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        // Fetch updated FAQ for response
        var query = new GetRuleConflictFaqByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteFaq(
        [FromRoute] Guid gameId,
        [FromRoute] Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteRuleConflictFaqCommand(id);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleGetAllFaqs(
        [FromRoute] Guid gameId,
        IMediator mediator,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllRuleConflictFaqsForGameQuery(gameId, page, pageSize);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetFaqByPattern(
        [FromRoute] Guid gameId,
        [FromRoute] string pattern,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetRuleConflictFaqByPatternQuery(gameId, pattern);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            return Results.NotFound(new { message = $"No FAQ found for pattern '{pattern}'" });
        }

        return Results.Ok(result);
    }
}
