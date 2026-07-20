# #3196 — Score editor mobile touch-targets (design)

**Issue**: [#3196](https://github.com/meepleAi-app/meepleai-monorepo/issues/3196) · **Date**: 2026-07-20 · **Parent audit**: #2989 · **Branch**: `feature/issue-3196-score-editor-touch-targets`

## 1. Context & corrected premise

The issue title is `[design] Parità mobile session-live + session editor (wave #3)` and lists two surfaces. Discovery (code + test read, 2026-07-20) corrected the premise:

- **Surface 1 — "session live immersiva mobile" is already shipped.** `SessionLiveView` (`apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`) already renders a full mobile shell (`MobileBody` `lg:hidden` → FAB → `MobileBottomSheetDrawer` Radix Sheet hosting the same polymorphic tabs, `LiveMobileMetaStrip`, mobile URL SSOT `?msheet`/`?mtab`/`?mchat`), covered by unit tests. This landed with epic #2374 T9; the CLAUDE.md "in-progress" note is stale. **No work needed.**
- **Route correction**: the shell lives at **`/sessions/[id]/live`**, not `/game-nights/[id]/live` (which is the unrelated `NightLiveClientView`, #1255).
- The referenced gap report (`2026-07-15-...-mobile.md` §2) registers session-live as a deferred "gap headline" with **no acceptance criteria** → this is genuinely a *design* task.

**Real remaining scope: Surface 2 — the score editor touch-targets.** The 4 score-type editors have interactive controls below the 44px touch minimum. This spec covers only that.

## 2. Scope

**In scope** — bring the 4 editors in `apps/web/src/components/sessions/score-strategies/` to touch-target ≥44px on mobile:

| Editor | Current control | Measured | Fix |
|---|---|---|---|
| `PointsEditor` | number input `px-3 py-1` | ~34px | enlarge input to ≥44px + `inputMode="numeric"` |
| `BinaryWinEditor` | native radio + label row | radio ~16px, row ~24px | enlarge Win/Lose label rows to ≥44px (bordered tap area on mobile) |
| `ObjectivesEditor` | chip `label` + native checkbox | chip ~30px, checkbox ~16px | enlarge chip to ≥44px, scale checkbox |
| `RankingEditor` | drag-handle `⋮⋮` | ~24px | enlarge handle to ≥44px **+** add mobile-only up/down arrow reorder |

**Out of scope**:
- `SessionLiveView` mobile shell (already shipped).
- Autosave / debounce / optimistic / rate-limit logic — lives entirely in `ScoreTabContent.tsx` (`useDebouncedCallback` 500ms → `useUpdateSessionScores`), **not** in the editors. Every editor is a dumb controlled component emitting a full snapshot via `onChange`; changing layout does not touch that contract.
- `ScoreTabContent` wrapper (`<div className={className}>`) — no change.
- Pre-existing hardcoded Italian strings in `BinaryWinEditor` ("Win"/"Lose") and `RankingEditor` aria-labels — out of scope for a layout-only pass.
- Horizontal-overflow work — none needed; all editors are already single-column (`space-y-*`, `flex-wrap`).

## 3. Design decisions (approved 2026-07-20)

1. **Strategy = Hybrid.** Minimal `min-h-[44px]` enlargement on all existing controls (matches the SP8 B-02 regression-guard pattern, lowest risk) **plus** a targeted redesign only where the native control is genuinely unusable on touch: `RankingEditor` gains up/down arrows (drag stays fiddly on a 375px touchscreen even when the handle is enlarged).
2. **Mobile-first, desktop preserved via `md:`.** Base classes apply ≥44px; `md:` overrides restore the current compact desktop sizing. New Ranking arrows are **mobile-only** (`md:hidden`); the drag handle stays as the desktop reorder path. Desktop layout is unchanged.
3. **Test assertion = class-based.** Assert `toHaveClass('min-h-[44px]')` on the controls. jsdom's `getBoundingClientRect` is always 0, so real pixel height cannot be measured in unit tests; class assertion is deterministic and is the existing SP8 B-02 convention. A pixel-accurate check is deferred to an optional E2E skeleton (not part of this scope).

## 4. Per-editor changes (exact)

### 4.1 PointsEditor.tsx
Input at `:64-73`: add `inputMode="numeric"` and prepend `min-h-[44px]` to the className, with `md:min-h-0` to restore desktop:
```
className="w-24 min-h-[44px] rounded-md border border-border bg-background px-3 py-1 text-right tabular-nums md:min-h-0"
```

### 4.2 BinaryWinEditor.tsx
The two Win/Lose `<label>`s at `:64` and `:74`: give each a ≥44px bordered tap row on mobile, compact on desktop. Native radios + `fieldset`/`legend` a11y unchanged:
```
className="flex min-h-[44px] items-center gap-2 rounded-md border border-border px-3 md:min-h-0 md:gap-1 md:rounded-none md:border-0 md:px-0"
```

### 4.3 ObjectivesEditor.tsx
Chip `<label>` at `:99-101`: enlarge on mobile, revert desktop; scale the checkbox:
```
className="inline-flex min-h-[44px] items-center gap-2 rounded-md border border-border-strong px-3 py-2 text-sm md:min-h-0 md:gap-1 md:px-2 md:py-1"
```
Checkbox at `:103`: add `className="size-5 md:size-4"`.

### 4.4 RankingEditor.tsx
1. **Handle** button at `:86-96`: enlarge to a 44×44 hit area on mobile, revert desktop:
```
className="inline-flex min-h-[44px] min-w-[44px] cursor-grab items-center justify-center disabled:cursor-not-allowed md:min-h-0 md:min-w-0"
```
2. **Up/down arrows (new, mobile-only).** Add a `move(index, dir)` handler in `RankingEditor`:
```ts
const move = (index: number, dir: -1 | 1) => {
  setIds(prev => {
    const target = index + dir;
    if (target < 0 || target >= prev.length) return prev;
    return arrayMove(prev, index, target);
  });
};
```
Thread `index`, `isFirst`, `isLast`, `onMoveUp`, `onMoveDown` into `RankableItem` and render a mobile-only pair of 44×44 buttons (`▲`/`▼`, `aria-label` "Sposta <name> su/giù", `data-testid` `ranking-up-<id>`/`ranking-down-<id>`, disabled at bounds, wrapped in `md:hidden`). Drag (`dnd-kit` PointerSensor + KeyboardSensor) and the `onChange` positions contract are untouched — the arrows call the same `setIds`/`arrayMove` path.

## 5. Testing (TDD, RED→GREEN)

Extend each editor's existing test in `apps/web/src/components/sessions/score-strategies/__tests__/`:
- **Points**: input has `min-h-[44px]` and `inputMode="numeric"`.
- **BinaryWin**: each Win/Lose label has `min-h-[44px]`.
- **Objectives**: each chip label has `min-h-[44px]`.
- **Ranking**:
  - handle has `min-h-[44px]` and `min-w-[44px]`;
  - up/down arrows render, are `md:hidden`, and reorder correctly — clicking `▲` on the 2nd item promotes it to position 1 and emits `positions` with the swapped order; clicking `▼` on the last item does nothing;
  - first item's `▲` and last item's `▼` are `disabled`.

**Invariant**: every existing editor test must stay green — the `onChange` snapshot contract is unchanged. No change to `PolymorphicScoreEditor`, `ScoreTabContent`, or `useUpdateSessionScores`.

## 6. Files touched

Source (4): `PointsEditor.tsx`, `BinaryWinEditor.tsx`, `ObjectivesEditor.tsx`, `RankingEditor.tsx` (all under `apps/web/src/components/sessions/score-strategies/`).
Tests (4): the matching `__tests__/*.test.tsx`.

No new dependencies, no backend change, no i18n key additions (arrow aria-labels reuse the existing inline Italian-string convention of the file).

## 7. Acceptance criteria (DoD)

- [ ] All interactive controls in the 4 editors are ≥44px on mobile (asserted via `min-h-[44px]`/`min-w-[44px]` classes).
- [ ] Desktop sizing unchanged (verified by `md:` overrides + existing tests staying green).
- [ ] Ranking has a touch-friendly up/down reorder path (mobile-only), drag preserved on desktop.
- [ ] Autosave/debounce untouched; `onChange` contracts unchanged.
- [ ] TDD test coverage added; full suite green; typecheck + eslint clean.
- [ ] PR merged to `main-dev`.
