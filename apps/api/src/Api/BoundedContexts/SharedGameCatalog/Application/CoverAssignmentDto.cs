using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Domain.Covers;

namespace Api.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Epic #3470 — the persisted per-context cover assignment returned by the write
/// command, so the picker can reflect the new state without a round-trip. The enums
/// serialize as their names via the global <c>JsonStringEnumConverter</c>.
/// </summary>
public sealed record CoverAssignmentDto(
    CoverContext Context,
    CoverAssignmentSource Source,
    double FocalX,
    double FocalY);
