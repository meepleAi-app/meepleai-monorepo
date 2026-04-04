using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Handles resending an invitation by expiring the old one and creating a new one.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class ResendInvitationCommandHandler : ICommandHandler<ResendInvitationCommand, InvitationDto>
{
    private readonly IInvitationTokenRepository _invitationRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendInvitationCommandHandler> _logger;

    public ResendInvitationCommandHandler(
        IInvitationTokenRepository invitationRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<ResendInvitationCommandHandler> logger)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InvitationDto> Handle(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Lookup existing invitation
        var existingInvitation = await _invitationRepo.GetByIdAsync(command.InvitationId, cancellationToken).ConfigureAwait(false);
        if (existingInvitation == null)
            throw new NotFoundException("InvitationToken", command.InvitationId.ToString());

        // Cannot resend an accepted invitation
        if (existingInvitation.Status == InvitationStatus.Accepted)
            throw new ConflictException("Cannot resend an accepted invitation");

        // Check if user already exists for this email
        var email = new Api.BoundedContexts.Authentication.Domain.ValueObjects.Email(existingInvitation.Email);
        var userExists = await _userRepo.ExistsByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (userExists)
            throw new ConflictException($"A user with email '{existingInvitation.Email}' already exists");

        // Expire old invitation
        existingInvitation.MarkExpired();
        await _invitationRepo.UpdateAsync(existingInvitation, cancellationToken).ConfigureAwait(false);

        // Generate new token
        var rawTokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(rawTokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        // Create new invitation
        var newInvitation = InvitationToken.Create(
            existingInvitation.Email,
            existingInvitation.Role,
            tokenHash,
            command.ResendByUserId);

        await _invitationRepo.AddAsync(newInvitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Send email (fire-and-forget)
        try
        {
            await _emailService.SendInvitationEmailAsync(
                existingInvitation.Email,
                existingInvitation.Role,
                rawToken,
                "Admin",
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send resend invitation email to {Email}", DataMasking.MaskEmail(existingInvitation.Email));
        }
#pragma warning restore CA1031

        return SendInvitationCommandHandler.MapToDto(newInvitation);
    }
}
