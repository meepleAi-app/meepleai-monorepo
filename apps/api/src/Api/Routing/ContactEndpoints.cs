using Api.BoundedContexts.Administration.Application.Commands.Contact;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Public contact form endpoint.
/// No authentication required — allows anonymous submissions.
/// </summary>
internal static class ContactEndpoints
{
    public static RouteGroupBuilder MapContactEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/contact", async (SendContactMessageCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            var messageId = await mediator.Send(cmd, ct).ConfigureAwait(false);
            return Results.Ok(messageId);
        })
        .WithName("SendContactMessage")
        .WithSummary("Submit a contact form message")
        .WithTags("Contact")
        .AllowAnonymous()
        .RequireRateLimiting("ContactForm");

        return group;
    }
}
