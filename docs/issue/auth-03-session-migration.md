# ISSUE-255: Apply UserSessions Migration

**Migration Name**: `20251015000000_AddLastSeenAtAndRevokedAtToUserSessions`

**Issue**: [#255 - DB-04 Apply Pending UserSessions Migration](https://github.com/DegrassiAaron/meepleai-monorepo/issues/255)

**Applied Date**: 2025-10-09

## Summary

Applied the pending database migration that adds session tracking columns to the `user_sessions` table.

## Changes Applied

### user_sessions Table

Added two new columns:
- `LastSeenAt` (timestamp with time zone, nullable): Tracks the last time a session was actively used
- `RevokedAt` (timestamp with time zone, nullable): Tracks when a session was manually revoked

## Schema Verification

Verified the migration was successfully applied by inspecting the `user_sessions` table schema:

```
                      Table "public.user_sessions"
   Column   |           Type           | Collation | Nullable | Default
------------+--------------------------+-----------+----------+---------
 Id         | character varying(64)    |           | not null |
 UserId     | character varying(64)    |           | not null |
 TokenHash  | character varying(128)   |           | not null |
 UserAgent  | character varying(256)   |           |          |
 IpAddress  | character varying(64)    |           |          |
 CreatedAt  | timestamp with time zone |           | not null |
 ExpiresAt  | timestamp with time zone |           | not null |
 LastSeenAt | timestamp with time zone |           |          |
 RevokedAt  | timestamp with time zone |           |          |
```

## Rollback Instructions

If rollback is needed, run:

```bash
cd apps/api
dotnet ef database update <PreviousMigrationName> --project src/Api --connection "Host=localhost;Port=5432;Database=meepleai;Username=meeple;Password=meeplepass"
```

Or execute the Down migration method manually in SQL.

## Notes

- Migration was already applied when this documentation was created (likely applied automatically on API startup)
- The migration includes additional schema changes for `rule_specs` table (GameEntityId foreign key)
- Both columns are nullable to support existing session records
