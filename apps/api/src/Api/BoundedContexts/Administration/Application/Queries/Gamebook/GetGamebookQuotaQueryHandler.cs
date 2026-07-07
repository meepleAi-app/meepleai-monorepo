using System.Globalization;
using Api.SharedKernel.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Gamebook;

/// <summary>
/// #2750 (C14): maps the tier-enforcement gamebook quota snapshot to the FE-facing
/// <see cref="GamebookQuotaDto"/>. Thin handler — no domain logic.
/// </summary>
internal sealed class GetGamebookQuotaQueryHandler
    : IRequestHandler<GetGamebookQuotaQuery, GamebookQuotaDto>
{
    private readonly ITierEnforcementService _tierService;

    public GetGamebookQuotaQueryHandler(ITierEnforcementService tierService)
        => _tierService = tierService ?? throw new ArgumentNullException(nameof(tierService));

    public async Task<GamebookQuotaDto> Handle(
        GetGamebookQuotaQuery request, CancellationToken cancellationToken)
    {
        var snapshot = await _tierService
            .GetGamebookQuotaSnapshotAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        return new GamebookQuotaDto(
            Used: snapshot.TranslationsThisMonth,
            Total: snapshot.MaxTranslationsPerMonth,
            ResetDate: snapshot.ResetDate.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            Tier: snapshot.Tier);
    }
}
