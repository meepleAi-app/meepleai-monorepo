# DomainEventLog operations

Issue #661 shipped a durable append-only log of domain events at
`domain_event_logs`. This doc describes runtime behavior, retention, and
the open follow-up for cleanup automation.

## Overview

- **Table**: `domain_event_logs` (Postgres). Indexes: `(UserId, LoggedAt
  DESC)`, `(LoggedAt)`, UNIQUE `EventId`.
- **Producer**: `MeepleAiDbContext.SaveChangesAsync` writes one row per
  registered `IDomainEvent` raised by an aggregate, atomically with the
  aggregate state.
- **Registry**: opt-in via `Api.Infrastructure.DomainEventLog.EventTypeRegistry`.
  An event must be listed there to be persisted. Unregistered events
  continue to flow through MediatR.Publish only (in-memory dispatch).
- **Consumers**: the UserLibrary activity feed (`GET
  /api/v1/library/activity`) reads `removed` and `session-recorded`
  events from the log; `added` and `state-changed` are still projected
  from `UserLibraryEntries` row timestamps (legacy MVP path).

## Retention

- The activity-feed query filters `LoggedAt >= NOW() - 90 days`.
- Rows older than 90 days are NOT served by the feed, but they still
  occupy the table.
- **No automatic cleanup job yet.** Open follow-up: a scheduled
  `DELETE FROM domain_event_logs WHERE LoggedAt < NOW() - INTERVAL '90
  days'` job. The single-column `(LoggedAt)` index in the migration
  exists specifically to keep that future job's range scan cheap.

## Growth estimate

At 100 active users x 5 registered events/day:
- 500 rows/day, ~182,500 rows/year before cleanup.
- The two registered events as of PR-B are `library.entry.removed` and
  `library.session.recorded`. Both are user-driven, so realistic load
  is much lower.

If growth becomes a concern before the cleanup job ships, the
operational mitigation is a one-shot `DELETE` matching the future job's
predicate.

## Recovery after a MediatR handler failure

The log row is committed BEFORE `MediatR.Publish` runs. If a handler
throws, the log row is durable and the failure is logged at ERROR with
`{EventType, EventId}` (see `MeepleAiDbContext.SaveChangesAsync` catch
block). A retry can re-process the event by reading the log row.

## Adding a new event to the log

1. Implement `IDomainEvent` as usual.
2. Add an entry to `EventTypeRegistry.AliasByType` mapping the CLR type
   to a stable alias (snake-cased, dotted: `bounded-context.aggregate.action`).
3. Aliases are stable across renames - picking a meaningful alias on
   first registration avoids future query-side breakage.
4. The unit test `EventTypeRegistryTests` enforces that every entry
   resolves to a real `IDomainEvent` implementation in the assembly.

## Removing or renaming a registered event

- **Rename the CLR class**: keep the alias the same. Activity feed
  queries reference the alias, not the type name.
- **Delete the class**: remove the registry entry. The
  `EventTypeRegistryTests` build will fail if the entry stays.
- **Rename the alias**: requires a data migration on existing log rows
  (`UPDATE domain_event_logs SET EventType = 'new.alias' WHERE
  EventType = 'old.alias'`). Coordinate with the activity-feed query.
