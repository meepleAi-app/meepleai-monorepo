using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.StagingAllowlist;

/// <summary>
/// Lists active staging allowlist entries, newest first.
/// Backs <c>GET /api/v1/admin/staging-allowlist</c>.
/// </summary>
public sealed record GetStagingAllowlistQuery : IRequest<IReadOnlyList<StagingAllowlistEntryDto>>;
