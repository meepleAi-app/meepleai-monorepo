using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Query + Handler in one file
namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>Returns the ToS acceptance status for a user (#2954 F1).</summary>
internal record GetTermsConsentStatusQuery(Guid UserId) : IQuery<TermsConsentStatusDto>;

internal sealed class GetTermsConsentStatusQueryHandler
    : IQueryHandler<GetTermsConsentStatusQuery, TermsConsentStatusDto>
{
    private readonly ITermsAcceptanceRepository _repository;

    public GetTermsConsentStatusQueryHandler(ITermsAcceptanceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TermsConsentStatusDto> Handle(GetTermsConsentStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var latest = await _repository.GetLatestByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        var acceptedVersion = latest?.TermsVersion;
        var needsReAcceptance = !string.Equals(acceptedVersion, TermsVersion.Current, StringComparison.Ordinal);

        return new TermsConsentStatusDto(TermsVersion.Current, acceptedVersion, latest?.AcceptedAt, needsReAcceptance);
    }
}
