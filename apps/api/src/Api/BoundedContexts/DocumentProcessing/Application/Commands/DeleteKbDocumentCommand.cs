using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Deletes a KB document. Sent from the admin explorer, from the orphan cleanup of the catalog
/// seed, and — on behalf of the owner — when a private game is deleted
/// (<c>PurgePrivateGameContentCommandHandler</c>, #4244): in that case the audit row names the
/// user, not an admin.
///
/// Side-effects (in order):
///   1. Remove the document id from KbCardIds of every consuming AgentDefinition.
///   2. Delete raw pgvector embeddings via IVectorStoreAdapter.
///   3. Remove PdfDocument (EF cascades TextChunks + VectorDocument).
///   4. Delete physical blob (best-effort).
///   5. Invalidate AI response cache (best-effort).
///   6. Invalidate what the hybrid cache still holds of the document (best-effort).
///   7. Emit the audit row (atomic with the mutation via [AtomicAudit]).
///
/// Issue #1653: F3-FU-4 — Admin delete KB document action.
/// </summary>
/// <remarks>
/// [AtomicAudit]: the DB mutation and the audit_outbox row are committed in a single transaction.
/// Note: UpdateKbCardIds fires AgentDefinitionUpdatedEvent (domain event). This is an in-process
/// event used for tracking only; no durable external side-effects are dispatched from it, so
/// AtomicAudit is safe here. See AtomicAuditAttribute constraint notes.
/// </remarks>
[AuditableAction("KbDocumentDelete", "Document", Level = 2)]
[AtomicAudit]
internal sealed record DeleteKbDocumentCommand(Guid Id) : ICommand;
