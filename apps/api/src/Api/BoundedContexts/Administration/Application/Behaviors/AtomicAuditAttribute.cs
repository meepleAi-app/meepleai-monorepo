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
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AtomicAuditAttribute : Attribute { }
