using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Gamebook;

/// <summary>
/// #2750 (C14): reads the authenticated user's monthly gamebook paragraph-translation quota.
/// Display-only (no enforcement/blocking).
/// </summary>
internal sealed record GetGamebookQuotaQuery(Guid UserId) : IRequest<GamebookQuotaDto>;

/// <summary>
/// Gamebook quota DTO matching the FE <c>QuotaInfo</c> shape: { used, total, resetDate, tier }.
/// </summary>
public sealed record GamebookQuotaDto(int Used, int Total, string ResetDate, string Tier);
