# Score Editor Mobile Touch-Targets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the 4 polymorphic score-type editors to touch-target ≥44px on mobile without changing desktop or the autosave contract.

**Architecture:** Each editor is a pure controlled component under `apps/web/src/components/sessions/score-strategies/`. Changes are mobile-first Tailwind: base `min-h-[44px]` (+ analogous width/padding), `md:` overrides restore the current compact desktop. `RankingEditor` additionally gets a mobile-only up/down arrow reorder path (`md:hidden`) because drag stays fiddly on touch. No change to `PolymorphicScoreEditor`, `ScoreTabContent`, or `useUpdateSessionScores` — the `onChange` snapshot contract is untouched.

**Tech Stack:** Next.js 16 / React 19 / Tailwind 4 / Vitest + Testing Library / `@dnd-kit` (Ranking).

## Global Constraints

- Mobile-first: base classes ≥44px; restore desktop with `md:` overrides. New Ranking arrows are `md:hidden`.
- Do NOT touch `onChange` payloads — every editor emits the same snapshot shape as today.
- Tests assert `toHaveClass('min-h-[44px]')` / `min-w-[44px]` — jsdom `getBoundingClientRect` is always 0, so real px cannot be measured.
- Semantic tokens only in classNames (`border-border`, `border-border-strong`, …). No hardcoded color utilities (ESLint `local/no-hardcoded-color-utility` is an error).
- Commit-msg hook: subject `feat|fix|docs|refactor|test|chore(scope): …`, ≤72 chars.
- Run tests per-file (`pnpm exec vitest run <path>`) — a full single-process run has known mock pollution locally. Working dir for test/lint commands is `apps/web/`.
- Background `git commit` (the pre-commit hook runs a full FE typecheck, ~5 min).

---

### Task 1: PointsEditor — 44px numeric input

**Files:**
- Modify: `apps/web/src/components/sessions/score-strategies/PointsEditor.tsx:64-73`
- Test: `apps/web/src/components/sessions/score-strategies/__tests__/PointsEditor.test.tsx`

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new (props/`onChange` unchanged).

- [ ] **Step 1: Write the failing test** — append inside the `describe('PointsEditor', …)` block (before its closing `});`):

```tsx
  it('sizes the input for touch (min-h-[44px]) with a numeric keypad', () => {
    render(<PointsEditor players={PLAYERS} onChange={vi.fn()} />);
    const input = screen.getByTestId('points-input-p1');
    expect(input).toHaveClass('min-h-[44px]');
    expect(input).toHaveAttribute('inputmode', 'numeric');
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/PointsEditor.test.tsx"`
Expected: FAIL — input lacks `min-h-[44px]` and `inputmode`.

- [ ] **Step 3: Write minimal implementation** — replace the `<input>` at `PointsEditor.tsx:64-73`:

```tsx
          <input
            id={`points-${player.id}`}
            type="number"
            inputMode="numeric"
            min={0}
            value={scores[player.id] ?? 0}
            onChange={e => handleChange(player.id, e.target.value)}
            disabled={disabled}
            data-testid={`points-input-${player.id}`}
            className="w-24 min-h-[44px] rounded-md border border-border bg-background px-3 py-1 text-right tabular-nums md:min-h-0"
          />
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/PointsEditor.test.tsx"`
Expected: PASS (all tests, including the 6 pre-existing ones).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/sessions/score-strategies/PointsEditor.tsx" "apps/web/src/components/sessions/score-strategies/__tests__/PointsEditor.test.tsx"
git commit -m "fix(sessions): #3196 PointsEditor 44px touch target"
```

---

### Task 2: BinaryWinEditor — 44px Win/Lose rows

**Files:**
- Modify: `apps/web/src/components/sessions/score-strategies/BinaryWinEditor.tsx:64-83`
- Test: `apps/web/src/components/sessions/score-strategies/__tests__/BinaryWinEditor.test.tsx`

**Interfaces:**
- Consumes: nothing new. Existing testids: `binary-win-<id>` / `binary-lose-<id>` (the radio inputs).
- Produces: nothing new.

- [ ] **Step 1: Write the failing test** — append inside the `describe('BinaryWinEditor', …)` block:

```tsx
  it('gives each Win/Lose row a 44px touch target', () => {
    render(<BinaryWinEditor players={PLAYERS} onChange={vi.fn()} />);
    const winLabel = screen.getByTestId('binary-win-p1').closest('label');
    const loseLabel = screen.getByTestId('binary-lose-p1').closest('label');
    expect(winLabel).toHaveClass('min-h-[44px]');
    expect(loseLabel).toHaveClass('min-h-[44px]');
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/BinaryWinEditor.test.tsx"`
Expected: FAIL — labels lack `min-h-[44px]`.

- [ ] **Step 3: Write minimal implementation** — replace both `<label>` opening tags at `BinaryWinEditor.tsx:64` and `:74` (each currently `<label className="flex items-center gap-1">`) with:

```tsx
              <label className="flex min-h-[44px] items-center gap-2 rounded-md border border-border px-3 md:min-h-0 md:gap-1 md:rounded-none md:border-0 md:px-0">
```

(Apply the identical className to both the Win label and the Lose label; the `<input type="radio">` and `<span>` children are unchanged.)

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/BinaryWinEditor.test.tsx"`
Expected: PASS (all tests, including pre-existing).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/sessions/score-strategies/BinaryWinEditor.tsx" "apps/web/src/components/sessions/score-strategies/__tests__/BinaryWinEditor.test.tsx"
git commit -m "fix(sessions): #3196 BinaryWinEditor 44px touch rows"
```

---

### Task 3: ObjectivesEditor — 44px chips

**Files:**
- Modify: `apps/web/src/components/sessions/score-strategies/ObjectivesEditor.tsx:99-110`
- Test: `apps/web/src/components/sessions/score-strategies/__tests__/ObjectivesEditor.test.tsx`

**Interfaces:**
- Consumes: nothing new. Existing checkbox testid: `obj-<playerId>-<objective>`.
- Produces: nothing new.

- [ ] **Step 1: Write the failing test** — append inside the `describe('ObjectivesEditor', …)` block. Use the same `availableObjectives` value the file's other tests pass (inspect the top of the test file; if they use e.g. `['Templi', 'Maggioranze']`, reuse it):

```tsx
  it('gives each objective chip a 44px touch target', () => {
    render(
      <ObjectivesEditor
        players={PLAYERS}
        availableObjectives={OBJECTIVES}
        onChange={vi.fn()}
      />
    );
    const chip = screen.getByTestId(`obj-p1-${OBJECTIVES[0]}`).closest('label');
    expect(chip).toHaveClass('min-h-[44px]');
  });
```

(Reuse the existing `PLAYERS` and objectives constant already defined in the test file; if the objectives constant has a different name, use it — do not redeclare.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/ObjectivesEditor.test.tsx"`
Expected: FAIL — chip label lacks `min-h-[44px]`.

- [ ] **Step 3: Write minimal implementation** — at `ObjectivesEditor.tsx`, replace the chip `<label>` className (`:101`) and add a size class to the checkbox (`:103-107`):

```tsx
                  <label
                    key={objective}
                    className="inline-flex min-h-[44px] items-center gap-2 rounded-md border border-border-strong px-3 py-2 text-sm md:min-h-0 md:gap-1 md:px-2 md:py-1"
                  >
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={() => toggle(player.id, objective)}
                      data-testid={testId}
                      className="size-5 md:size-4"
                    />
                    <span>{objective}</span>
                  </label>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/ObjectivesEditor.test.tsx"`
Expected: PASS (all tests, including pre-existing).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/sessions/score-strategies/ObjectivesEditor.tsx" "apps/web/src/components/sessions/score-strategies/__tests__/ObjectivesEditor.test.tsx"
git commit -m "fix(sessions): #3196 ObjectivesEditor 44px chips"
```

---

### Task 4: RankingEditor — 44px handle + mobile up/down arrows

**Files:**
- Modify: `apps/web/src/components/sessions/score-strategies/RankingEditor.tsx` (`RankableItemProps` `:55-60`, `RankableItem` `:62-99`, `RankingEditor` `:101-148`)
- Test: `apps/web/src/components/sessions/score-strategies/__tests__/RankingEditor.test.tsx`

**Interfaces:**
- Consumes: `arrayMove` (already imported from `@dnd-kit/sortable`).
- Produces (internal to this file, used only by `RankableItem`): `RankableItemProps` gains `isFirst: boolean`, `isLast: boolean`, `onMoveUp: () => void`, `onMoveDown: () => void`. New testids: `ranking-up-<id>`, `ranking-down-<id>`.

- [ ] **Step 1: Write the failing test** — the test file currently imports `{ render, screen }`; change that import line to add `fireEvent`:

```tsx
import { fireEvent, render, screen } from '@testing-library/react';
```

Then append inside the `describe('RankingEditor', …)` block:

```tsx
  it('enlarges the drag handle to a 44px touch target', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} />);
    const handle = screen.getByTestId('ranking-handle-p1');
    expect(handle).toHaveClass('min-h-[44px]');
    expect(handle).toHaveClass('min-w-[44px]');
  });

  it('reorders via the mobile up arrow', () => {
    const onChange = vi.fn();
    render(<RankingEditor players={PLAYERS} onChange={onChange} />);
    fireEvent.click(screen.getByTestId('ranking-up-p2'));
    expect(onChange).toHaveBeenLastCalledWith({
      positions: [
        { playerId: 'p2', position: 1 },
        { playerId: 'p1', position: 2 },
        { playerId: 'p3', position: 3 },
      ],
    });
  });

  it('reorders via the mobile down arrow', () => {
    const onChange = vi.fn();
    render(<RankingEditor players={PLAYERS} onChange={onChange} />);
    fireEvent.click(screen.getByTestId('ranking-down-p1'));
    expect(onChange).toHaveBeenLastCalledWith({
      positions: [
        { playerId: 'p2', position: 1 },
        { playerId: 'p1', position: 2 },
        { playerId: 'p3', position: 3 },
      ],
    });
  });

  it('disables up on the first item and down on the last', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} />);
    expect(screen.getByTestId('ranking-up-p1')).toBeDisabled();
    expect(screen.getByTestId('ranking-down-p3')).toBeDisabled();
  });

  it('disables reorder arrows when the disabled prop is set', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} disabled />);
    expect(screen.getByTestId('ranking-up-p2')).toBeDisabled();
    expect(screen.getByTestId('ranking-down-p2')).toBeDisabled();
  });

  it('hides the reorder arrows on desktop (md:hidden wrapper)', () => {
    render(<RankingEditor players={PLAYERS} onChange={vi.fn()} />);
    // The md:hidden lives on the wrapper div, not the buttons.
    expect(screen.getByTestId('ranking-up-p2').closest('div')).toHaveClass('md:hidden');
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/RankingEditor.test.tsx"`
Expected: FAIL — no `ranking-up-*`/`ranking-down-*` elements, handle lacks 44px classes.

- [ ] **Step 3: Write minimal implementation**

3a. Extend `RankableItemProps` (`:55-60`):

```tsx
interface RankableItemProps {
  id: string;
  displayName: string;
  position: number;
  isFirst: boolean;
  isLast: boolean;
  onMoveUp: () => void;
  onMoveDown: () => void;
  disabled?: boolean;
}
```

3b. Replace the `RankableItem` function body's destructure + returned `<li>` markup (`:62-99`) — enlarge the handle and add a mobile-only arrow group:

```tsx
function RankableItem({
  id,
  displayName,
  position,
  isFirst,
  isLast,
  onMoveUp,
  onMoveDown,
  disabled,
}: RankableItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id,
    disabled,
  });

  return (
    <li
      ref={setNodeRef}
      style={{
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.5 : 1,
      }}
      className="flex items-center gap-3 rounded-md border border-border bg-card p-3"
      data-testid={`ranking-item-${id}`}
    >
      <span
        className="w-8 font-mono text-lg font-bold tabular-nums"
        data-testid={`ranking-position-${id}`}
      >
        {position}
      </span>
      <span className="flex-1 font-medium">{displayName}</span>
      <button
        type="button"
        {...attributes}
        {...listeners}
        disabled={disabled}
        aria-label={`Trascina ${displayName}`}
        data-testid={`ranking-handle-${id}`}
        className="inline-flex min-h-[44px] min-w-[44px] cursor-grab items-center justify-center disabled:cursor-not-allowed md:min-h-0 md:min-w-0"
      >
        ⋮⋮
      </button>
      <div className="flex gap-1 md:hidden">
        <button
          type="button"
          onClick={onMoveUp}
          disabled={disabled || isFirst}
          aria-label={`Sposta ${displayName} su`}
          data-testid={`ranking-up-${id}`}
          className="inline-flex min-h-[44px] min-w-[44px] items-center justify-center rounded-md border border-border disabled:opacity-40"
        >
          ▲
        </button>
        <button
          type="button"
          onClick={onMoveDown}
          disabled={disabled || isLast}
          aria-label={`Sposta ${displayName} giù`}
          data-testid={`ranking-down-${id}`}
          className="inline-flex min-h-[44px] min-w-[44px] items-center justify-center rounded-md border border-border disabled:opacity-40"
        >
          ▼
        </button>
      </div>
    </li>
  );
}
```

3c. In `RankingEditor` add the `move` handler (after the `handleDragEnd` definition, before `playerMap`):

```tsx
  const move = (index: number, dir: -1 | 1) => {
    setIds(prev => {
      const target = index + dir;
      if (target < 0 || target >= prev.length) return prev;
      return arrayMove(prev, index, target);
    });
  };
```

3d. Pass the new props at the `<RankableItem …>` call site (`:136-142`):

```tsx
              <RankableItem
                key={id}
                id={id}
                displayName={player.displayName}
                position={idx + 1}
                isFirst={idx === 0}
                isLast={idx === ids.length - 1}
                onMoveUp={() => move(idx, -1)}
                onMoveDown={() => move(idx, 1)}
                disabled={disabled}
              />
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies/__tests__/RankingEditor.test.tsx"`
Expected: PASS (all tests, including the 6 pre-existing ones).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/sessions/score-strategies/RankingEditor.tsx" "apps/web/src/components/sessions/score-strategies/__tests__/RankingEditor.test.tsx"
git commit -m "fix(sessions): #3196 RankingEditor 44px handle + touch reorder"
```

---

### Task 5: Verify — full editor suite + lint + typecheck

**Files:** none (verification only).

- [ ] **Step 1: Run the whole score-strategies suite**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions/score-strategies"`
Expected: PASS — all 4 editors' tests green (new + pre-existing).

- [ ] **Step 2: Run the dispatcher + host consumers (regression)**

Run: `cd apps/web && pnpm exec vitest run "src/components/sessions" "src/app/(authenticated)/sessions/[id]/live/_components/__tests__/ScoreTabContent.test.tsx" "src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx"`
Expected: PASS — `PolymorphicScoreEditor`, `ScoreTabContent`, `SessionLiveView` unaffected.

- [ ] **Step 3: Lint the 4 changed source files**

Run: `cd apps/web && pnpm exec eslint "src/components/sessions/score-strategies/PointsEditor.tsx" "src/components/sessions/score-strategies/BinaryWinEditor.tsx" "src/components/sessions/score-strategies/ObjectivesEditor.tsx" "src/components/sessions/score-strategies/RankingEditor.tsx"`
Expected: 0 errors (test files are eslint-ignored — a "File ignored" warning on them is fine).

- [ ] **Step 4: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors (tsc exits 0).

---

## Self-Review

**Spec coverage** — every spec §4 change maps to a task: Points (T1), BinaryWin (T2), Objectives (T3), Ranking handle + arrows (T4); §5 testing folded per-task + T5 regression; §7 DoD covered by T1-T5. No gaps.

**Placeholder scan** — Task 3's objectives constant is intentionally deferred to the test file's existing value (the implementer reuses it rather than guessing); every other step has complete code. No TBD/TODO.

**Type consistency** — `move(index, dir)`, `isFirst`/`isLast`/`onMoveUp`/`onMoveDown` on `RankableItemProps`, and testids `ranking-up-<id>`/`ranking-down-<id>` are used consistently between the T4 test and implementation. `onChange` payload shapes in T4 tests match the existing `RankingScoreData` positions contract.
