using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

internal sealed class GetPublicStatusBannerQueryHandler
    : IQueryHandler<GetPublicStatusBannerQuery, PublicStatusBannerResponse?>
{
    private readonly IIncidentBannerRepository _repository;

    public GetPublicStatusBannerQueryHandler(IIncidentBannerRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PublicStatusBannerResponse?> Handle(
        GetPublicStatusBannerQuery request,
        CancellationToken cancellationToken)
    {
        var state = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);

        if (!state.IsCurrentlyVisible(DateTime.UtcNow))
            return null;

        var messageId = StatusBannerMessageId.Compute(state.Message, state.Severity, state.UpdatedAt);

        return new PublicStatusBannerResponse(
            MessageId: messageId,
            Message: state.Message,
            Severity: state.Severity.ToString(),
            UpdatedAt: state.UpdatedAt);
    }
}
