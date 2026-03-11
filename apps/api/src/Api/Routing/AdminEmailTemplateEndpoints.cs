using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for email template management (CRUD, versioning, preview).
/// Issue #56: Admin endpoints for email template management.
/// </summary>
internal static class AdminEmailTemplateEndpoints
{
    public static void MapAdminEmailTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/email-templates")
            .WithTags("Admin - Email Templates")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/", GetEmailTemplates)
            .WithName("GetEmailTemplates")
            .WithSummary("List email templates with optional type and locale filtering");

        group.MapGet("/{id:guid}", GetEmailTemplate)
            .WithName("GetEmailTemplateById")
            .WithSummary("Get a single email template by ID");

        group.MapPost("/", CreateEmailTemplate)
            .WithName("CreateEmailTemplate")
            .WithSummary("Create a new email template");

        group.MapPut("/{id:guid}", UpdateEmailTemplate)
            .WithName("UpdateEmailTemplate")
            .WithSummary("Update an existing email template's content");

        group.MapPost("/{id:guid}/publish", PublishEmailTemplate)
            .WithName("PublishEmailTemplate")
            .WithSummary("Publish (activate) a template version, deactivating others");

        group.MapPost("/{id:guid}/preview", PreviewEmailTemplate)
            .WithName("PreviewEmailTemplate")
            .WithSummary("Preview a rendered template with test placeholder data");

        group.MapGet("/{name}/versions", GetEmailTemplateVersions)
            .WithName("GetEmailTemplateVersions")
            .WithSummary("Get all versions of a template by name");
    }

    private static async Task<IResult> GetEmailTemplates(
        IMediator mediator,
        string? type = null,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetEmailTemplatesQuery(type, locale), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEmailTemplate(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetEmailTemplateByIdQuery(id), cancellationToken).ConfigureAwait(false);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> CreateEmailTemplate(
        CreateEmailTemplateRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var session = context.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        var userId = session!.User!.Id;

        var id = await mediator.Send(
            new CreateEmailTemplateCommand(request.Name, request.Locale, request.Subject, request.HtmlBody, userId),
            cancellationToken).ConfigureAwait(false);

        return Results.Created($"/admin/email-templates/{id}", new { id });
    }

    private static async Task<IResult> UpdateEmailTemplate(
        Guid id,
        UpdateEmailTemplateRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var session = context.Items[nameof(SessionStatusDto)] as SessionStatusDto;
        var userId = session!.User!.Id;

        var result = await mediator.Send(
            new UpdateEmailTemplateCommand(id, request.Subject, request.HtmlBody, userId),
            cancellationToken).ConfigureAwait(false);

        return result ? Results.Ok(new { success = true }) : Results.NotFound();
    }

    private static async Task<IResult> PublishEmailTemplate(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new PublishEmailTemplateCommand(id), cancellationToken).ConfigureAwait(false);
        return result ? Results.Ok(new { success = true }) : Results.NotFound();
    }

    private static async Task<IResult> PreviewEmailTemplate(
        Guid id,
        PreviewEmailTemplateRequest? request,
        IMediator mediator,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new PreviewEmailTemplateCommand(id, request?.TestData),
            cancellationToken).ConfigureAwait(false);

        return !string.IsNullOrEmpty(result)
            ? Results.Ok(new { html = result })
            : Results.NotFound();
    }

    private static async Task<IResult> GetEmailTemplateVersions(
        string name,
        IMediator mediator,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new GetEmailTemplateVersionsQuery(name, locale),
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(result);
    }
}

internal record CreateEmailTemplateRequest(string Name, string Locale, string Subject, string HtmlBody);
internal record UpdateEmailTemplateRequest(string Subject, string HtmlBody);
internal record PreviewEmailTemplateRequest(Dictionary<string, string>? TestData);
