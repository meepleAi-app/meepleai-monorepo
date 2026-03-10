using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Creates or updates user AI consent preferences (Issue #5512)
/// </summary>
internal sealed class UpdateUserAiConsentCommandHandler : ICommandHandler<UpdateUserAiConsentCommand>
{
    private readonly IUserAiConsentRepository _consentRepository;
    private readonly ILogger<UpdateUserAiConsentCommandHandler> _logger;

    public UpdateUserAiConsentCommandHandler(
        IUserAiConsentRepository consentRepository,
        ILogger<UpdateUserAiConsentCommandHandler> logger)
    {
        _consentRepository = consentRepository;
        _logger = logger;
    }

    public async Task Handle(UpdateUserAiConsentCommand request, CancellationToken cancellationToken)
    {
        var existing = await _consentRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            existing.UpdateConsent(
                request.ConsentedToAiProcessing,
                request.ConsentedToExternalProviders,
                request.ConsentVersion);

            await _consentRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "AI consent updated for user {UserId}: AI={AiConsent}, External={ExternalConsent}, Version={Version}",
                request.UserId, request.ConsentedToAiProcessing, request.ConsentedToExternalProviders, request.ConsentVersion);
        }
        else
        {
            var consent = UserAiConsent.Create(
                request.UserId,
                request.ConsentedToAiProcessing,
                request.ConsentedToExternalProviders,
                request.ConsentVersion);

            await _consentRepository.AddAsync(consent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "AI consent recorded for user {UserId}: AI={AiConsent}, External={ExternalConsent}, Version={Version}",
                request.UserId, request.ConsentedToAiProcessing, request.ConsentedToExternalProviders, request.ConsentVersion);
        }
    }
}
