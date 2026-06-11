# Post-Deploy Smoke Checklist — Issue #2116 (registration-mode env asymmetry fix)

**When to run**: immediately after the first deploy that carries [PR #2159](https://github.com/meepleAi-app/meepleai-monorepo/pull/2159) (commit `ebdf63fb7` on `main-dev`) reaches an environment other than Production.

**Why this is needed**: PR #2159 fixed a P0 500 on `PUT /api/v1/admin/settings/registration-mode` but it changed the `(Key, Environment)` row that `Registration:PublicEnabled` writes to. On a database that already carried the seeded row at `Environment="Production"`, the first toggle on a non-Production deploy writes a NEW row at the current environment name (`"Staging"`, `"Development"`, …) and the old `"Production"` row becomes orphan. The effective `publicRegistrationEnabled` value resets to its boolean default (`false`) until a superadmin sets it again.

**Risk if skipped**: silent regression on the public-registration flag — users on staging may not be able to register (or may be able to register when they shouldn't) for one full refresh cycle.

## Pre-deploy

- [ ] Note the current value of `publicRegistrationEnabled` in staging (login as superadmin → `/admin/config` → General → Registration Mode → record `Public registration enabled: true/false`).

## Post-deploy smoke (≤ 5 min)

### 1. The endpoint no longer 500s (the original bug)

- [ ] Login as superadmin at the staging URL.
- [ ] Navigate to `/admin/config` → General → Registration Mode.
- [ ] Toggle the switch (either direction).
- [ ] Confirm the page returns successfully (HTTP 200, no toast/banner error).

If you see HTTP 500 or `23505 duplicate key`, **STOP** — the fix did not deploy. Rollback per `./rollback-runbook.md`.

### 2. The new env-specific row is created

- [ ] In a DB console connected to the staging Postgres, run:

  ```sql
  SELECT environment, value, version, created_at, updated_at
  FROM system_configurations
  WHERE key = 'Registration:PublicEnabled'
  ORDER BY environment;
  ```

- [ ] Expect **2 rows**:
  - `Environment='Production'` → the original seed row (unchanged value, `version=1` if untouched by prior deploys to this DB).
  - `Environment='Staging'` (or whatever `ASPNETCORE_ENVIRONMENT` evaluates to on the staging deploy) → the env-specific row, **any `version >= 1`**. If this is the very first deploy of the fix to this DB it will be `version=1` and the row was created by step 1; on a re-deploy or if dev/staging shared a DB, it may be `version > 1` and pre-exist — that is OK as long as the row exists.

### 3. Restore the pre-deploy intent

- [ ] Compare the new env-specific row value to the pre-deploy value you recorded.
- [ ] If they differ, toggle once more so the new env-specific row matches the pre-deploy intent.
- [ ] Re-verify by toggling **twice** (on→off→on, or off→on→off). Both clicks must return HTTP 200 and the row's `version` must increment each time. No 500.

### 4. Confirm GET endpoint reads the new row

- [ ] `curl https://<staging>/api/v1/auth/registration-mode` — should return the value matching the env-specific row you just confirmed in step 2.
- [ ] Test the public path: open an incognito window, visit `/register`. The page must reflect the current `publicRegistrationEnabled` state (form vs. `RequestAccessForm` popup).

### 5. Idempotency under repeat-toggle (regression of `IX_system_configurations_Key_Environment`)

- [ ] Toggle 3 more times rapidly in the admin UI. All must return HTTP 200. The DB row count for `key='Registration:PublicEnabled'` must remain exactly 2 (never more).

## What to do if any step fails

| Failure | Likely cause | Action |
|---|---|---|
| Step 1: HTTP 500 + `23505` | Fix did not deploy / migration applied to wrong env | Verify commit on staging: `ssh meepleai@staging "docker ps --format '{{.Image}}'"`, check api image SHA. If old, redeploy. |
| Step 2: only 1 row, missing env-specific | Lookup-side env override (env var, K8s pod) is overriding `ASPNETCORE_ENVIRONMENT`, OR the toggle in step 1 silently no-op'd | First: `printenv ASPNETCORE_ENVIRONMENT` on the api container — must match the expected staging value (`"Staging"` or `"Production"` depending on infra convention). If env var is correct, capture the api container logs around the toggle time and look for a non-200 response that wasn't surfaced to the UI. |
| Step 2: > 2 rows for same key | Another writer (FeatureFlagService, see #2162) created an extra row | Capture all rows and attach to a comment on #2162. Pick one row to be the active one. |
| Step 4: GET returns wrong value | Cache stale (HybridCache 5 min TTL) | Wait 5 min OR restart the api container OR call `POST /admin/cache/invalidate` if exposed. |
| Step 5: row count grows past 2 | Race condition between concurrent toggles or another asymmetric writer | Lock the admin UI from accepting toggles, file a P1 issue, attach DB dump of the table. |

## Out of scope

- **Production deploy**: on Production `ASPNETCORE_ENVIRONMENT="Production"` so the existing seed row IS the row the new code touches. No orphan, no checklist needed. Just verify step 1 (no 500) once after rollout.
- **#2162 FeatureFlagService follow-up**: the same env asymmetry exists in `FeatureFlagService.cs` (4 methods) but is NOT exposed via the admin UI toggle path. Out of scope for this smoke; tracked separately.

## Related

- Issue #2116 (closed) — root cause.
- PR #2159 (merged) — the fix.
- Issue #2162 — same-pattern bug in `FeatureFlagService` (P1, not blocking).
- Audit: `docs/for-developers/audits/2026-06-11-config-key-environment-asymmetry-audit.md`.
- IT regression test (catches 23505 end-to-end): `apps/api/tests/Api.Tests/Integration/Authentication/SetRegistrationModeIntegrationTests.cs`.
