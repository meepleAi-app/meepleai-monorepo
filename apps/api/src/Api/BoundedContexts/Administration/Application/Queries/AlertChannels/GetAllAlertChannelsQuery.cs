using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertChannels;

/// <summary>
/// Returns every configured alert channel for the admin Canali drawer
/// (Issue #1840 SP5 F4-C7).
/// </summary>
internal sealed record GetAllAlertChannelsQuery : IRequest<IReadOnlyList<AlertChannelDto>>;

/// <summary>
/// Channel DTO surfaced to the admin UI. The transport config blob
/// (<see cref="ConfigJson"/>) is returned verbatim — secrets-at-rest hardening
/// is tracked as a follow-up; the field is intentionally not masked here so
/// the existing drawer form can round-trip without re-fetching after save.
/// </summary>
internal sealed record AlertChannelDto(
    string Type,
    string ConfigJson,
    bool IsEnabled,
    DateTime? LastTestedAt,
    string? LastTestStatus,
    string? LastTestMessage,
    DateTime UpdatedAt,
    string? UpdatedBy,
    string RowVersion);
