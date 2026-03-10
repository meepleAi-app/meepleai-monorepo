using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Services;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Checks user AI consent by querying the Administration context (Issue #5513).
/// GDPR safe default: no record = not consented.
/// </summary>
internal sealed class UserAiConsentCheckService : IUserAiConsentCheckService
{
    private readonly IUserAiConsentRepository _consentRepository;

    public UserAiConsentCheckService(IUserAiConsentRepository consentRepository)
    {
        _consentRepository = consentRepository ?? throw new ArgumentNullException(nameof(consentRepository));
    }

    public async Task<bool> IsAiProcessingAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var consent = await _consentRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return consent?.ConsentedToAiProcessing ?? false;
    }

    public async Task<bool> IsExternalProviderAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var consent = await _consentRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return consent is { ConsentedToAiProcessing: true, ConsentedToExternalProviders: true };
    }
}
