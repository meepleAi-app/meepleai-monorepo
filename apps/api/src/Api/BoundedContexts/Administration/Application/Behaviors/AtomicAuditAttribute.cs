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
/// ⚠ CONSTRAINT — domain events: this attribute is appropriate ONLY for commands whose handler
/// does NOT publish observable external side-effects through IDomainEventCollector.
/// Rationale: MeepleAiDbContext.SaveChangesAsync dispatches collected events via MediatR.Publish
/// INSIDE the same SaveChanges call (after base.SaveChangesAsync, before our outer Commit). If
/// the outer transaction subsequently rolls back (audit enqueue fails OR NpgsqlRetryingExecutionStrategy
/// retries on a transient error), the event side-effects already happened and CANNOT be undone.
/// The behavior calls IDomainEventCollector.Clear() at the start of each execution-strategy attempt
/// so a retried handler does not see stale events from the failed attempt — but it cannot undo
/// dispatches that already occurred during the previous SaveChangesAsync. Tracked follow-up:
/// post-commit dispatch via durable event outbox (see SP5 S1 T3b code review).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AtomicAuditAttribute : Attribute { }
