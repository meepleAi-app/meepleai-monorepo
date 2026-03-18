using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Handles dismissing the onboarding checklist for a user.
/// Note: Currently unused — the endpoint uses ExecuteUpdateAsync directly
/// to avoid IUnitOfWork/DbContext scope mismatch (SaveChanges returned 0).
/// Retained for potential future use via MediatR pipelines.
/// </summary>
internal sealed class DismissOnboardingCommandHandler : ICommandHandler<DismissOnboardingCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DismissOnboardingCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(
        DismissOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("User not found");

        user.DismissOnboarding();

        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
