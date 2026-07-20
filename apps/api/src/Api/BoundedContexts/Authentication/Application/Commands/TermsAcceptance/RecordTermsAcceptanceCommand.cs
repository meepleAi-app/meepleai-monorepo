using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

#pragma warning disable MA0048 // File name must match type name - Command + Handler in one file
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Records that a user accepted the current ToS version (#2954 F1). Used by the
/// /users/me/terms/accept endpoint (Context = ReConsent). Idempotent: no new row
/// when the user's latest accepted version already equals TermsVersion.Current.
/// </summary>
internal record RecordTermsAcceptanceCommand(
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<TermsConsentStatusDto>;

internal sealed class RecordTermsAcceptanceCommandHandler
    : ICommandHandler<RecordTermsAcceptanceCommand, TermsConsentStatusDto>
{
    private readonly ITermsAcceptanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordTermsAcceptanceCommandHandler(ITermsAcceptanceRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TermsConsentStatusDto> Handle(RecordTermsAcceptanceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var latest = await _repository.GetLatestByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (latest is null || !string.Equals(latest.TermsVersion, TermsVersion.Current, StringComparison.Ordinal))
        {
            var acceptance = TermsAcceptance.Create(
                command.UserId,
                TermsVersion.Current,
                TermsAcceptanceContext.ReConsent,
                command.IpAddress,
                command.UserAgent);

            await _repository.AddAsync(acceptance, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new TermsConsentStatusDto(TermsVersion.Current, TermsVersion.Current, acceptance.AcceptedAt, NeedsReAcceptance: false);
        }

        return new TermsConsentStatusDto(TermsVersion.Current, latest.TermsVersion, latest.AcceptedAt, NeedsReAcceptance: false);
    }
}
