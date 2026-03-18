using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.Contact;

/// <summary>
/// Command to submit a contact form message.
/// Public endpoint — no authentication required.
/// </summary>
internal record SendContactMessageCommand(
    string Name,
    string Email,
    string Subject,
    string Message
) : ICommand<Guid>;
