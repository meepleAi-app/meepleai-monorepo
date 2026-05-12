using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

internal sealed class RemoveStagingAllowlistEntryCommandHandler
    : IRequestHandler<RemoveStagingAllowlistEntryCommand, Unit>
{
    private readonly IStagingAllowlistRepository _repository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveStagingAllowlistEntryCommandHandler(
        IStagingAllowlistRepository repository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Unit> Handle(RemoveStagingAllowlistEntryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entry = await _repository.GetByIdAsync(request.EntryId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            throw new NotFoundException($"Staging allowlist entry '{request.EntryId}' not found.");
        }

        entry.SoftDelete(request.RemovedByUserId);
        await _repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);

        var audit = new AuditLog(
            id: Guid.NewGuid(),
            userId: request.RemovedByUserId,
            action: "staging_allowlist.removed",
            resource: "staging_allowlist",
            result: "success",
            resourceId: entry.Email);
        await _auditLogRepository.AddAsync(audit, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
