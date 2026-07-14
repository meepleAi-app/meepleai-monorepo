# AST Gate Against Inline Card Re-implementation (Issue #2861 / C4) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Vitest AST gate that fails when a `*Card`-named component (outside the canonical meeple-card dirs) renders inline star-rating glyphs (`★`/`☆`) without composing `<MeepleCard>` — catching the raw-HTML entity-tile reimplementation that C1's import-boundary rule can't see.

**Architecture:** A single Vitest test (Babel `@babel/parser` + `@babel/traverse`, same pattern as `call-site-coverage.test.tsx`) exposes a pure `detectInlineStarCard(source, relPath)` helper, tested against inline fixtures AND run over every production `.tsx` under `src/{app,components}`. Two pre-existing hand-rolled star-cards are grandfathered via a path allowlist.

**Tech Stack:** Vitest · `@babel/parser` · `@babel/traverse` · `glob` · TypeScript.

**Spec:** `docs/superpowers/specs/2026-07-13-issue-2861-ast-gate-inline-card-design.md`

## Global Constraints

- Work on branch `feature/issue-2861-ast-gate-inline-card` (already created from `main-dev`); PR targets `main-dev`.
- Frontend paths under `apps/web/`. Run commands from `apps/web/`. Single test: `pnpm exec vitest run <path>`. `pnpm typecheck`, `pnpm lint`, `pnpm build`.
- Card-name regex: `/^[A-Z][A-Za-z0-9]*Card([A-Z][A-Za-z0-9]*)?$/`. Star regex: `/[★☆]/` (U+2605 / U+2606).
- Detection is per-file: file declares a `*Card` component AND contains a star glyph in a `StringLiteral`/`JSXText`/`TemplateLiteral` (AST — excludes comments) AND has NO `<MeepleCard>` JSX element AND is not allowlisted.
- Exclusions: `**/__tests__/**`, `src/app/(public)/dev/**`, `src/components/**/dev/**`, `**/showcase/**`, `src/components/ui/data-display/meeple-card/**`, `src/components/ui/data-display/extra-meeple-card/**`.
- `STAR_CARD_ALLOWLIST` = exactly `{ 'src/components/features/hub/HubGameCard.tsx', 'src/components/features/hub/HubToolkitCard.tsx' }` (the only 2 existing violations, enumerated & verified).
- Cover/badge are NOT gated. Commit format `type(scope): description` + `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

### Task 1: The AST gate + allowlist + self-verification + doc

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/__tests__/no-inline-card-reimplementation.test.tsx`
- Modify: `docs/for-developers/frontend/card-decision-table.md` (append a gate note)

**Interfaces:**
- Produces: the gate. No runtime exports consumed elsewhere.

- [ ] **Step 1: Write the gate test (fixtures fail first because the file doesn't exist)**

Create `apps/web/src/components/ui/data-display/meeple-card/__tests__/no-inline-card-reimplementation.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { sync as globSync } from 'glob';
import { parse } from '@babel/parser';
import _traverse from '@babel/traverse';

// CJS/ESM interop: @babel/traverse's default export is the function itself.
const traverse = (_traverse as unknown as { default?: typeof _traverse }).default ?? _traverse;

const CARD_NAME_RE = /^[A-Z][A-Za-z0-9]*Card([A-Z][A-Za-z0-9]*)?$/;
const STAR_RE = /[★☆]/;

/**
 * Grandfathered hand-rolled star-cards (pre-C4). Follow-up: convert to MeepleCard
 * adapters like MeepleCardGame in C1 (#2858) — tracked separately, out of C4 scope.
 * Paths are relative to apps/web/, normalized with forward slashes.
 */
const STAR_CARD_ALLOWLIST: ReadonlySet<string> = new Set([
  'src/components/features/hub/HubGameCard.tsx',
  'src/components/features/hub/HubToolkitCard.tsx',
]);

function normalize(p: string): string {
  return p.replace(/\\/g, '/');
}

interface Violation {
  component: string;
  line: number;
}

/**
 * A file violates C4 when it declares a `*Card` component, renders an inline
 * star glyph (in code, not a comment), and never composes <MeepleCard>.
 * Returns the first violation or null. Unparseable files return null.
 */
export function detectInlineStarCard(source: string, _relPath: string): Violation | null {
  let ast;
  try {
    ast = parse(source, { sourceType: 'module', plugins: ['jsx', 'typescript'] });
  } catch {
    return null;
  }

  let card: { name: string; line: number } | null = null;
  let starLine = -1;
  let rendersMeepleCard = false;

  const markCard = (name: string, line: number) => {
    if (!card) card = { name, line };
  };
  const markStar = (line: number) => {
    if (starLine < 0) starLine = line;
  };

  traverse(ast, {
    FunctionDeclaration(path) {
      const id = path.node.id;
      if (id && CARD_NAME_RE.test(id.name)) markCard(id.name, id.loc?.start.line ?? -1);
    },
    VariableDeclarator(path) {
      const id = path.node.id;
      const init = path.node.init;
      if (
        id.type === 'Identifier' &&
        CARD_NAME_RE.test(id.name) &&
        (init?.type === 'ArrowFunctionExpression' || init?.type === 'FunctionExpression')
      ) {
        markCard(id.name, id.loc?.start.line ?? -1);
      }
    },
    JSXOpeningElement(path) {
      const name = path.node.name;
      if (name.type === 'JSXIdentifier' && name.name === 'MeepleCard') rendersMeepleCard = true;
    },
    StringLiteral(path) {
      if (STAR_RE.test(path.node.value)) markStar(path.node.loc?.start.line ?? -1);
    },
    JSXText(path) {
      if (STAR_RE.test(path.node.value)) markStar(path.node.loc?.start.line ?? -1);
    },
    TemplateLiteral(path) {
      if (path.node.quasis.some(q => STAR_RE.test(q.value.raw ?? ''))) {
        markStar(path.node.loc?.start.line ?? -1);
      }
    },
  });

  if (card && starLine !== -1 && !rendersMeepleCard) {
    return { component: card.name, line: starLine };
  }
  return null;
}

describe('C4 — no inline card re-implementation (#2861)', () => {
  it('self-check: flags a *Card with inline star glyphs and no <MeepleCard>', () => {
    const src = `
      export function RogueGameCard() {
        return <div><span>{'★'}</span><span>☆</span></div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/RogueGameCard.tsx')).not.toBeNull();
  });

  it('self-check: does NOT flag a *Card that composes <MeepleCard>', () => {
    const src = `
      export function OkGameCard() {
        return <MeepleCard entity="game" title="x">{'★'}</MeepleCard>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/OkGameCard.tsx')).toBeNull();
  });

  it('self-check: does NOT flag a *Card with no star glyph', () => {
    const src = `
      export function PlainCard() {
        return <div>hello</div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/PlainCard.tsx')).toBeNull();
  });

  it('self-check: does NOT flag a non-*Card component with star glyphs', () => {
    const src = `
      export function StarRating() {
        return <div><span>★</span></div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/StarRating.tsx')).toBeNull();
  });

  it('self-check: does NOT flag a star glyph that lives only in a comment', () => {
    const src = `
      // renders a ★ rating elsewhere
      export function CommentedCard() {
        return <div>plain</div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/CommentedCard.tsx')).toBeNull();
  });

  it('production scan: no un-allowlisted *Card renders stars inline without <MeepleCard>', () => {
    // apps/web root
    const root = resolve(__dirname, '..', '..', '..', '..', '..', '..');

    // Path-drift safeguard: verify root is @meepleai/web.
    const pkgJsonPath = resolve(root, 'package.json');
    if (!existsSync(pkgJsonPath)) {
      throw new Error(`C4 path drift: ${root} has no package.json; fix the '..' count.`);
    }
    const pkgName = (JSON.parse(readFileSync(pkgJsonPath, 'utf8')) as { name?: string }).name;
    if (pkgName !== '@meepleai/web') {
      throw new Error(`C4 path drift: root ${root} is "${pkgName ?? '(unnamed)'}", expected "@meepleai/web".`);
    }

    const files = globSync('src/{app,components}/**/*.tsx', {
      cwd: root,
      ignore: [
        '**/__tests__/**',
        'src/app/(public)/dev/**',
        'src/components/**/dev/**',
        '**/showcase/**',
        'src/components/ui/data-display/meeple-card/**',
        'src/components/ui/data-display/extra-meeple-card/**',
      ],
      absolute: true,
    });

    // Files-scanned floor: catch a broken glob/cwd (vacuous pass).
    expect(files.length).toBeGreaterThan(50);

    const offenders: string[] = [];
    for (const file of files) {
      const rel = normalize(file).slice(normalize(file).indexOf('/src/') + 1); // -> src/...
      if (STAR_CARD_ALLOWLIST.has(rel)) continue;
      const v = detectInlineStarCard(readFileSync(file, 'utf8'), rel);
      if (v) offenders.push(`${rel}:${v.line}  <${v.component}> renders inline ★/☆ without <MeepleCard>`);
    }

    expect(
      offenders,
      offenders.length
        ? `These *Card components hand-roll a star rating instead of composing <MeepleCard>.\n` +
            `Compose <MeepleCard> (adapter), or use the shared <Stars> (@/components/ui/feedback/Stars)\n` +
            `for a non-entity rating. If genuinely intentional, add the path to STAR_CARD_ALLOWLIST\n` +
            `in this test with a reason.\n` +
            offenders.join('\n')
        : ''
    ).toEqual([]);
  });
});
```

- [ ] **Step 2: Run the test to verify it passes (self-checks + repo scan)**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/__tests__/no-inline-card-reimplementation.test.tsx`
Expected: PASS (5 self-checks + the production scan). The scan is green because the only 2 hits (`HubGameCard`, `HubToolkitCard`) are allowlisted.

- [ ] **Step 3: Prove the gate actually catches a violation (temporary de-allowlist)**

Temporarily remove `'src/components/features/hub/HubGameCard.tsx'` from `STAR_CARD_ALLOWLIST`, then run the test:
Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/__tests__/no-inline-card-reimplementation.test.tsx`
Expected: the production-scan test FAILS, listing `src/components/features/hub/HubGameCard.tsx:<line> <HubGameCard> renders inline ★/☆ without <MeepleCard>`. This confirms the gate is not vacuous.
Then RESTORE the allowlist entry and re-run:
Expected: PASS again.

- [ ] **Step 4: Typecheck**

Run: `pnpm typecheck`
Expected: PASS.

- [ ] **Step 5: Document the gate in the decision-table**

Append to `docs/for-developers/frontend/card-decision-table.md`:

```markdown
## Anti-reimplementation gates (#2858 C1 + #2861 C4)

Two complementary guards stop a new entity tile from being hand-rolled instead of
composing `MeepleCard`:

- **C1 — `local/no-standalone-card-renderer`** (ESLint, error): no value-import of
  `meeple-card/parts` or `/variants` from outside the canonical dir — you cannot
  re-assemble a card from the atomic parts.
- **C4 — `no-inline-card-reimplementation.test.tsx`** (Vitest AST gate): fails when a
  `*Card` component (outside the canonical dirs) renders inline star glyphs (`★`/`☆`)
  and never composes `<MeepleCard>` — catches the raw-HTML rating reimplementation.
  Existing hand-rolled star-cards (`HubGameCard`, `HubToolkitCard`) are grandfathered
  in `STAR_CARD_ALLOWLIST`; new ones must compose `<MeepleCard>` or use the shared
  `<Stars>` (`@/components/ui/feedback/Stars`).
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/no-inline-card-reimplementation.test.tsx \
        docs/for-developers/frontend/card-decision-table.md
git commit -m "$(cat <<'EOF'
test(meeple-card): AST gate against inline card re-implementation (#2861)

Vitest Babel gate: fails when a *Card component (outside the canonical dirs)
renders inline ★/☆ glyphs without composing <MeepleCard>. Complements C1's
import-boundary rule. HubGameCard + HubToolkitCard grandfathered (only 2 existing).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Final verification + PR

**Files:** none (verification only).

- [ ] **Step 1: Quality gates**

Run each and confirm PASS:
- `cd apps/web && pnpm exec vitest run src/components/ui/data-display/meeple-card/__tests__/no-inline-card-reimplementation.test.tsx`
- `cd apps/web && pnpm typecheck`
- `cd apps/web && pnpm lint`
- `cd apps/web && pnpm build`

- [ ] **Step 2: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2861-ast-gate-inline-card
gh pr create --base main-dev --title "test(meeple-card): AST gate against inline card re-implementation (#2861)" --body "$(cat <<'EOF'
Closes #2861 (C4 / ST7 of umbrella #2863).

## What
A Vitest Babel AST gate (`no-inline-card-reimplementation.test.tsx`) fails when a `*Card`-named component (outside the canonical meeple-card dirs) renders inline star glyphs (`★`/`☆`) and does not compose `<MeepleCard>` — the raw-HTML rating reimplementation that C1's import-boundary rule can't see. Cover/badge are intentionally NOT gated (too common; part-theft is covered by C1). Detection is per-file via Babel AST (excludes comments), with self-verifying inline fixtures. `HubGameCard` + `HubToolkitCard` (the only 2 existing hand-rolled star-cards, enumerated) are grandfathered in `STAR_CARD_ALLOWLIST` as follow-up adapter-conversion candidates.

## Design
`docs/superpowers/specs/2026-07-13-issue-2861-ast-gate-inline-card-design.md`. Complements C1 (`local/no-standalone-card-renderer`): C1 = can't import the parts; C4 = can't hand-roll the rating in a card.

## Verification
Gate test green (5 self-checks + repo scan), and de-allowlisting one entry proves the gate fails as intended. typecheck / lint / build green.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: After CI green + merge — close-out**

Update issue #2861 (state + DoD) and tick its box in umbrella #2863's Phase C checklist.

---

## Self-Review notes (author)

- **Spec coverage:** §3 gate → Task 1 Steps 1-4; §4 allowlist → Task 1 Step 1 (2 entries); §5 C1 relationship → doc note Step 5; §6 self-verification → Task 1 Step 1 (5 fixtures) + Step 3 (non-vacuous proof); §9 acceptance → Task 2 gates. All mapped.
- **Type consistency:** `detectInlineStarCard(source, relPath) → Violation | null` used consistently in fixtures and the scan; return logic is a single clean branch.
- **Non-vacuous:** Task 1 Step 3 temporarily de-allowlists one entry to prove the gate actually fails, then restores — guards against a gate that always passes.
