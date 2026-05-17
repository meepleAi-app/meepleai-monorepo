using Api.BoundedContexts.Administration.Application.Queries.StagingAllowlist;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

internal sealed class AddStagingAllowlistEntryCommandHandler
    : IRequestHandler<AddStagingAllowlistEntryCommand, StagingAllowlistEntryDto>
{
    private readonly IStagingAllowlistRepository _repository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddStagingAllowlistEntryCommandHandler(
        IStagingAllowlistRepository repository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<StagingAllowlistEntryDto> Handle(
        AddStagingAllowlistEntryCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = StagingAllowlistEntry.NormalizeEmail(request.Email);

        if (await _repository.ExistsByEmailAsync(normalized, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"Email '{normalized}' is already in the staging allowlist.");
        }

        var entry = StagingAllowlistEntry.Create(normalized, request.AddedByUserId, request.Note);
        await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);

        var audit = new AuditLog(
            id: Guid.NewGuid(),
            userId: request.AddedByUserId,
            action: "staging_allowlist.added",
            resource: "staging_allowlist",
            result: "success",
            resourceId: normalized,
            details: request.Note);
        await _auditLogRepository.AddAsync(audit, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new StagingAllowlistEntryDto(entry.Id, entry.Email, entry.AddedByUserId, entry.AddedAt, entry.Note);
    }
}
