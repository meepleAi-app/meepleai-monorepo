using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Command to batch-provision multiple users and send invitation emails (JSON path).
/// Iterates items and dispatches ProvisionAndInviteUserCommand per item via MediatR.
/// Does NOT abort on individual failures — collects errors into Failed list.
/// Issue #124: Admin invitation flow — batch provisioning.
/// </summary>
internal sealed record BatchProvisionCommand(
    List<BatchInvitationItemDto> Invitations,
    Guid InvitedByUserId
) : ICommand<BatchInvitationResultDto>;

/// <summary>
/// Handles batch provisioning by dispatching individual ProvisionAndInviteUserCommand per item.
/// Catches per-item exceptions and collects them into the Failed list.
/// Issue #124: Admin invitation flow — batch provisioning.
/// </summary>
internal sealed class BatchProvisionCommandHandler
    : ICommandHandler<BatchProvisionCommand, BatchInvitationResultDto>
{
    private const int MaxItems = 100;

    private readonly ISender _sender;
    private readonly ILogger<BatchProvisionCommandHandler> _logger;

    public BatchProvisionCommandHandler(ISender sender, ILogger<BatchProvisionCommandHandler> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BatchInvitationResultDto> Handle(BatchProvisionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var succeeded = new List<InvitationDto>();
        var failed = new List<BatchInvitationError>();

        if (command.Invitations is not { Count: > 0 })
            return new BatchInvitationResultDto(succeeded, failed);

        if (command.Invitations.Count > MaxItems)
            throw new FluentValidation.ValidationException(
                $"Batch exceeds maximum of {MaxItems} invitations. Found {command.Invitations.Count} items.");

        foreach (var item in command.Invitations)
        {
            try
            {
                var provisionCommand = new ProvisionAndInviteUserCommand(
                    Email: item.Email,
                    DisplayName: item.DisplayName,
                    Role: item.Role,
                    Tier: item.Tier,
                    CustomMessage: item.CustomMessage,
                    ExpiresInDays: item.ExpiresInDays,
                    GameSuggestions: item.GameSuggestions,
                    InvitedByUserId: command.InvitedByUserId);

                var result = await _sender.Send(provisionCommand, cancellationToken).ConfigureAwait(false);
                succeeded.Add(result);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to provision invitation for {Email}", item.Email);
                var userFacingError = ex switch
                {
                    Middleware.Exceptions.ConflictException => "A pending invitation or user already exists for this email",
                    ArgumentException ae => ae.Message,
                    FluentValidation.ValidationException => "Invalid invitation data",
                    _ => "Failed to provision invitation"
                };
                failed.Add(new BatchInvitationError(item.Email, userFacingError));
            }
#pragma warning restore CA1031
        }

        return new BatchInvitationResultDto(succeeded, failed);
    }
}
