# ADR-079 — Edit-overlay drawer pattern for detail routes

**Status**: Accepted
**Date**: 2026-06-16
**Deciders**: @badsworm (ratified via [#2344](https://github.com/meepleAi-app/meepleai-monorepo/issues/2344) acceptance 2026-06-16)
**Tracking**: [#2344](https://github.com/meepleAi-app/meepleai-monorepo/issues/2344) — sp7-game-night-edit disposition decision
**Related**: ADR-061 (game-detail tab canonical) · `docs/superpowers/plans/2026-06-04-asse-b-ui-shell-pattern.md` (WP3 DrawerStack cascade-store) · `docs/superpowers/plans/2026-06-05-asse-c-dashboard-priority-driven.md` (WP6 GameNightDrawerContent)

## Context

Issue #2344 surfaced a gap in the SP7 Wave 1 mockup commission: `sp7-game-night-edit.{html,jsx}` was referenced by the brief but never shipped during the Claude Design canvas sessions A+B+K+L+M. Filesystem check 2026-06-15 confirmed:

```
admin-mockups/design_files/sp7-game-night-edit.html → ✗ NOT EXIST
admin-mockups/design_files/sp7-game-night-edit.jsx  → ✗ NOT EXIST
```

The disposition was: drop the standalone mockup, commission a missing mockup, or refactor the architectural decision so no standalone mockup is required.

The 2026-06-15 brainstorm in [#2344 comment-4707720729](https://github.com/meepleAi-app/meepleai-monorepo/issues/2344#issuecomment-4707720729) raccomandò **Option C** with multi-axis evidence:

1. **Asse-B `DrawerStack` cascade-store** already shipped (PR sess.33). Generic primitive with `openDrawer(entity, id)` API designed exactly for entity-overlay UX.
2. **Asse-A `GameNightDrawerContent`** (PR sess.34, dashboard plan WP6) already proves the pattern: edit-style content inside drawer triggered from dashboard cards.
3. **Next.js 16 App Router**: `/games/[id]/edit` style routes are anti-pattern when the edit content is strictly correlated to the detail view. Deep link `?action=edit` is the canonical alternative.
4. **A11y baseline already met**: drawer cascade-store has 0-axe-violation baseline (asse-B WP7 test `apps/web/__tests__/asse-b-axe.test.tsx`).
5. **Effort delta**: Option C ~3gg vs Option B (commission new mockup) ~10gg.

## Decision

**Edit operations on detail routes are rendered as a drawer overlay, NOT a standalone page**. The pattern is canonical for any future detail route that exposes mutation operations.

Concrete instantiation for the original #2344 case:

- Route `/game-nights/[id]/edit` (legacy, never shipped) is replaced by deep link `/game-nights/[id]?action=edit`.
- A `GameNightEditDrawer` component is mounted in the detail page; it opens when the URL search param `action=edit` is present.
- The drawer reuses `GameNightDrawerContent` (asse-A primitive) where possible — content composition rather than a new page.
- Brief SP7 Wave 1 mockup C is marked as "not commissioned (per ADR-079)".

## Pattern (for future detail routes)

When a detail route needs an edit operation:

```
✅ DO
  /entity/[id]?action=edit         → opens <EntityEditDrawer> mounted in detail page
  /entity/[id]?action=invite        → opens <EntityInviteDrawer>
  /entity/[id]?action=share         → opens <EntityShareDrawer>

❌ DO NOT
  /entity/[id]/edit                 → standalone page
  /entity/[id]/invite               → standalone page
  /entity/[id]/share                → standalone page
```

Reasons standalone edit pages are anti-pattern:

- Doubles the route tree without adding distinct content (the form fields are largely a subset of the detail view).
- Forces server-side route group placement decisions (`(public)` vs `(authenticated)` vs `(admin)`) that are already settled by the parent detail route — re-deciding them per action invites drift.
- Breaks deep-link locality: linking to the edit form requires the user to navigate to the detail page first to grasp context.
- Multiplies a11y test surface: each new page needs its own axe baseline.

## Exceptions to the pattern

The drawer overlay is **not** appropriate when:

- The edit form is structurally large (>10 fields, multi-step wizard, file upload tab, payment flow). Use a wizard modal (`WizardModal` primitive shipped in asse-B WP4) or a dedicated route.
- The edit operation has a long-lived state that must survive page reloads (e.g. multi-hour draft of a published article). Use a dedicated route + local storage.
- The user-flow is conceptually "creating a sibling resource", not "modifying the current resource". Then it's a create flow, not an edit overlay (see SP7-A `sp7-game-night-new` for the create case).

When in doubt, default to drawer overlay. The wizard modal escape hatch is available if the form complexity blows past the drawer's comfortable scope.

## Consequences

### Positive

- **No new mockup commissions** for routine edit flows of existing detail routes. Brief SP7 Wave 1 mockup C is now closed at zero design effort.
- **Reuse of asse-B `DrawerStack` cascade-store** — a single drawer infrastructure handles edit, invite, share, settings drawers across the app.
- **Deep link canonical for state** — `?action=edit` lives in the URL, so browser back/forward works correctly, and links shareable into chat/email open the right state.
- **A11y baseline stays at 0** — no new route, no new axe surface.
- **Bundle budget**: drawer content is conditionally loaded (`dynamic` import), so the detail page chunk does not grow.

### Negative

- **Mental model shift** for new contributors who expect REST-style `/edit` routes. Mitigated by this ADR + `NotificationRoutes` style codification (separate ADR-075).
- **Edit form complexity ceiling**: if a future edit grows to wizard-scale (>10 fields, multi-step), the migration from drawer to wizard modal is a refactor. Mitigated by the "exceptions" section above and the wizard modal escape hatch.
- **Search engines never see edit URLs**: not a concern for authenticated mutation surfaces, but worth flagging if any edit route is ever exposed to a public detail.

### Trade-offs accepted

- The decision is opinionated rather than negotiable per-feature: every detail route in the authenticated surface uses the same pattern. This is intentional — design coherence > local optimization.

## Implementation guidance

For the original #2344 case (`sp7-game-night-edit` ratification):

1. **Spec update** ([`docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md`](../../../for-developers/specs/2026-06-14-mockup-us-coverage-map.md) §1.2 + §4a US-INT-3c): mark mockup C as "ratified Option C drawer overlay" referencing this ADR. **Done in the same PR as this ADR.**
2. **Brief update** ([`admin-mockups/briefs/SP7-game-night-agent-builder.md`](../../../../admin-mockups/briefs/SP7-game-night-agent-builder.md) Wave 1 mockup C section): mark as "not commissioned per ADR-079". **Done in the same PR.**
3. **Redirect rule** (`apps/web/next.config.js`): `/game-nights/:id/edit` → `/game-nights/:id?action=edit` (permanent 301). Safety net for any link that escaped into chat/email before this ADR landed. **Defer to implementation PR** (next session).
4. **GameNightEditDrawer component** mounted in `/game-nights/[id]` detail page, opened by URL search param `action=edit`. Reuses `GameNightDrawerContent` asse-A primitive. **Defer to implementation PR** (~1gg).
5. **Close #2344** with link to this ADR + spec update + brief update.

Total implementation effort: ~3gg single FTE (item 3 ~0.5gg, item 4 ~1gg + tests, item 5 trivial). Items 1-2 ship in the ADR PR (this); items 3-5 in a follow-up PR.

## Rollback / reversibility

The ADR is documentation. The actual change at code level is:

- `next.config.js` redirect rule — trivial to remove (single block).
- `GameNightEditDrawer` mount in detail page — `git revert` the component file + the `?action=edit` handling in the detail page.

If after rollout users (or analytics) demonstrate the drawer pattern is wrong for this case, restore by:

- Commissioning `sp7-game-night-edit` mockup.
- Adding `/game-nights/[id]/edit` standalone page route in Next.js App Router.
- Updating brief SP7 Wave 1 to re-include mockup C.

Reverting the ADR itself is a doc-only change.

## References

- Issue #2344 — disposition decision (the source of this ADR)
- Issue #2344 [comment-4707720729](https://github.com/meepleAi-app/meepleai-monorepo/issues/2344#issuecomment-4707720729) — original Option C recommendation with full multi-axis rationale
- Issue #2344 [comment-4718607499](https://github.com/meepleAi-app/meepleai-monorepo/issues/2344#issuecomment-4718607499) — status reminder + ratification ask
- ADR-061 — game-detail tab canonical (similar single-detail-route discipline for tabs)
- Spec: `docs/superpowers/plans/2026-06-04-asse-b-ui-shell-pattern.md` (WP3 — DrawerStack cascade-store + WP7 axe baseline)
- Spec: `docs/superpowers/plans/2026-06-05-asse-c-dashboard-priority-driven.md` (WP6 — GameNightDrawerContent props-based asse-B reuse)
- Brief: `admin-mockups/briefs/SP7-game-night-agent-builder.md` (Wave 1 mockup C section — updated in same PR as this ADR)
- Memory: `route-group-audience-not-feature.md` (route group discipline that constrains the canonical pattern)
