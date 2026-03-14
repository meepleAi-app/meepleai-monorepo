using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class RequestAccessCommandHandler : ICommandHandler<RequestAccessCommand, Unit>
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IUserRepository _userRepository;

    public RequestAccessCommandHandler(
        IAccessRequestRepository accessRequestRepository,
        IUserRepository userRepository)
    {
        _accessRequestRepository = accessRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<Unit> Handle(RequestAccessCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Always perform both lookups for timing equalization (email enumeration prevention)
        var existingUserTask = _userRepository.GetByEmailAsync(
            new Email(normalizedEmail), cancellationToken);
        var pendingRequestTask = _accessRequestRepository.GetPendingByEmailAsync(
            normalizedEmail, cancellationToken);

        var existingUser = await existingUserTask.ConfigureAwait(false);
        var pendingRequest = await pendingRequestTask.ConfigureAwait(false);

        // Silent skip: existing account or already pending — same response as success
        if (existingUser is not null || pendingRequest is not null)
            return Unit.Value;

        var accessRequest = Domain.Entities.AccessRequest.Create(normalizedEmail);
        await _accessRequestRepository.AddAsync(accessRequest, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
