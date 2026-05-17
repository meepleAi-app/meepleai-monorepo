# Alpha Zero Scope — ARCHIVED

> **Status: REMOVED 2026-05-10 (PR #949, commit `e8a940264`).**
>
> The `ALPHA_MODE` / `NEXT_PUBLIC_ALPHA_MODE` env flag and its associated
> feature gating (UI hides, nav filtering, route guard, ~155 lines of
> backend endpoint gating) have been removed entirely from the codebase.
> All previously gated features are now permanently enabled.

## What replaced it

The "alpha" use case (gradual rollout to invited users) is now covered by
the runtime **`RegistrationMode`** admin toggle:

- Admin UI: `/admin/config` → General → Registration Mode
- Backed by DB-persisted config (no env var, no rebuild, no redeploy)
- When `publicRegistrationEnabled=false`, `/register` renders the
  `RequestAccessForm` (request-access popup) instead of the standard form
- Endpoint: `MapAccessRequestEndpoints` (always registered in core endpoints)

To put a deployment in invite-only mode, flip the toggle in the admin UI
or seed the DB via `FeatureFlagSeeder` with the desired default.

## Historical context

This file previously described the Alpha Zero feature scope (auth, games,
BGG, RAG chat, library) and which Bounded Contexts were active vs hidden
under `ALPHA_MODE=true`. That gating is gone — every BC is reachable
unconditionally. Refer to [the v2 migration matrix](./frontend/v2-migration-matrix.md)
for the canonical list of route status (done / pending / deferred).

## Related

- PR #949 — `refactor: remove ALPHA_MODE gating entirely (FE + BE + infra + docs)`
- ADR-052 — `Frontend Mock Mode Removal` (sister cleanup)
