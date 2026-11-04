using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Commands;
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
        // The admin who sends it: EffectiveActor, not the claims. RequireAdminSessionFilter authorizes on the
        // real actor and has already required the session to be valid, so the cast cannot fail here. During an
        // impersonation the claims (NameIdentifier, Name) are the ones of the impersonated user, who sent
        // nothing: the id and the name saved in the metadata, and shown to the recipients, must be the admin's
        // (Principal.cs). #4580: this read the claims "displayName" and "email", which the session handler does
        // not emit, so the name was always "Admin".
        var admin = ((SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!).Principal!.EffectiveActor;
        var adminName = NameShownToRecipients(admin);

        var command = new SendManualNotificationCommand
        {
            Title = request.Title,
            Message = request.Message,
            Channels = request.Channels,
            RecipientType = request.RecipientType,
            RecipientRole = request.RecipientRole,
            RecipientUserIds = request.RecipientUserIds,
            DeepLinkPath = request.DeepLinkPath,
            SentByAdminId = admin.Id,
            SentByAdminName = adminName
        };

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// The display name of the admin, or "Admin" when there is none: the recipients must never read the email
    /// address of the admin. UserRepository.MapToDomain already puts the email in <c>DisplayName</c> when the
    /// stored name is blank, so a blank name reaches this point as the email, and that is what is treated as
    /// "no name".
    /// </summary>
    private static string NameShownToRecipients(UserDto admin) =>
        string.IsNullOrWhiteSpace(admin.DisplayName)
        || string.Equals(admin.DisplayName, admin.Email, StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : admin.DisplayName;
}

internal record SendManualNotificationRequest(
    string Title,
    string Message,
    string[] Channels,
    string RecipientType,
    string? RecipientRole = null,
    Guid[]? RecipientUserIds = null,
    string? DeepLinkPath = null);
