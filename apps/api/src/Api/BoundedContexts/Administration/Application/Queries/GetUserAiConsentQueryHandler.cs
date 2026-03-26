using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Returns user AI consent status (Issue #5512)
/// </summary>
internal sealed class GetUserAiConsentQueryHandler : IQueryHandler<GetUserAiConsentQuery, UserAiConsentDto?>
{
    private readonly IUserAiConsentRepository _consentRepository;

    public GetUserAiConsentQueryHandler(IUserAiConsentRepository consentRepository)
    {
        _consentRepository = consentRepository;
    }

    public async Task<UserAiConsentDto?> Handle(GetUserAiConsentQuery request, CancellationToken cancellationToken)
    {
        var consent = await _consentRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (consent is null) return null;

        return new UserAiConsentDto(
            consent.UserId,
            consent.ConsentedToAiProcessing,
            consent.ConsentedToExternalProviders,
            consent.ConsentedAt,
            consent.ConsentVersion);
    }
}
