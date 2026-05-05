using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Handler for <see cref="UpdateCertificationThresholdsCommand"/> (ADR-051 Sprint 1 / Task 26).
/// Builds a new <see cref="CertificationThresholds"/> value object via its validating factory,
/// applies it to the singleton <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.CertificationThresholdsConfig"/>
/// aggregate, and commits through a single unit-of-work boundary.
/// </summary>
/// <remarks>
/// <para>
/// Intentionally does NOT recompute or recertify any existing <c>MechanicAnalysis</c> rows.
/// Threshold edits and bulk recalculation are split so each is independently auditable;
/// operators trigger the recalc pipeline separately.
/// </para>
/// <para>
/// The <see cref="CertificationThresholds.Create"/> factory throws <see cref="ArgumentException"/>
/// on out-of-bounds inputs as defense-in-depth — the validator enforces the same bounds at the
/// surface, so this propagation is only reached if the request bypasses validation.
/// </para>
/// </remarks>
internal sealed class UpdateCertificationThresholdsHandler
    : ICommandHandler<UpdateCertificationThresholdsCommand, Unit>
{
    private readonly ICertificationThresholdsConfigRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateCertificationThresholdsHandler> _logger;

    public UpdateCertificationThresholdsHandler(
        ICertificationThresholdsConfigRepository configRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateCertificationThresholdsHandler> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        UpdateCertificationThresholdsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = await _configRepository
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

        var thresholds = CertificationThresholds.Create(
            request.MinCoveragePct,
            request.MaxPageTolerance,
            request.MinBggMatchPct,
            request.MinOverallScore);

        config.Update(thresholds, request.UserId);

        await _configRepository.UpdateAsync(config, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated certification thresholds: MinCoverage={MinCoverage} MaxPageTol={MaxPageTol} " +
            "MinBgg={MinBgg} MinOverall={MinOverall} by user {UserId}.",
            request.MinCoveragePct,
            request.MaxPageTolerance,
            request.MinBggMatchPct,
            request.MinOverallScore,
            request.UserId);

        return Unit.Value;
    }
}
