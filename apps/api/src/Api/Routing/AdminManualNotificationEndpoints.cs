using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Extensions;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for sending manual notifications to users.
/// Supports channel selection (inapp, email) and recipient targeting.
/// </summary>
internal static class AdminManualNotificationEndpoints
{
    public static void MapAdminManualNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/notifications")
            .WithTags("Admin - Manual Notifications")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/send", HandleSendManualNotification)
            .WithName("SendManualNotification")
            .Produces<SendManualNotificationResult>(200)
            .Produces(400)
            .WithSummary("Send a manual notification to selected users via chosen channels");
    }

    private static async Task<IResult> HandleSendManualNotification(
        SendManualNotificationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var adminId = httpContext.User.GetUserId();
        var adminName = httpContext.User.FindFirst("displayName")?.Value
            ?? httpContext.User.FindFirst("email")?.Value
            ?? "Admin";

        var command = new SendManualNotificationCommand
        {
            Title = request.Title,
            Message = request.Message,
            Channels = request.Channels,
            RecipientType = request.RecipientType,
            RecipientRole = request.RecipientRole,
            RecipientUserIds = request.RecipientUserIds,
            DeepLinkPath = request.DeepLinkPath,
            SentByAdminId = adminId,
            SentByAdminName = adminName
        };

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

internal record SendManualNotificationRequest(
    string Title,
    string Message,
    string[] Channels,
    string RecipientType,
    string? RecipientRole = null,
    Guid[]? RecipientUserIds = null,
    string? DeepLinkPath = null);
