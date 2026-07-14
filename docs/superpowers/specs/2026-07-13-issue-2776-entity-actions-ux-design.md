# Issue #2776 — `useEntityActions` quick-action UX cleanup — Design

**Date**: 2026-07-13
**Issue**: [#2776](https://github.com/meepleAi-app/meepleai-monorepo/issues/2776) (tech-debt, area/frontend, parent #564)
**Branch**: `feature/issue-2776-entity-actions-ux`
**Scope**: 1 file (`apps/web/src/hooks/useEntityActions.ts`) + unit tests

---

## Context

Issue #2776 flags 5 quick-action handlers in `useEntityActions.ts` whose intended UX was
stubbed with a `// TODO` and a simpler navigation/fallback. The design intent was to be
recovered from the `admin-mockups/` design files, not invented.

A read-only discovery pass (5 parallel agents, one per handler, mockups + reuse) produced two
material findings:

### Finding A — the 5 handlers are unreachable today (dead branches)

`useEntityActions` is consumed by exactly **one** component, `MeepleGameCard.tsx`, always with
`entity: 'game'`. The only wrapper, `useContextualActions.ts`, has **no consumer at all**. So
the `switch` branches for `session`, `kb`, `chat`, `player`, `event` — i.e. **all 5 TODOs** —
are never rendered. This matches the issue's own note ("no user-facing breakage today").

Consequence: this is genuine **code-cleanup**, not a UX-facing bugfix. There is zero runtime
risk, and we should keep changes minimal + honest rather than build new UI components for code
nobody calls. (`useContextualActions` also hard-caps visible actions at 4 via `slice(0, 4)`.)

### Finding B — 3 of the 5 fallbacks are broken links (not "working fallbacks")

The issue states the handlers "are not broken (a working fallback exists)". That is **false**
for 3 of them:

| Handler | Fallback | Reality |
|---|---|---|
| `kb` Download | `window.open('/api/v1/documents/{id}/download')` | 🔴 backend route does not exist (correct is `/api/v1/pdfs/{id}/download`) |
| `chat` Esporta | `router.push('/chat/{id}/export')` | 🔴 no such Next route → 404 |
| `event` Partecipa | `router.push('/events/{id}/rsvp')` | 🔴 no `/events/[id]` route at all |

Plus the sibling `event` "Condividi" copies `${origin}/events/{id}` — also a dead URL.

---

## Decisions (per-handler)

All edits stay inside `useEntityActions.ts`. No new components, no backend, no new routes.
Reuse only what already ships.

| # | Handler | Decision | Implementation |
|---|---------|----------|----------------|
| 1 | `session` Condividi codice | **toast feedback** | async guard + `await clipboard.writeText(sessionCode)` → `toast.success('Codice sessione copiato')`; catch → `toast.error`. Drop TODO. |
| 2 | `kb` Download | **fix dead URL** | `window.open(api.pdf.getPdfDownloadUrl(id), '_blank')` (canonical `KbDocActions` pattern). Drop TODO. |
| 3 | `chat` Esporta | **real export, default PDF** | `await api.chat.exportChat(id, { format: 'pdf' })` + success/error toast. Infra already wired (`ExportFormat = pdf\|txt\|md`). Drop TODO. |
| 4 | `player` Invita a Sessione | **keep nav + drop TODO** | Premise ("asse-B invite drawer") unsupported: no mockup, cascade drawer is entity-**detail** not a form, and there is no "add existing player to session" endpoint. The `/sessions/new` wizard does not read `invitePlayer`. Per issue DoD ("else keep navigation and remove the TODO"): keep `router.push('/sessions/new?invitePlayer={id}')`, replace TODO with an honest comment. |
| 5 | `event` Partecipa (RSVP) | **fix dead route** | `router.push('/game-nights/{id}')` — the real detail route hosting the RSVP action bar (`event` id = game-night id). Inline click-to-mutate is not viable (RSVP is a 3-way choice with 409/410 lifecycle guards that live on the detail page). Drop TODO. |

### Sibling fixes (chosen scope: "include coherent siblings")

| # | Sibling | Decision |
|---|---------|----------|
| 6 | `event` Condividi | Fix dead copy URL `/events/{id}` → `/game-nights/{id}` + add copy→toast feedback. |
| 7 | `game` Condividi | Add copy→toast feedback (same no-feedback gap as #1). URL `/games/{id}` unchanged. |

The three "copy to clipboard" actions (session code, game URL, event URL) are unified through a
single module-level `copyWithToast(text, successMessage)` helper (guard + await + success/error
toast) to avoid duplication.

### Explicitly out of scope (why chat-export stays single-action)

The `chat` export intent was a "format picker". `QuickAction` has **no submenu primitive** and
the wrapper caps at 4 actions; three flat export actions would be truncated and, on an unrendered
branch, would be dead code. So we ship a single `Esporta` (default `pdf`) that calls the existing
export infra. A true multi-format picker is deferred to when the `chat` branch gains a real
consumer + a picker design.

---

## Testing strategy

No test file exists for `useEntityActions` today. Add `apps/web/src/hooks/__tests__/useEntityActions.test.tsx`
(vitest + `@testing-library/react` `renderHook`), driving the hook **directly per entity** (the
only way to exercise the non-`game` branches). Mock: `sonner` (`toast`), `next/navigation`
(`useRouter`), `@/lib/api` (`api.pdf.getPdfDownloadUrl`, `api.chat.exportChat`), `navigator.clipboard`,
and `window.open`. Assert the corrected side effects per handler:

- session/game/event Condividi → clipboard written + `toast.success`; on clipboard rejection → `toast.error`.
- kb Download → `window.open` called with `/api/v1/pdfs/{id}/download`.
- chat Esporta → `api.chat.exportChat(id, { format: 'pdf' })` called; success/error toast.
- player Invita → `router.push('/sessions/new?invitePlayer={id}')` (unchanged, no TODO).
- event Partecipa → `router.push('/game-nights/{id}')`.

TDD: write these assertions first (red), then edit the handlers (green).

## DoD (from issue, mapped)

- [x] Copy-code: success toast + failure fallback → handlers 1/6/7.
- [x] Invite: design does not call for a modal → keep navigation, remove TODO (handler 4).
- [x] Export / Download / RSVP: implement the richer/correct flow (2, 3) or redirect to the real UX (5).
- [x] Remove the resolved `// TODO:` markers.

## Follow-up (not this PR)

- The 5 non-`game` branches (and `useContextualActions`) are unreferenced. Track a separate
  decision to either wire a real multi-entity consumer or remove the dead branches. Not done here
  to keep #2776 a minimal, reviewable cleanup.
