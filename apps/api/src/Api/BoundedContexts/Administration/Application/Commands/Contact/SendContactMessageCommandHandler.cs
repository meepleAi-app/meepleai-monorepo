using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.Contact;

/// <summary>
/// Handler for SendContactMessageCommand.
/// Logs the contact message and returns a tracking ID.
/// Future: integrate with email service to forward messages.
/// </summary>
internal class SendContactMessageCommandHandler : ICommandHandler<SendContactMessageCommand, Guid>
{
    private readonly ILogger<SendContactMessageCommandHandler> _logger;

    public SendContactMessageCommandHandler(ILogger<SendContactMessageCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Guid> Handle(SendContactMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var messageId = Guid.NewGuid();

        _logger.LogInformation(
            "Contact message received: Id={MessageId}, Subject={Subject}",
            messageId,
            command.Subject);

        return Task.FromResult(messageId);
    }
}
