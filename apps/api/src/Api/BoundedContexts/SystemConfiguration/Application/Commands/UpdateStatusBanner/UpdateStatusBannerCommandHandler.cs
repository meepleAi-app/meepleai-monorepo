using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateStatusBanner;

internal sealed class UpdateStatusBannerCommandHandler
    : ICommandHandler<UpdateStatusBannerCommand, AdminStatusBannerResponse>
{
    private readonly IIncidentBannerRepository _repository;
    private readonly ILogger<UpdateStatusBannerCommandHandler> _logger;

    public UpdateStatusBannerCommandHandler(
        IIncidentBannerRepository repository,
        ILogger<UpdateStatusBannerCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminStatusBannerResponse> Handle(
        UpdateStatusBannerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Parse severity (validator guarantees parse succeeds).
        if (!Enum.TryParse<BannerSeverity>(request.Severity, ignoreCase: true, out var severity))
            severity = BannerSeverity.Info;

        var state = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);

        state.Update(
            message: request.Message ?? string.Empty,
            severity: severity,
            isActive: request.IsActive,
            startsAt: request.StartsAt,
            endsAt: request.EndsAt,
            updatedBy: request.UpdatedBy);

        await _repository.UpdateAsync(state, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Status banner updated by {UpdatedBy} — active={IsActive}, severity={Severity}",
            request.UpdatedBy ?? "(unknown)", state.IsActive, state.Severity);

        return new AdminStatusBannerResponse(
            Message: state.Message,
            Severity: state.Severity.ToString(),
            IsActive: state.IsActive,
            StartsAt: state.StartsAt,
            EndsAt: state.EndsAt,
            UpdatedAt: state.UpdatedAt,
            UpdatedBy: state.UpdatedBy);
    }
}
