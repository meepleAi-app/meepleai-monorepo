# `useTranslation()` SSR-reachability audit — 2026-05-13

**Issue:** [#1102](https://github.com/meepleAi-app/meepleai-monorepo/issues/1102)
**Trigger:** [#1076](https://github.com/meepleAi-app/meepleai-monorepo/issues/1076) (NotFoundContent React #418 hydration mismatch)
**Auditor:** Claude Code (panel-reviewed sweep)
**Scope:** `apps/web/src/**/*.{ts,tsx}` excluding `__tests__` and the hook definition itself

## TL;DR

| Metric | Count |
|--------|-------|
| Files matching `useTranslation()` or `useIntl()` | 60 |
| Files **without** an explicit `'use client'` directive | 10 |
| Confirmed SSR-reachability bugs | **0** (the only known case — `_not-found-content.tsx` — is fixed by PR #1100) |
| Latent risks requiring follow-up | 0 |

**Outcome:** The codebase is **safe-by-construction** for the `useTranslation` SSR pattern after #1076 lands. No additional fixes required. The helper proposed in #1101 should target the 3 known callsites only, not a wider migration.

---

## Methodology

1. Sweep all `.ts` / `.tsx` under `apps/web/src` for literal `useTranslation()` or `useIntl()` calls.
2. Exclude:
   - Test files (`**/__tests__/**`)
   - The hook implementation itself (`src/hooks/useTranslation.ts`)
3. For each match, classify by **SSR-reachability**:
   - **CLIENT-EXPLICIT** — file declares `'use client'` at module top. Safe: file is a client-boundary, `IntlProvider` is mounted upstream.
   - **CLIENT-TRANSITIVE** — file omits `'use client'` but is only imported from files that declare it. Safe in Next.js App Router: the boundary cascades down.
   - **SERVER-REACHABLE** — file is reached during SSR with no client-boundary ancestor on the import path. **This is the bug class introduced by #1076.**
   - **FALSE POSITIVE** — match is inside a comment / string / docstring, not actual hook invocation.

The classification uses the chain:
- Find files lacking `'use client'`.
- Identify their importers.
- Walk up until a `'use client'` boundary OR a server entry point (page.tsx / layout.tsx / not-found.tsx / error.tsx) is reached.

## Findings

### Server-reachable bugs

| File | Status | Notes |
|------|--------|-------|
| `apps/web/src/app/_not-found-content.tsx` | **FIXED** (PR #1100) | Was the only true bug. Now a server component reading static `it.json`. |

### Files lacking `'use client'` (10 candidates investigated)

| File | Classification | Boundary path | Notes |
|------|---------------|---------------|-------|
| `app/_not-found-content.tsx` | FIXED | n/a — now server component by design | PR #1100 |
| `components/auth/AuthModal.tsx` | CLIENT-TRANSITIVE | `(auth)/login/_content.tsx → 'use client'` | Modal mounted only from auth `_content.tsx` shells that all have `'use client'`. |
| `components/auth/LoginForm.tsx` | CLIENT-TRANSITIVE | `(auth)/login/_content.tsx → 'use client'` | Also re-exported via `AuthModal.tsx` (same client tree). |
| `components/auth/RegisterForm.tsx` | CLIENT-TRANSITIVE | `(auth)/register/_content.tsx → 'use client'` | Also re-exported via `AuthModal.tsx`. |
| `components/auth/TwoFactorDisable.tsx` | CLIENT-TRANSITIVE | re-exported via `components/auth/index.ts` consumed by client `_content.tsx` shells only |  |
| `components/auth/TwoFactorRecoveryCodes.tsx` | CLIENT-TRANSITIVE | as above |  |
| `components/auth/TwoFactorSetup.tsx` | CLIENT-TRANSITIVE | as above |  |
| `components/auth/TwoFactorVerification.tsx` | CLIENT-TRANSITIVE | `(auth)/login/_content.tsx → 'use client'` |  |
| `components/comments/CommentForm.tsx` | CLIENT-TRANSITIVE | `components/comments/CommentThread.tsx → versions/page.tsx → 'use client'` |  |
| `lib/join/games.ts` | **FALSE POSITIVE** | n/a | Match is inside a JSDoc comment (`* resolve via useTranslation().t(entry.labelKey) ...`). The file exports static data only. |

### Server pages verified clean of i18n hooks

`dashboard/page.tsx` and `game-nights/page.tsx` were flagged as server pages importing from `components/auth/*`. Verified:
- Both import only `RequireRole` (from `components/auth/RequireRole`).
- `RequireRole` contains no `useTranslation` / `useIntl` calls.

No SSR boundary crossing into i18n hooks.

### `generateMetadata` and `error.tsx` / `loading.tsx` review

| Path | i18n usage | Safe? |
|------|-----------|-------|
| `app/error.tsx` | uses `useTranslation` but has `'use client'` | ✅ — error boundaries in Next.js App Router are always client components |
| `app/(public)/shared-games/[id]/page.tsx` `generateMetadata` | imports `it.json` statically (Issue #617 pattern) | ✅ — already SSR-safe |
| `app/(public)/shared-games/[id]/not-found.tsx` | hardcoded IT strings, no hook | ✅ |
| `app/(authenticated)/agents/[id]/page.tsx` `generateMetadata` | _not inspected — flag for follow-up if EN rollout begins_ | ⚠️ TBD |
| `app/(public)/invites/[token]/page.tsx` `generateMetadata` | _not inspected — flag for follow-up if EN rollout begins_ | ⚠️ TBD |

The latter two `generateMetadata` callsites are not bugs today (they don't use `useTranslation` — they likely return hardcoded titles or use the same `it.json` import pattern), but should be re-verified when SSR locale negotiation lands (#1103).

## Implications for #1101 (`getSsrMessages` helper)

Confirmed callsites for the helper (per Newman's "minimal viable helper" argument in the panel):

1. `apps/web/src/app/_not-found-content.tsx` (post-#1100) — currently `const NOT_FOUND_MESSAGES = itMessages.pages.errors.notFound;`
2. `apps/web/src/app/(public)/shared-games/[id]/page.tsx` — `generateMetadata` consumer of `itMessages.pages.sharedGameDetail.metadata.*`
3. `apps/web/src/app/(public)/shared-games/[id]/page.tsx` — module-level `SSR_METADATA = itMessages.pages.sharedGameDetail.metadata`

No additional latent callsites discovered. The helper can ship as a simple typed dot-path resolver against `typeof itMessages` without provisioning for unknown future consumers.

## Recommendations

1. **Proceed with #1101** as a minimal helper (3 callsites). Skip the wider migration that #1102 contemplated as a potential outcome.
2. **Add an ESLint rule** (post-#1101): `no-usetranslation-in-server-component` — flag any file that calls `useTranslation()` without a `'use client'` directive at module top, unless the file is allowlisted as a transitive client. (Even better long-term: a Next.js linter plugin that walks the import graph.) Track as a stretch goal under #1101 — not blocking.
3. **Re-run this audit when EN locale becomes user-selectable** (#1103). The `useTranslation` callers that are currently safe-by-locale (only IT, no negotiation) may produce different runtime behavior once locale flows server→client.
4. **Close #1102 as completed** with this report as the closing artifact.

## Verification commands

To reproduce this sweep:

```bash
# Find all useTranslation/useIntl callers
grep -rlE "useTranslation\s*\(\s*\)|useIntl\s*\(\s*\)" apps/web/src \
  --include='*.ts' --include='*.tsx' \
  | grep -v __tests__ | grep -v 'hooks/useTranslation.ts'

# Filter to those without 'use client'
for f in $(grep -rlE "useTranslation\s*\(\s*\)|useIntl\s*\(\s*\)" apps/web/src --include='*.tsx' --include='*.ts' | grep -v __tests__ | grep -v 'hooks/useTranslation.ts'); do
  grep -qE "^\s*['\"]use client['\"]" "$f" || echo "NO-USE-CLIENT: $f"
done
```

Expected output: 10 files matching the second command (1 fixed + 7 auth + 1 comments + 1 false-positive). Any new entry on a future audit run must be classified before merging.

## Refs

- Issue #1076 (root cause)
- PR #1100 (fix for the one true bug)
- Issue #1101 (helper extraction — informed by this audit)
- Issue #1103 (SSR locale negotiation — deferred)
- Issue #617 (original SSR-safe i18n pattern in shared-games)
