using Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries.Tier;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for tier definition CRUD operations.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
internal static class AdminTierEndpoints
{
    public static RouteGroupBuilder MapAdminTierEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/admin/tiers
        group.MapGet("/admin/tiers", HandleGetAllTiers)
            .WithName("GetAllTierDefinitions")
            .WithTags("Admin", "Tiers")
            .WithSummary("Get all tier definitions")
            .WithDescription("Retrieves all tier definitions with their resource limits")
            .Produces<IReadOnlyList<TierDefinitionDto>>();

        // GET /api/v1/admin/tiers/{name}
        group.MapGet("/admin/tiers/{name}", HandleGetTierByName)
            .WithName("GetTierDefinitionByName")
            .WithTags("Admin", "Tiers")
            .WithSummary("Get a tier definition by name")
            .WithDescription("Retrieves a single tier definition by its normalized name")
            .Produces<TierDefinitionDto>()
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/tiers
        group.MapPost("/admin/tiers", HandleCreateTier)
            .WithName("CreateTierDefinition")
            .WithTags("Admin", "Tiers")
            .WithSummary("Create a new tier definition")
            .WithDescription("Creates a new tier with resource limits. Name must be unique.")
            .Produces<TierDefinitionDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        // PUT /api/v1/admin/tiers/{name}
        group.MapPut("/admin/tiers/{name}", HandleUpdateTier)
            .WithName("UpdateTierDefinition")
            .WithTags("Admin", "Tiers")
            .WithSummary("Update an existing tier definition")
            .WithDescription("Updates limits, LLM model tier, or default status of a tier. Only provided fields are updated.")
            .Produces<TierDefinitionDto>()
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleGetAllTiers(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new GetAllTierDefinitionsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTierByName(
        string name,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new GetTierDefinitionByNameQuery(name), ct).ConfigureAwait(false);
        return result is null
            ? Results.NotFound(new { message = $"Tier '{name}' not found" })
            : Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateTier(
        CreateTierRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation(
            "Admin {UserId} creating tier definition '{TierName}'",
            userId, request.Name);

        try
        {
            var command = new CreateTierDefinitionCommand(
                request.Name, request.DisplayName, request.Limits,
                request.LlmModelTier, request.IsDefault);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Admin {UserId} successfully created tier '{TierName}'",
                userId, request.Name);

            return Results.Created($"/api/v1/admin/tiers/{result.Name}", result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleUpdateTier(
        string name,
        UpdateTierRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        logger.LogInformation(
            "Admin {UserId} updating tier definition '{TierName}'",
            userId, name);

        try
        {
            var command = new UpdateTierDefinitionCommand(
                name, request.DisplayName, request.Limits,
                request.LlmModelTier, request.IsDefault);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Admin {UserId} successfully updated tier '{TierName}'",
                userId, name);

            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { message = $"Tier '{name}' not found" });
        }
    }

    /// <summary>
    /// Request DTO for creating a tier definition.
    /// </summary>
    private sealed record CreateTierRequest(
        string Name,
        string DisplayName,
        TierLimitsDto Limits,
        string LlmModelTier,
        bool IsDefault = false);

    /// <summary>
    /// Request DTO for updating a tier definition.
    /// </summary>
    private sealed record UpdateTierRequest(
        string? DisplayName,
        TierLimitsDto? Limits,
        string? LlmModelTier,
        bool? IsDefault);
}
