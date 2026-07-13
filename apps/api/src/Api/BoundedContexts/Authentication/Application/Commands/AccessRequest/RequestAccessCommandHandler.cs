using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class RequestAccessCommandHandler : ICommandHandler<RequestAccessCommand, Unit>
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RequestAccessCommandHandler(
        IAccessRequestRepository accessRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _accessRequestRepository = accessRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RequestAccessCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Always perform BOTH lookups for timing equalization (email enumeration prevention).
        // Issue #2804 (symptom 1): the two repositories share the SAME request-scoped MeepleAiDbContext,
        // which permits at most one in-flight operation. Starting both queries as overlapping tasks tripped
        // EF's ConcurrencyDetector ("a second operation was started on this context instance...") → HTTP 500.
        // Awaiting them sequentially preserves the timing-equalization property (both queries always run)
        // without concurrent use of the shared context.
        var existingUser = await _userRepository.GetByEmailAsync(
            new Email(normalizedEmail), cancellationToken).ConfigureAwait(false);
        var pendingRequest = await _accessRequestRepository.GetPendingByEmailAsync(
            normalizedEmail, cancellationToken).ConfigureAwait(false);

        // Silent skip: existing account or already pending — same response as success
        if (existingUser is not null || pendingRequest is not null)
            return Unit.Value;

        var accessRequest = Domain.Entities.AccessRequest.Create(normalizedEmail);
        await _accessRequestRepository.AddAsync(accessRequest, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
