using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Deletes a single gamebook glossary entry. Complements the upsert flow: the PUT
/// endpoint keeps <c>TermEn</c> immutable, so renaming a term is done by deleting the
/// old entry and recreating it. Hard delete — <see cref="Domain.Entities.GamebookGlossaryEntry"/>
/// has no soft-delete state (glossary rows are per-campaign, low-value, and regenerable
/// via bootstrap).
/// </summary>
public sealed record DeleteGlossaryEntryCommand(
    Guid CampaignId,
    Guid EntryId,
    Guid CallerUserId) : IRequest<Unit>;
