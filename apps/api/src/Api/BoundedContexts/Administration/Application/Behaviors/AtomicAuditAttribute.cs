using System;

namespace Api.BoundedContexts.Administration.Application.Behaviors;

/// <summary>
/// Marks a command whose audit MUST be atomic with its mutation: AuditLoggingBehavior wraps the
/// handler's SaveChanges and the audit_outbox write in a single transaction (commit together or
/// roll back together). Applied to DESTRUCTIVE commands (delete user, rotate key, emergency
/// shutdown) where losing the audit of a committed mutation — or auditing a rolled-back mutation —
/// is unacceptable. Non-atomic [AuditableAction] commands use the best-effort path (separate save).
///
/// Design: the handler's own UoW.SaveChangesAsync() is NOT removed — the behavior opens a
/// transaction, calls next() (which includes the handler's save), then enqueues the audit outbox
/// row, and commits both in one shot. If anything fails, the transaction rolls back everything.
///
/// Atomicity guarantee: mutation + audit outbox row commit together or roll back together.
/// Error audit note: on failure the whole transaction rolls back — there is no committed mutation
/// to audit, so we intentionally do NOT write an Error audit row (it would also be rolled back).
/// This differs from best-effort [AuditableAction] commands, which write an Error audit on failure.
///
/// SP5 Admin Security S1 — Task 3b.
/// Three-amigos Q1: atomic for destructive, best-effort for the rest.
///
/// Domain-event safety post-#1535: events raised via <c>IDomainEventCollector</c> are dispatched
/// POST-COMMIT via the <c>DomainEventOutboxProcessor</c> (Phase B cutover, commit <c>23dc88727</c>
/// + T10 cleanup). A rolled-back transaction never persists the outbox row, so no event
/// side-effect leaves the system. The historical CONSTRAINT (domain events forbidden inside
/// AtomicAudit) is RESOLVED — see <c>audits/2026-06-06-issue-1535-consumer-idempotency-audit.md</c>
/// for the consumer-contract requirements that every <c>INotificationHandler&lt;TEvent&gt;</c>
/// MUST honour to make the post-commit dispatch safe.
///
/// <para>⚠ <b>Hybrid rollback caveat</b>: flipping <c>DomainEventOutbox:Mode</c> back to
/// <c>Hybrid</c> re-introduces the inline-dispatch race for any <c>[AtomicAudit]</c> command
/// whose handler raises a domain event with observable external side-effects (e.g.
/// <c>RotateProviderKeyCommand</c> → <c>ProviderKeyRotatedEvent</c> → Redis pub/sub broadcast).
/// In Hybrid, the inline <c>MediatR.Publish</c> fires INSIDE <c>SaveChangesAsync</c>, so a
/// rolled-back outer audit transaction CAN leave a side-effect in flight. Treat Hybrid as a
/// short-term incident-response measure; do not run Hybrid as steady state with these
/// commands enabled.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AtomicAuditAttribute : Attribute { }
