using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Returns the most recent Failed rows (terminal, retry budget exhausted). Powers
/// the future <c>/admin/monitor?tab=events</c> "poison messages" panel where
/// operators triage and replay.
///
/// <para><see cref="Limit"/> is clamped to <c>[1, 200]</c> by the handler — a
/// generous ceiling because per-row size is small (no JSON payload) and the
/// admin UI may scroll a long table.</para>
///
/// Issue #1535 T6.
/// </summary>
internal sealed record GetFailedEventOutboxRowsQuery(int Limit)
    : IQuery<IReadOnlyList<DomainEventOutboxRowDto>>;
