using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.UserProfile;

/// <summary>
/// Handles saving user's onboarding interest selections.
/// Issue #124: Invitation system onboarding wizard.
/// </summary>
internal sealed class SaveUserInterestsCommandHandler : ICommandHandler<SaveUserInterestsCommand>
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SaveUserInterestsCommandHandler> _logger;

    public SaveUserInterestsCommandHandler(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        ILogger<SaveUserInterestsCommandHandler> logger)
    {
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SaveUserInterestsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userRepo.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new NotFoundException("User", command.UserId.ToString());

        user.UpdateInterests(command.Interests);

        await _userRepo.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated interests for user {UserId}: {InterestCount} interests",
            command.UserId, command.Interests?.Count ?? 0);
    }
}
