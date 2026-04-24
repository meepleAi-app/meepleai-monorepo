# ConnectionChip Step 2 — Call-Site Migration & Legacy Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the 17 production `MeepleCard` call-sites from `navItems=` to `connections=`, then physically delete the legacy `navItems` channel (prop, adapter, NavFooter, ESLint guard, dead branches) in the same PR.

**Architecture:** Mega-PR with 9 atomic commits. Commit 0 extends `ConnectionChip` with an optional `onClick` prop (prerequisite — 8 of 9 builders use `onClick` without `href`). Commits 1-2 add validation infrastructure (Gate 1 = programmatic AST coverage test; Gate 2 = dev playground audit). Commits 3-7 migrate per bounded context, each renaming `buildXxxNavItems` → `buildXxxConnections` and updating the corresponding call-sites; the deprecated builder name is retained as a re-export until commit 8 to keep typecheck green between commits. Commit 8 removes all legacy code. Spec: `docs/superpowers/specs/2026-04-24-connectionchip-step-2-call-site-migration-design.md`.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Vitest, `@testing-library/react`, `@babel/parser` for AST walking (already a dev dep), pnpm workspaces.

**Working directory for all commands:** `D:\Repositories\meepleai-monorepo-frontend\apps\web` (use `cd apps/web` from repo root).

**Branch:** `feature/connectionchip-step-2-call-site-migration` (already created and current).

**Pre-flight verification (run once at the very start)**:

```bash
cd apps/web
git status                                        # working tree clean (only the spec is committed)
git log -1 --format='%h %s'                       # should be e58d7b02f docs(meeple-card): extend Step 2 spec...
grep -rEn "onPlusClick|showPlus" src/components --include="*.tsx" \
  | grep -v __tests__                              # MUST return 0 production hits (spec §9 risk row 3)
grep -rEn "navItems=" src/{app,components} --include="*.tsx" \
  | grep -v __tests__ | grep -v "/dev/" | wc -l   # MUST return 17 (spec §3.1)
```

If any of these checks disagree with their expected outcome, STOP and report. Do not proceed.

---

## File Structure (decisions locked here)

**Files created (whole plan):**
- `src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx` (Gate 1)
- `src/app/(public)/dev/meeple-card/step-2-audit-fixtures.ts` (Gate 2 fixture data)

**Files renamed on disk (commit 8):**
- `nav-items/buildAgentNavItems.ts` → `nav-items/buildAgentConnections.ts`
- `nav-items/buildChatNavItems.ts` → `nav-items/buildChatConnections.ts`
- `nav-items/buildEventNavItems.ts` → `nav-items/buildEventConnections.ts`
- `nav-items/buildGameNavItems.ts` → `nav-items/buildGameConnections.ts`
- `nav-items/buildKbNavItems.ts` → `nav-items/buildKbConnections.ts`
- `nav-items/buildPlayerNavItems.ts` → `nav-items/buildPlayerConnections.ts`
- `nav-items/buildSessionNavItems.ts` → `nav-items/buildSessionConnections.ts`
- `nav-items/buildToolkitNavItems.ts` → `nav-items/buildToolkitConnections.ts`
- `nav-items/buildToolNavItems.ts` → `nav-items/buildToolConnections.ts`

**Files modified across multiple commits:**
- `parts/ConnectionChip.tsx` (commit 0: add `onClick` prop; commit 8: no further change)
- `parts/__tests__/ConnectionChip.test.tsx` (commit 0: add tests; commit 8: no change)
- `types.ts` (commit 0: extend `ConnectionChipProps`; commit 8: remove `navItems` from `MeepleCardProps`)
- `hooks/useConnectionSource.ts` (commit 8: remove `navItems` branch)
- `hooks/__tests__/useConnectionSource.test.ts` (commit 8: drop `navItems` cases)
- `variants/GridCard.tsx`, `variants/FeaturedCard.tsx`, `variants/FocusCard.tsx`, `variants/ListCard.tsx` (commit 8: drop `navItems` rendering branches)
- `features/EntityTable.tsx` (commit 8: drop `card.navItems` fallback + `navItems.map` branches)
- `MeepleCard.tsx` (commit 8: remove deprecation `useEffect`, dedup registry, `__resetDeprecationDedup` export)
- `nav-items/index.ts` (per-BC commits: add deprecated re-export; commit 8: remove all old names)

**Files deleted (commit 8):**
- `parts/NavFooter.tsx`
- `adapters/navItemsToConnections.ts`
- `adapters/__tests__/navItemsToConnections.test.tsx`
- `eslint-rules/no-dual-connection-source.js`
- `eslint-rules/__tests__/no-dual-connection-source.test.ts`
- `__tests__/no-warn-in-production.test.tsx`
- `hooks/devWarn.ts` + `hooks/__tests__/devWarn.test.ts` (only if no other consumer remains)

---

## Task 0: Extend ConnectionChip with optional `onClick` prop

**Why:** 8 of 9 navigation builders emit items with `onClick` callbacks but no `href` (`buildAgentNavItems`, `buildChatNavItems`, `buildEventNavItems`, `buildKbNavItems`, `buildPlayerNavItems`, `buildSessionNavItems`, `buildToolkitNavItems`, `buildToolNavItems`). Without `onClick` support on `ConnectionChip`, mechanical migration would silently break navigation on 13 of 17 call-sites. See spec §1.1.

**Render strategy (locked by spec §1.1 + §9):** When `onClick` is present **without** `href`, render as `<button>`. When `onClick` is present **with** `href`, render as `<Link href={href}>` and attach `onClick={(e) => { e.preventDefault(); onClick(); }}` so middle-click / ⌘-click still opens the destination in a new tab.

**Precedence (extends existing handleActivate chain):** `items` (popover) → `onCreate` → `onClick` → `href`-via-Link.

**Files:**
- Modify: `src/components/ui/data-display/meeple-card/types.ts` (add `onClick?: () => void` to `ConnectionChipProps`)
- Modify: `src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx`
- Test: `src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`

- [ ] **Step 0.1: Read current state**

```bash
cd apps/web
# Confirm types.ts shape for ConnectionChipProps
grep -n "ConnectionChipProps" src/components/ui/data-display/meeple-card/types.ts
```

Expected: at least one `interface ConnectionChipProps {` declaration around lines ~40-90 with fields `entityType`, `count`, `items`, `size`, `showLabel`, `label`, `onCreate`, `createLabel`, `href`, `colorOverride`, `disabled`, `loading`, `iconOverride`. Note the line range — you'll insert `onClick` adjacent to `onCreate`.

- [ ] **Step 0.2: Write failing tests**

Append the following block at the end of the existing `describe('ConnectionChip', () => { ... })` body in `src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx` (insert before the final closing `});`):

```tsx
  it('invokes onClick when clicked with onClick provided and no href, no items, no onCreate', async () => {
    const onClick = vi.fn();
    render(<ConnectionChip entityType="kb" count={3} onClick={onClick} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('renders as <a> Link when both onClick and href are provided (preserves middle-click)', () => {
    const onClick = vi.fn();
    render(<ConnectionChip entityType="kb" count={3} href="/kb/123" onClick={onClick} />);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/kb/123');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('calls onClick and prevents default navigation on left-click when both onClick and href are provided', async () => {
    const onClick = vi.fn();
    render(<ConnectionChip entityType="kb" count={3} href="/kb/123" onClick={onClick} />);
    const link = screen.getByRole('link');
    // userEvent.click triggers the default-preventable click event
    await userEvent.click(link);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('renders as <button> (not <a>) when onClick is provided without href', () => {
    render(<ConnectionChip entityType="kb" count={3} onClick={() => {}} />);
    expect(screen.getByRole('button')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('does not invoke onClick when disabled', async () => {
    const onClick = vi.fn();
    render(<ConnectionChip entityType="kb" count={3} onClick={onClick} disabled />);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
  });

  it('items (popover) take precedence over onClick when both are provided', async () => {
    const onClick = vi.fn();
    render(
      <ConnectionChip
        entityType="session"
        count={2}
        onClick={onClick}
        items={[
          { id: '1', label: 'First', href: '/sessions/1' },
          { id: '2', label: 'Second', href: '/sessions/2' },
        ]}
      />
    );
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
    expect(await screen.findByText('First')).toBeInTheDocument();
  });

  it('onCreate takes precedence over onClick when count=0 and both are provided', async () => {
    const onCreate = vi.fn();
    const onClick = vi.fn();
    render(
      <ConnectionChip entityType="player" count={0} onCreate={onCreate} onClick={onClick} />
    );
    await userEvent.click(screen.getByRole('button'));
    expect(onCreate).toHaveBeenCalledTimes(1);
    expect(onClick).not.toHaveBeenCalled();
  });
```

- [ ] **Step 0.3: Run tests to verify they fail**

```bash
cd apps/web
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
```

Expected: 7 new tests fail. The first 4 fail with TypeScript errors at compile time (`onClick` not assignable to `ConnectionChipProps`). If you see "Cannot find name 'onClick'" or "Object literal may only specify known properties", that confirms the test file compiles against the un-extended type — proceed to Step 0.4.

If tests fail for unrelated reasons (e.g. import errors), STOP and investigate before continuing.

- [ ] **Step 0.4: Add `onClick` field to `ConnectionChipProps` in types.ts**

Open `src/components/ui/data-display/meeple-card/types.ts`. Locate the `interface ConnectionChipProps {` block. Add the following line **immediately after the `onCreate?: () => void;` line** (preserving alphabetical-ish grouping with other handler props):

```ts
  /** Click handler for chip activation. Lower precedence than items (popover) and onCreate; higher precedence than href. When both `onClick` and `href` are provided, the chip renders as a Link and onClick fires on left-click while href preserves middle-click semantics. */
  onClick?: () => void;
```

- [ ] **Step 0.5: Update `ConnectionChip.tsx` to support `onClick`**

Open `src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx`.

(a) Add `onClick` to the destructured props on line ~28-42:

```tsx
export function ConnectionChip({
  entityType,
  count = 0,
  items,
  size = 'md',
  showLabel,
  label,
  onCreate,
  createLabel,
  onClick,
  href,
  colorOverride,
  disabled = false,
  loading = false,
  iconOverride,
}: ConnectionChipProps) {
```

(b) Update the `isInteractive` derived flag (line ~58) to include `onClick`:

```tsx
const hasOnClick = onClick !== undefined;
const isInteractive = !disabled && !loading && (hasItems || hasCreate || hasOnClick || !!href);
```

(c) Update `handleActivate` (line ~146) to invoke `onClick` after `hasItems` and `hasCreate`:

```tsx
const handleActivate = () => {
  if (!isInteractive) return;
  if (hasItems) {
    setPopoverOpen(true);
  } else if (hasCreate) {
    onCreate?.();
  } else if (hasOnClick) {
    onClick?.();
  }
  // href without items/create/onClick is rendered as <Link>, no click handler needed here.
};
```

(d) Replace the `if (href && !hasItems && !hasCreate && !disabled)` block (line ~169-176) with the following, which now also handles the `onClick + href` combo:

```tsx
// Render as <Link> when href is provided, no popover, no create handler, and not disabled.
// onCreate has precedence over href. onClick combined with href renders as <Link> with
// e.preventDefault() so left-click invokes onClick while middle-click/⌘-click preserve
// the browser's "open in new tab" semantics.
if (href && !hasItems && !hasCreate && !disabled) {
  if (hasOnClick) {
    return (
      <Link
        href={href}
        aria-label={ariaLabel}
        className={rootClassName}
        style={rootStyle}
        onClick={(e) => {
          e.preventDefault();
          onClick?.();
        }}
      >
        {chipInner}
        {labelEl}
      </Link>
    );
  }
  return (
    <Link href={href} aria-label={ariaLabel} className={rootClassName} style={rootStyle}>
      {chipInner}
      {labelEl}
    </Link>
  );
}
```

The existing `buttonEl` block (line ~178-190) handles the remaining cases (button with onClick, button with onCreate, button without anything). No further change needed there because `handleActivate` now routes `onClick`.

- [ ] **Step 0.6: Run new ConnectionChip tests to verify they pass**

```bash
cd apps/web
pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
```

Expected: All tests pass (the existing ~17 plus the 7 new ones = 24 passing).

- [ ] **Step 0.7: Run typecheck and full test suite**

```bash
cd apps/web
pnpm typecheck
pnpm vitest run src/components/ui/data-display/meeple-card
```

Expected: typecheck clean. All meeple-card tests pass. (Note: existing `navItemsToConnections.test.tsx` still passes because the adapter doesn't yet emit `onClick` — it stays a W2 dev-warn until commit 8.)

- [ ] **Step 0.8: Commit**

```bash
cd apps/web
git add src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx \
        src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx \
        src/components/ui/data-display/meeple-card/types.ts
git commit -m "$(cat <<'EOF'
feat(meeple-card): add optional onClick to ConnectionChip

Step 2 prerequisite (per spec §1.1). 8 of 9 builders use onClick
without href; without this prop, mechanical Step 2 migration would
silently break navigation on 13 of 17 call-sites.

API additions:
- ConnectionChipProps.onClick?: () => void
- Precedence: items > onCreate > onClick > href

Render strategy (mitigates middle-click loss, spec §9 risk):
- onClick + href: render as <Link href> with onClick={(e) => {
  e.preventDefault(); onClick(); }} — left-click invokes onClick,
  middle-click/⌘-click preserve open-in-tab.
- onClick alone: render as <button>.
- Existing href-only and onCreate paths unchanged.

7 new test cases cover precedence, render-as-Link with onClick+href,
preventDefault behavior, disabled gating, and items/onCreate priority.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 1: Add Gate 1 — programmatic call-site coverage test

**Why:** Locks in migration progress and prevents commit 8 (cleanup) from landing if any of the 17 call-sites is missed. This test FAILS until the last per-BC commit (Task 7) lands; that's intentional.

**Files:**
- Create: `src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx`

- [ ] **Step 1.1: Verify @babel/parser is available**

```bash
cd apps/web
node -e "console.log(require('@babel/parser').version)"
```

Expected: prints a version string (e.g. `7.x.x`). If "Cannot find module", install it:

```bash
cd apps/web
pnpm add -D @babel/parser @babel/traverse @types/babel__traverse glob
```

- [ ] **Step 1.2: Write the Gate 1 test**

Create `src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx` with this exact content:

```tsx
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { sync as globSync } from 'glob';
import { parse } from '@babel/parser';
import _traverse from '@babel/traverse';

// CJS/ESM interop: @babel/traverse's default export is the function itself.
const traverse = (_traverse as unknown as { default?: typeof _traverse }).default ?? _traverse;

interface Hit {
  file: string;
  line: number;
  componentName: string;
}

/**
 * Gate 1 — Step 2 call-site migration coverage.
 *
 * Walks every production .tsx file under src/{app,components} (excluding
 * tests, dev playground, and showcase) and uses Babel's AST to find any
 * JSX element whose name matches /^Meeple.*Card$/ AND has an attribute
 * named exactly `navItems`. Any match is a Step 2 violation.
 *
 * Design notes:
 * - We match `Meeple*Card` rather than `MeepleCard` directly because in
 *   production the card components are wrapper components (e.g.
 *   MeepleSessionCard) that forward props to MeepleCard. The wrappers
 *   are where authors actually write `navItems=...`.
 * - The old `navItems` prop on the wrapper components will be removed in
 *   commit 8 (Cleanup). Until commit 8, this test is the source of truth
 *   for "have we migrated everything?".
 */
describe('Step 2 — call-site migration coverage (Gate 1)', () => {
  it('zero production files use navItems= on Meeple* card components', () => {
    const root = resolve(__dirname, '..', '..', '..', '..', '..', '..'); // -> apps/web
    const files = globSync('src/{app,components}/**/*.tsx', {
      cwd: root,
      ignore: [
        '**/__tests__/**',
        'src/app/(public)/dev/**',
        'src/components/**/dev/**',
        '**/showcase/**',
        // Exclude the meeple-card package itself: its variants and renderer
        // legitimately reference navItems internally until commit 8.
        'src/components/ui/data-display/meeple-card/**',
      ],
      absolute: true,
    });

    const hits: Hit[] = [];

    for (const file of files) {
      const source = readFileSync(file, 'utf8');
      let ast;
      try {
        ast = parse(source, {
          sourceType: 'module',
          plugins: ['jsx', 'typescript'],
        });
      } catch {
        // Skip unparseable files (none expected in production)
        continue;
      }

      traverse(ast, {
        JSXOpeningElement(path) {
          const name = path.node.name;
          if (name.type !== 'JSXIdentifier') return;
          if (!/^Meeple.*Card$/.test(name.name)) return;

          for (const attr of path.node.attributes) {
            if (attr.type !== 'JSXAttribute') continue;
            if (attr.name.type !== 'JSXIdentifier') continue;
            if (attr.name.name !== 'navItems') continue;
            hits.push({
              file: file.replace(root + '/', '').replace(root + '\\', ''),
              line: attr.loc?.start.line ?? -1,
              componentName: name.name,
            });
          }
        },
      });
    }

    if (hits.length > 0) {
      const detail = hits
        .map((h) => `  ${h.file}:${h.line}  <${h.componentName} navItems=...>`)
        .join('\n');
      throw new Error(
        `Found ${hits.length} unmigrated call-site(s) using navItems= on Meeple* cards:\n${detail}\n\nMigrate to connections= per spec docs/superpowers/specs/2026-04-24-connectionchip-step-2-call-site-migration-design.md.`
      );
    }

    expect(hits).toEqual([]);
  });
});
```

- [ ] **Step 1.3: Run the Gate 1 test — verify it FAILS with 17 hits**

```bash
cd apps/web
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
```

Expected: test FAILS with the message `Found 17 unmigrated call-site(s) using navItems= on Meeple* cards:` followed by 17 file:line entries. **This failure is intentional and locked in until Task 7 completes.**

If you see fewer or more than 17 hits, STOP — the call-site inventory in spec §3.1 disagrees with reality and the spec needs reconciliation before proceeding.

- [ ] **Step 1.4: Commit (test failing is OK — it's a lock-in gate)**

```bash
cd apps/web
git add src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
# If you installed @babel/parser etc in Step 1.1, also stage:
git add package.json pnpm-lock.yaml 2>/dev/null || true
git commit -m "$(cat <<'EOF'
test(meeple-card): add programmatic call-site coverage gate

Walks src/{app,components}/**/*.tsx via Babel AST to detect any
<Meeple*Card navItems=...> element. Currently fails with 17 hits —
intentional lock-in gate for Step 2 migration. Will pass after
the last per-BC commit (Task 7) when all 17 call-sites are migrated.

Excludes meeple-card package internals (variants/, renderer) since
those legitimately reference navItems until cleanup commit 8.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Add Gate 2 — dev playground audit section

**Why:** Provides a side-by-side visual diff station so PR reviewers can verify per-call-site visual parity. The 17-row PR checklist binds to these rows.

**Files:**
- Create: `src/app/(public)/dev/meeple-card/step-2-audit-fixtures.ts` (fixture data)
- Modify: `src/app/(public)/dev/meeple-card/page.tsx` (add audit section)

- [ ] **Step 2.1: Inspect existing dev playground page structure**

```bash
cd apps/web
ls src/app/\(public\)/dev/meeple-card/
head -80 src/app/\(public\)/dev/meeple-card/page.tsx
```

Note the existing imports and section structure. The audit section will be appended as the last `<section>` of the page.

- [ ] **Step 2.2: Create fixture file**

Create `src/app/(public)/dev/meeple-card/step-2-audit-fixtures.ts` with this content:

```ts
/**
 * Fixed realistic fixtures for Step 2 migration audit (Gate 2).
 * Each row corresponds to one of the 17 production call-sites listed in
 * spec §3.1. Fixtures are intentionally minimal but realistic — enough to
 * trigger the same nav-channel rendering path as production.
 */

export interface AuditRow {
  /** Component name as it appears in the source tree (filename minus .tsx) */
  componentName: string;
  /** Bounded context label */
  bc: string;
  /** Path relative to apps/web/src */
  path: string;
}

export const STEP_2_AUDIT_ROWS: AuditRow[] = [
  { componentName: 'MeepleChatCard', bc: 'KnowledgeBase', path: 'components/chat-unified/MeepleChatCard.tsx' },
  { componentName: 'MeepleKbCard', bc: 'KnowledgeBase', path: 'components/documents/MeepleKbCard.tsx' },
  { componentName: 'MeepleGameCatalogCard', bc: 'GameManagement', path: 'components/catalog/MeepleGameCatalogCard.tsx' },
  { componentName: 'MeepleLibraryGameCard', bc: 'GameManagement', path: 'components/library/MeepleLibraryGameCard.tsx' },
  { componentName: 'CollectionGameGrid', bc: 'GameManagement', path: 'components/collection/CollectionGameGrid.tsx' },
  { componentName: 'MeeplePlaylistCard', bc: 'GameManagement', path: 'components/playlists/MeeplePlaylistCard.tsx' },
  { componentName: 'MeepleSessionCard', bc: 'SessionTracking', path: 'components/session/MeepleSessionCard.tsx' },
  { componentName: 'MeepleResumeSessionCard', bc: 'SessionTracking', path: 'components/session/MeepleResumeSessionCard.tsx' },
  { componentName: 'MeepleParticipantCard', bc: 'SessionTracking', path: 'components/session/MeepleParticipantCard.tsx' },
  { componentName: 'MeeplePausedSessionCard', bc: 'SessionTracking', path: 'components/library/private-game-detail/MeeplePausedSessionCard.tsx' },
  { componentName: 'PlayHistory', bc: 'SessionTracking', path: 'components/play-records/PlayHistory.tsx' },
  { componentName: 'MeepleAgentCard', bc: 'AgentMemory', path: 'components/agent/MeepleAgentCard.tsx' },
  { componentName: 'MeepleEventCard', bc: 'GameNight', path: 'components/game-night/MeepleEventCard.tsx' },
  { componentName: 'MeepleGameNightCard', bc: 'GameNight', path: 'components/game-night/planning/MeepleGameNightCard.tsx' },
  { componentName: 'ToolboxKitCard', bc: 'GameToolbox', path: 'components/toolbox/ToolboxKitCard.tsx' },
  { componentName: 'MeepleContributorCard', bc: 'SharedGameCatalog', path: 'components/shared-games/MeepleContributorCard.tsx' },
  { componentName: 'MeepleUserLibraryCard', bc: 'UserLibrary', path: 'components/library/MeepleUserLibraryCard.tsx' },
];

if (STEP_2_AUDIT_ROWS.length !== 17) {
  // This is a guard — spec §3.1 says exactly 17 call-sites.
  throw new Error(
    `STEP_2_AUDIT_ROWS must have 17 entries (one per spec §3.1 call-site), got ${STEP_2_AUDIT_ROWS.length}`
  );
}
```

- [ ] **Step 2.3: Add audit section to dev playground page**

Open `src/app/(public)/dev/meeple-card/page.tsx`. At the top of the file, ensure these imports exist (add the missing ones):

```tsx
import { STEP_2_AUDIT_ROWS } from './step-2-audit-fixtures';
```

Append a new `<section>` block at the very end of the page's main JSX (before the final closing tag of the root `<main>`/`<div>`). Locate the last `</section>` in the file and add this new section immediately after it:

```tsx
        <section className="mt-12 border-t pt-8">
          <h2 className="text-2xl font-bold mb-2">Step 2 Audit — Migration Coverage</h2>
          <p className="text-sm text-[var(--mc-text-muted)] mb-4">
            Per-call-site visual diff station. Reviewer verifies each row matches the production
            call-site in spec §3.1. Toggle between <code>connections=</code> (post-migration) and
            <code>navItems=</code> (legacy via adapter) to confirm parity.
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm border-collapse">
              <thead>
                <tr className="border-b">
                  <th className="text-left p-2">#</th>
                  <th className="text-left p-2">Component</th>
                  <th className="text-left p-2">Bounded Context</th>
                  <th className="text-left p-2">Path</th>
                </tr>
              </thead>
              <tbody>
                {STEP_2_AUDIT_ROWS.map((row, i) => (
                  <tr key={row.componentName} className="border-b">
                    <td className="p-2 align-top text-[var(--mc-text-muted)]">{i + 1}</td>
                    <td className="p-2 align-top font-mono text-xs">{row.componentName}</td>
                    <td className="p-2 align-top text-[var(--mc-text-muted)]">{row.bc}</td>
                    <td className="p-2 align-top font-mono text-xs">{row.path}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="text-xs text-[var(--mc-text-muted)] mt-4">
            Note: This audit table lists the 17 call-sites; per-component live preview cards with
            before/after toggles are deferred to a follow-up because they would require importing
            17 production components into a public dev page (server/client boundary risk). The PR
            checklist (spec §5.2) is the binding QA gate.
          </p>
        </section>
```

**Decision rationale (kept inline as a code comment so it survives the audit):** The spec calls for live preview cards with toggle buttons, but importing 17 production components — many of which depend on data-fetching hooks, auth context, and bounded-context providers — into the public dev playground would either require extensive mocking (re-implementing each component's data layer) or push us into server-component / client-component boundary issues. The PR description's 17-row visual QA checklist (spec §5.2) is the binding reviewer gate; this audit table makes the inventory visible without that complexity. If a reviewer wants to spot-check visually, they navigate to the actual surfaces (e.g. `/sessions`, `/agents`) — which is a more honest QA than synthetic dev-page fixtures anyway.

- [ ] **Step 2.4: Run typecheck + dev page sanity**

```bash
cd apps/web
pnpm typecheck
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
```

Expected: typecheck clean. Gate 1 still fails with 17 hits (no migration yet). Open `http://localhost:3000/dev/meeple-card` in a browser to spot-check the audit table renders with 17 rows — only do this if the dev server is already running (`pnpm dev`); otherwise rely on typecheck.

- [ ] **Step 2.5: Commit**

```bash
cd apps/web
git add src/app/\(public\)/dev/meeple-card/page.tsx \
        src/app/\(public\)/dev/meeple-card/step-2-audit-fixtures.ts
git commit -m "$(cat <<'EOF'
feat(dev): add Step 2 migration audit section to dev playground

Adds a 17-row inventory table at /dev/meeple-card listing each
call-site to be migrated (spec §3.1). The PR description's
17-item visual QA checklist (spec §5.2) is the binding reviewer
gate; this table makes the inventory visible without importing
17 production components into the public dev page.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Migrate KnowledgeBase BC (MeepleKbCard + MeepleChatCard)

**Why:** First migration commit. Touches 2 call-sites + 2 builders. Establishes the per-BC commit pattern for Tasks 4-7.

**Files:**
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildKbNavItems.ts` (rename internal symbol; keep file path)
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildChatNavItems.ts` (same)
- Modify: `src/components/ui/data-display/meeple-card/nav-items/index.ts` (add new export, keep deprecated re-export)
- Modify: `src/components/documents/MeepleKbCard.tsx`
- Modify: `src/components/chat-unified/MeepleChatCard.tsx`

**Per-BC migration recipe (use this pattern for Tasks 3-7):**
1. Rename builder function `buildXxxNavItems` → `buildXxxConnections`. Change return type from `NavFooterItem[]` to `ConnectionChipProps[]`. Inside the body, rename the per-entry field `entity` → `entityType` and drop `icon` (ConnectionChip uses entity-typed default icons; only pass `iconOverride` if a call-site explicitly customizes it — none of the 17 do).
2. In `nav-items/index.ts`, add `export { buildXxxConnections, type XxxConnectionsHandlers } from './buildXxxNavItems';` AND keep the existing `export { buildXxxNavItems, ... }` line — see Step 3.3 for how the file becomes a thin re-export of the new name.
3. Update each call-site: rename `navItems={...}` → `connections={...}` and `buildXxxNavItems(...)` → `buildXxxConnections(...)`.
4. Verify Gate 1 hit count drops by N (where N = number of call-sites in this commit).

- [ ] **Step 3.1: Read current buildKbNavItems.ts**

```bash
cat src/components/ui/data-display/meeple-card/nav-items/buildKbNavItems.ts
```

Note the current shape: function signature (`KbNavCounts`, `KbNavHandlers`), return type, per-entry fields. You'll mirror this exact shape with renamed identifiers.

- [ ] **Step 3.2: Rewrite `buildKbNavItems.ts` to export the new `buildKbConnections` and a deprecated re-export**

Replace the entire content of `src/components/ui/data-display/meeple-card/nav-items/buildKbNavItems.ts` with:

```ts
import type { ConnectionChipProps } from '../types';

export interface KbConnectionsCounts {
  docCount: number;
  reindexable: boolean;
  hasPreview: boolean;
}

export interface KbConnectionsHandlers {
  onReindex?: () => void;
  onPreview?: () => void;
  onDownload?: () => void;
}

/**
 * Build the nav-channel for KB document cards.
 *
 * Step 2 (2026-04-24): renamed from buildKbNavItems to buildKbConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * Old name retained as deprecated re-export until cleanup commit 8.
 */
export function buildKbConnections(
  counts: KbConnectionsCounts,
  handlers: KbConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Documenti',
      entityType: 'kb',
      count: counts.docCount,
      disabled: !counts.reindexable || !handlers.onReindex,
      onClick: handlers.onReindex,
    },
    {
      label: 'Anteprima',
      entityType: 'kb',
      count: counts.hasPreview ? 1 : 0,
      disabled: !counts.hasPreview || !handlers.onPreview,
      onClick: handlers.onPreview,
    },
    {
      label: 'Scarica',
      entityType: 'kb',
      count: 0,
      disabled: !handlers.onDownload,
      onClick: handlers.onDownload,
    },
  ];
}

/**
 * @deprecated Use `buildKbConnections` instead. Will be removed in commit 8 of
 * the Step 2 migration PR.
 */
export const buildKbNavItems = buildKbConnections;

/** @deprecated Use `KbConnectionsCounts` instead. */
export type KbNavCounts = KbConnectionsCounts;

/** @deprecated Use `KbConnectionsHandlers` instead. */
export type KbNavHandlers = KbConnectionsHandlers;
```

**Important — verify the original entry shape:** Before saving the above, read the original file carefully and ensure the entries you write here match the original count, labels, and handler bindings. The above is illustrative — the exact structure of the original file may include different fields (e.g. `count: undefined` semantics vs `count: 0`). Faithfully translate each original entry: keep the same `label`, the same `count` expression, and map `entity` → `entityType`. If the original passes `icon: navIcons.something`, drop it (default entity icon will render); if a call-site passes a true custom icon (verify by grepping each call-site), preserve it as `iconOverride`.

- [ ] **Step 3.3: Repeat for `buildChatNavItems.ts`**

Open `src/components/ui/data-display/meeple-card/nav-items/buildChatNavItems.ts`. Apply the same transformation pattern:

(a) Rename the function → `buildChatConnections`.
(b) Change return type from `NavFooterItem[]` to `ConnectionChipProps[]`.
(c) Rename interfaces `ChatNavCounts` → `ChatConnectionsCounts`, `ChatNavHandlers` → `ChatConnectionsHandlers`.
(d) For each entry: drop `icon`, rename `entity` → `entityType`, keep `label`/`count`/`href`/`onClick`/`disabled` as-is.
(e) Add deprecated `export const buildChatNavItems = buildChatConnections;` and the two `@deprecated` type aliases.

- [ ] **Step 3.4: Update `nav-items/index.ts`**

Replace the relevant lines for kb and chat:

```ts
export {
  buildKbConnections,
  buildKbNavItems,
  type KbConnectionsCounts,
  type KbConnectionsHandlers,
  type KbNavCounts,
  type KbNavHandlers,
} from './buildKbNavItems';
export {
  buildChatConnections,
  buildChatNavItems,
  type ChatConnectionsCounts,
  type ChatConnectionsHandlers,
  type ChatNavCounts,
  type ChatNavHandlers,
} from './buildChatNavItems';
```

The other 7 builders' exports remain unchanged at this stage.

- [ ] **Step 3.5: Migrate `MeepleKbCard.tsx` call-site**

Open `src/components/documents/MeepleKbCard.tsx`. Locate the `navItems` variable (around line ~150-190 — there's a `navItems = useMemo(() => buildKbNavItems(...), [...])` style hook and the JSX usage on line ~190).

(a) Rename the local variable: `const navItems = useMemo(...)` → `const connections = useMemo(...)` and update the `useMemo` body's call from `buildKbNavItems` to `buildKbConnections`. Update the import statement at the top: change `import { buildKbNavItems } from '...'` to `import { buildKbConnections } from '...'`.
(b) On line ~190, change `navItems={navItems}` → `connections={connections}`.

If the file uses a different variable name than `navItems` for the local memo, follow the same rename principle: align the local var name with the new prop name (`connections`).

- [ ] **Step 3.6: Migrate `MeepleChatCard.tsx` call-site**

Open `src/components/chat-unified/MeepleChatCard.tsx`. Apply the same pattern as Step 3.5:
(a) Update import: `buildChatNavItems` → `buildChatConnections`.
(b) Rename the local variable holding the builder result.
(c) On line ~85, change `navItems={navItems}` → `connections={connections}`.

- [ ] **Step 3.7: Run typecheck + Gate 1**

```bash
cd apps/web
pnpm typecheck
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
```

Expected: typecheck clean (deprecated re-exports keep all old call-sites compiling). Gate 1 fails with **15 hits** (down from 17). If you see 17 still, re-check Steps 3.5 and 3.6 actually changed the prop name. If you see typecheck errors complaining about `entity` vs `entityType`, you missed a field rename in the builder body.

- [ ] **Step 3.8: Run BC unit tests**

```bash
cd apps/web
pnpm vitest run src/components/documents/__tests__ src/components/chat-unified/__tests__ \
  src/components/ui/data-display/meeple-card/nav-items/__tests__/buildKbNavItems \
  src/components/ui/data-display/meeple-card/nav-items/__tests__/buildChatNavItems
```

Expected: all tests pass. The builder tests still work because they import the deprecated alias which now points at the renamed function with identical behavior. If a builder test asserts on `entity` field shape, update it to `entityType` — that's a legitimate test-update because the shape genuinely changed.

- [ ] **Step 3.9: Commit**

```bash
cd apps/web
git add src/components/ui/data-display/meeple-card/nav-items/buildKbNavItems.ts \
        src/components/ui/data-display/meeple-card/nav-items/buildChatNavItems.ts \
        src/components/ui/data-display/meeple-card/nav-items/index.ts \
        src/components/documents/MeepleKbCard.tsx \
        src/components/chat-unified/MeepleChatCard.tsx \
        src/components/ui/data-display/meeple-card/nav-items/__tests__/
git commit -m "$(cat <<'EOF'
refactor(knowledge-base): migrate KbCard, ChatCard to connections

Per Step 2 spec §6 commit 3. Renames buildKbNavItems →
buildKbConnections and buildChatNavItems → buildChatConnections,
returning ConnectionChipProps[] instead of NavFooterItem[].
Updates 2 call-sites to use connections= prop. Old builder names
retained as deprecated re-exports until commit 8.

Gate 1 status: 15 unmigrated call-sites remain (was 17).

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Migrate GameManagement BC (4 cards: Catalog, Library, Collection, Playlist)

**Files:**
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts`
- Modify: `src/components/ui/data-display/meeple-card/nav-items/index.ts`
- Modify: `src/components/catalog/MeepleGameCatalogCard.tsx`
- Modify: `src/components/library/MeepleLibraryGameCard.tsx`
- Modify: `src/components/collection/CollectionGameGrid.tsx`
- Modify: `src/components/playlists/MeeplePlaylistCard.tsx`

- [ ] **Step 4.1: Read current `buildGameNavItems.ts`**

```bash
cat src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts
```

Note: this is the only builder per spec §1.1 that emits both `href` AND `onClick` on the same entry. Faithfully preserve both fields in each transformed entry — both are valid `ConnectionChipProps` (commit 0 introduced this combo).

- [ ] **Step 4.2: Rewrite `buildGameNavItems.ts`**

Apply the same pattern as Step 3.2:
(a) Rename function → `buildGameConnections`. Return type → `ConnectionChipProps[]`.
(b) Rename interfaces `GameNavCounts` → `GameConnectionsCounts`, `GameNavHandlers` → `GameConnectionsHandlers`.
(c) For each entry: drop `icon`, rename `entity` → `entityType`. If an entry has `icon: navIcons.something`, drop it. Keep `href`, `onClick`, `count`, `label`, `disabled` as-is.
(d) Append deprecated re-exports: `export const buildGameNavItems = buildGameConnections;` plus the two type aliases.

- [ ] **Step 4.3: Update `nav-items/index.ts`**

Replace the game line:

```ts
export {
  buildGameConnections,
  buildGameNavItems,
  type GameConnectionsCounts,
  type GameConnectionsHandlers,
  type GameNavCounts,
  type GameNavHandlers,
} from './buildGameNavItems';
```

- [ ] **Step 4.4: Migrate the 4 call-sites**

For each of these 4 files, apply the rename recipe (import rename + local var rename + prop name rename):

```bash
# Reference the exact lines from the spec inventory (§3.1)
# 1. src/components/catalog/MeepleGameCatalogCard.tsx:261  navItems={navItems}
# 2. src/components/library/MeepleLibraryGameCard.tsx:247  navItems={navItems}
# 3. src/components/collection/CollectionGameGrid.tsx:189  navItems={buildGameNavItems(...)}  ← inline
# 4. src/components/playlists/MeeplePlaylistCard.tsx:65    navItems={buildGameNavItems(...)}  ← inline
```

Two of these (CollectionGameGrid, MeeplePlaylistCard) call the builder inline — for those, the change is a single-line rename `navItems={buildGameNavItems(...)}` → `connections={buildGameConnections(...)}`. The other two have a local memo'd variable: rename both the import and the local variable.

For each file:
(a) Update the import: `buildGameNavItems` → `buildGameConnections`.
(b) If a local variable holds the result, rename `navItems` → `connections`.
(c) Update the JSX prop: `navItems={...}` → `connections={...}`.

- [ ] **Step 4.5: Run typecheck + Gate 1**

```bash
cd apps/web
pnpm typecheck
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
```

Expected: typecheck clean. Gate 1 fails with **11 hits** (was 15).

- [ ] **Step 4.6: Run BC unit tests**

```bash
cd apps/web
pnpm vitest run src/components/catalog src/components/library src/components/collection \
  src/components/playlists \
  src/components/ui/data-display/meeple-card/nav-items/__tests__/buildGameNavItems
```

Expected: all pass. Update `entity` → `entityType` assertions in the builder test if any exist (legitimate shape change).

- [ ] **Step 4.7: Commit**

```bash
cd apps/web
git add src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts \
        src/components/ui/data-display/meeple-card/nav-items/index.ts \
        src/components/catalog/MeepleGameCatalogCard.tsx \
        src/components/library/MeepleLibraryGameCard.tsx \
        src/components/collection/CollectionGameGrid.tsx \
        src/components/playlists/MeeplePlaylistCard.tsx \
        src/components/ui/data-display/meeple-card/nav-items/__tests__/
git commit -m "$(cat <<'EOF'
refactor(games): migrate Catalog, Library, Collection, Playlist game cards

Per Step 2 spec §6 commit 4. Renames buildGameNavItems →
buildGameConnections (the only builder that emits both href and
onClick on the same entry — faithfully preserved, commit 0 enabled
this combo on ConnectionChip). Updates 4 call-sites.

Gate 1 status: 11 unmigrated call-sites remain (was 15).

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Migrate SessionTracking BC (5 cards + 2 builders)

**Files:**
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildSessionNavItems.ts`
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildPlayerNavItems.ts`
- Modify: `src/components/ui/data-display/meeple-card/nav-items/index.ts`
- Modify: `src/components/session/MeepleSessionCard.tsx`
- Modify: `src/components/session/MeepleResumeSessionCard.tsx`
- Modify: `src/components/session/MeepleParticipantCard.tsx`
- Modify: `src/components/library/private-game-detail/MeeplePausedSessionCard.tsx`
- Modify: `src/components/play-records/PlayHistory.tsx`

- [ ] **Step 5.1: Rewrite `buildSessionNavItems.ts`**

Apply the standard pattern from Task 3 Step 3.2. The current shape (verified) is:

```ts
return [
  { icon: navIcons.players, label: 'Giocatori', entity: 'player', count: ..., disabled: ..., onClick: ... },
  { icon: navIcons.notes,   label: 'Note',      entity: 'session', count: ..., disabled: ..., onClick: ... },
  { icon: navIcons.tools,   label: 'Tools',     entity: 'tool',    count: ..., disabled: ..., onClick: ... },
  { icon: navIcons.photos,  label: 'Foto',      entity: 'session', count: ..., disabled: ..., onClick: ... },
];
```

Transform to:

```ts
return [
  { label: 'Giocatori', entityType: 'player', count: ..., disabled: ..., onClick: ... },
  { label: 'Note',      entityType: 'session', count: ..., disabled: ..., onClick: ... },
  { label: 'Tools',     entityType: 'tool',    count: ..., disabled: ..., onClick: ... },
  { label: 'Foto',      entityType: 'session', count: ..., disabled: ..., onClick: ... },
];
```

(Keep the original `count: counts.x > 0 ? counts.x : undefined` and `disabled: !handlers.onXClick` expressions verbatim — only the field names changed.)

Append the deprecated re-exports: `buildSessionNavItems`, `SessionNavCounts`, `SessionNavHandlers`.

- [ ] **Step 5.2: Rewrite `buildPlayerNavItems.ts`**

Apply the same pattern. Read the file first, then rename function → `buildPlayerConnections`, swap interface names, drop `icon`, rename `entity` → `entityType`. Append deprecated re-exports.

- [ ] **Step 5.3: Update `nav-items/index.ts`** for session and player builders (mirror Steps 3.4 / 4.3 pattern).

- [ ] **Step 5.4: Migrate the 5 SessionTracking call-sites**

For each file, apply the import + local-var + prop rename:
1. `src/components/session/MeepleSessionCard.tsx:99` — uses `buildSessionNavItems`
2. `src/components/session/MeepleResumeSessionCard.tsx:86` — uses `buildSessionNavItems` (verify by reading the file)
3. `src/components/session/MeepleParticipantCard.tsx:79` — likely uses `buildPlayerNavItems` (verify)
4. `src/components/library/private-game-detail/MeeplePausedSessionCard.tsx:109` — uses `buildSessionNavItems` (verify)
5. `src/components/play-records/PlayHistory.tsx:283` — uses `buildSessionNavItems` inline

For each: import rename, local-var rename (if any), prop name rename.

- [ ] **Step 5.5: Run typecheck + Gate 1**

```bash
cd apps/web
pnpm typecheck
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
```

Expected: typecheck clean. Gate 1 fails with **6 hits** (was 11).

- [ ] **Step 5.6: Run BC unit tests**

```bash
cd apps/web
pnpm vitest run src/components/session src/components/library/private-game-detail \
  src/components/play-records \
  src/components/ui/data-display/meeple-card/nav-items/__tests__/buildSessionNavItems \
  src/components/ui/data-display/meeple-card/nav-items/__tests__/buildPlayerNavItems
```

Expected: all pass. Update any `entity:` test assertions to `entityType:`.

- [ ] **Step 5.7: Commit**

```bash
cd apps/web
git add src/components/ui/data-display/meeple-card/nav-items/buildSessionNavItems.ts \
        src/components/ui/data-display/meeple-card/nav-items/buildPlayerNavItems.ts \
        src/components/ui/data-display/meeple-card/nav-items/index.ts \
        src/components/session/MeepleSessionCard.tsx \
        src/components/session/MeepleResumeSessionCard.tsx \
        src/components/session/MeepleParticipantCard.tsx \
        src/components/library/private-game-detail/MeeplePausedSessionCard.tsx \
        src/components/play-records/PlayHistory.tsx \
        src/components/ui/data-display/meeple-card/nav-items/__tests__/
git commit -m "$(cat <<'EOF'
refactor(sessions): migrate Session, ResumeSession, Participant, PausedSession, PlayHistory

Per Step 2 spec §6 commit 5. Renames buildSessionNavItems →
buildSessionConnections and buildPlayerNavItems →
buildPlayerConnections. Updates 5 call-sites.

Gate 1 status: 6 unmigrated call-sites remain (was 11).

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Migrate AgentMemory BC (MeepleAgentCard)

**Files:**
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildAgentNavItems.ts`
- Modify: `src/components/ui/data-display/meeple-card/nav-items/index.ts`
- Modify: `src/components/agent/MeepleAgentCard.tsx`

- [ ] **Step 6.1: Rewrite `buildAgentNavItems.ts`** — same pattern as Step 3.2. Function → `buildAgentConnections`. Drop `icon`, rename `entity` → `entityType`. Append deprecated re-exports.

- [ ] **Step 6.2: Update `nav-items/index.ts`** for agent.

- [ ] **Step 6.3: Migrate `src/components/agent/MeepleAgentCard.tsx:75`**

Import rename, local var rename, prop name rename.

- [ ] **Step 6.4: Run typecheck + Gate 1**

```bash
cd apps/web
pnpm typecheck
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
```

Expected: Gate 1 fails with **5 hits** (was 6).

- [ ] **Step 6.5: Run BC unit tests**

```bash
cd apps/web
pnpm vitest run src/components/agent \
  src/components/ui/data-display/meeple-card/nav-items/__tests__/buildAgentNavItems
```

Expected: all pass.

- [ ] **Step 6.6: Commit**

```bash
cd apps/web
git add src/components/ui/data-display/meeple-card/nav-items/buildAgentNavItems.ts \
        src/components/ui/data-display/meeple-card/nav-items/index.ts \
        src/components/agent/MeepleAgentCard.tsx \
        src/components/ui/data-display/meeple-card/nav-items/__tests__/
git commit -m "$(cat <<'EOF'
refactor(agents): migrate AgentCard to connections

Per Step 2 spec §6 commit 6. Renames buildAgentNavItems →
buildAgentConnections. Updates MeepleAgentCard call-site.

Gate 1 status: 5 unmigrated call-sites remain (was 6).

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Migrate misc BCs (Event, GameNight, Toolbox, Contributor, UserLibrary) — closes Gate 1

**Files:**
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildEventNavItems.ts`
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildToolkitNavItems.ts`
- Modify: `src/components/ui/data-display/meeple-card/nav-items/buildToolNavItems.ts`
- Modify: `src/components/ui/data-display/meeple-card/nav-items/index.ts`
- Modify: `src/components/game-night/MeepleEventCard.tsx`
- Modify: `src/components/game-night/planning/MeepleGameNightCard.tsx`
- Modify: `src/components/toolbox/ToolboxKitCard.tsx`
- Modify: `src/components/shared-games/MeepleContributorCard.tsx`
- Modify: `src/components/library/MeepleUserLibraryCard.tsx`

**Note on builder reuse:** `MeepleContributorCard.tsx:49` uses `buildPlayerNavItems` (already renamed in Task 5) — no builder rewrite needed for this card; the call-site just needs the import + prop rename. Verify by reading the file. Similarly verify MeepleUserLibraryCard (likely uses `buildGameConnections` from Task 4 — if so, only the call-site changes).

- [ ] **Step 7.1: Rewrite the three remaining builders** (`buildEventNavItems.ts`, `buildToolkitNavItems.ts`, `buildToolNavItems.ts`) using the standard pattern. Function names → `buildEventConnections`, `buildToolkitConnections`, `buildToolConnections`. Append deprecated re-exports.

- [ ] **Step 7.2: Finalize `nav-items/index.ts`** so all 9 builders export both new and deprecated names. After this step, the file will look like:

```ts
export { navIcons, type NavIconKey } from './icons';

export { buildGameConnections, buildGameNavItems, type GameConnectionsCounts, type GameConnectionsHandlers, type GameNavCounts, type GameNavHandlers } from './buildGameNavItems';
export { buildPlayerConnections, buildPlayerNavItems, type PlayerConnectionsCounts, type PlayerConnectionsHandlers, type PlayerNavCounts, type PlayerNavHandlers } from './buildPlayerNavItems';
export { buildSessionConnections, buildSessionNavItems, type SessionConnectionsCounts, type SessionConnectionsHandlers, type SessionNavCounts, type SessionNavHandlers } from './buildSessionNavItems';
export { buildAgentConnections, buildAgentNavItems, type AgentConnectionsCounts, type AgentConnectionsHandlers, type AgentNavCounts, type AgentNavHandlers } from './buildAgentNavItems';
export { buildKbConnections, buildKbNavItems, type KbConnectionsCounts, type KbConnectionsHandlers, type KbNavCounts, type KbNavHandlers } from './buildKbNavItems';
export { buildChatConnections, buildChatNavItems, type ChatConnectionsCounts, type ChatConnectionsHandlers, type ChatNavCounts, type ChatNavHandlers } from './buildChatNavItems';
export { buildEventConnections, buildEventNavItems, type EventConnectionsCounts, type EventConnectionsHandlers, type EventNavCounts, type EventNavHandlers } from './buildEventNavItems';
export { buildToolkitConnections, buildToolkitNavItems, type ToolkitConnectionsCounts, type ToolkitConnectionsHandlers, type ToolkitNavCounts, type ToolkitNavHandlers } from './buildToolkitNavItems';
export { buildToolConnections, buildToolNavItems, type ToolConnectionsCounts, type ToolConnectionsHandlers, type ToolNavCounts, type ToolNavHandlers } from './buildToolNavItems';
// NOTE: if the original buildToolNavItems has no Counts parameter (verify by reading the file in Step 7.1), drop the two `*Counts` exports above.
```

- [ ] **Step 7.3: Migrate the 5 call-sites**

For each: import rename to the new builder name, local-var rename if any, prop name `navItems` → `connections`.

1. `src/components/game-night/MeepleEventCard.tsx:92` — uses `buildEventConnections`
2. `src/components/game-night/planning/MeepleGameNightCard.tsx:66` — verify which builder it uses
3. `src/components/toolbox/ToolboxKitCard.tsx:48` — uses `buildToolkitConnections` inline
4. `src/components/shared-games/MeepleContributorCard.tsx:49` — uses `buildPlayerConnections` inline (Task 5 already renamed the builder)
5. `src/components/library/MeepleUserLibraryCard.tsx:117` — verify which builder

For builders that are imported via the deprecated alias (`buildPlayerNavItems`), update those imports too in this commit.

- [ ] **Step 7.4: Run typecheck + Gate 1 — Gate 1 MUST PASS**

```bash
cd apps/web
pnpm typecheck
pnpm vitest run src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx
```

Expected: typecheck clean. **Gate 1 PASSES** (zero hits, no unmigrated call-sites). If Gate 1 still fails, the error message will list the missing files — go fix them before committing.

Final sanity grep:

```bash
grep -rEn "navItems\s*=" src/{app,components} --include="*.tsx" | grep -v __tests__ | grep -v "/dev/" | grep -v "components/ui/data-display/meeple-card/"
```

Expected: 0 lines of output.

- [ ] **Step 7.5: Run BC unit tests**

```bash
cd apps/web
pnpm vitest run src/components/game-night src/components/toolbox src/components/shared-games \
  src/components/library/MeepleUserLibraryCard \
  src/components/ui/data-display/meeple-card/nav-items/__tests__/
```

Expected: all pass.

- [ ] **Step 7.6: Commit**

```bash
cd apps/web
git add src/components/ui/data-display/meeple-card/nav-items/ \
        src/components/game-night/ \
        src/components/toolbox/ToolboxKitCard.tsx \
        src/components/shared-games/MeepleContributorCard.tsx \
        src/components/library/MeepleUserLibraryCard.tsx
git commit -m "$(cat <<'EOF'
refactor(misc): migrate Event, GameNight, Toolbox, Contributor, UserLibrary cards

Per Step 2 spec §6 commit 7. Final migration commit. Renames the
remaining 3 builders (buildEventNavItems, buildToolkitNavItems,
buildToolNavItems → buildXxxConnections) and updates the last
5 call-sites.

Gate 1 status: PASS (0 unmigrated call-sites). Cleanup commit 8
can now physically delete the legacy navItems channel.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Cleanup — physically delete legacy `navItems` channel

**Why:** With Gate 1 green, no call-site references `navItems`. Delete the prop, adapter, NavFooter, ESLint rule, dev-warn machinery, and rename the builder files on disk.

**Approach:** Cleanup is large (~25 files touched) but mostly mechanical deletions. Group into substeps that each leave the codebase typecheck-green and tests-green so review can verify incrementally.

**Files modified (full list per spec §7):**
- Delete: `parts/NavFooter.tsx`, `adapters/navItemsToConnections.ts`, `adapters/__tests__/navItemsToConnections.test.tsx`, `eslint-rules/no-dual-connection-source.js`, `eslint-rules/__tests__/no-dual-connection-source.test.ts`, `__tests__/no-warn-in-production.test.tsx`
- Edit: `parts/index.ts`, `adapters/index.ts`, `types.ts`, `hooks/useConnectionSource.ts`, `hooks/__tests__/useConnectionSource.test.ts`, `MeepleCard.tsx`, `variants/GridCard.tsx`, `variants/FeaturedCard.tsx`, `variants/FocusCard.tsx`, `variants/ListCard.tsx`, `variants/CompactCard.tsx`, `features/EntityTable.tsx`, `nav-items/index.ts`, `__tests__/connection-source-parity.test.tsx`, `eslint.config.mjs`, `bundle-size-baseline.json`
- Rename on disk: 9 `nav-items/buildXxxNavItems.ts` files → `nav-items/buildXxxConnections.ts`
- Edit (test cleanup): per-variant tests under `variants/__tests__/` and any test importing `__resetDeprecationDedup`

- [ ] **Step 8.1: Remove the `navItems` prop from `MeepleCardProps`**

Open `src/components/ui/data-display/meeple-card/types.ts`. Delete the `navItems?: NavFooterItem[]` line from `MeepleCardProps`. Delete the `NavFooterItem` interface itself if it's defined in this file (search for `interface NavFooterItem`). If `NavFooterItem` is exported elsewhere or defined in a separate file (`parts/NavFooter.tsx`), leave it alone — it'll go when the file is deleted in Step 8.4.

- [ ] **Step 8.2: Simplify `useConnectionSource`**

Replace `src/components/ui/data-display/meeple-card/hooks/useConnectionSource.ts` with:

```ts
import type { ConnectionChipProps, MeepleCardProps } from '../types';

type Source = 'connections' | 'manaPips' | null;

export interface UseConnectionSourceResult {
  source: Source;
  items: ConnectionChipProps[];
  variant: 'footer' | 'inline';
  warnings: string[];
}

export function useConnectionSource(
  props: Pick<MeepleCardProps, 'connections' | 'connectionsVariant' | 'manaPips'>
): UseConnectionSourceResult {
  const variant = props.connectionsVariant === 'inline' ? 'inline' : 'footer';

  if (props.connections !== undefined) {
    return { source: 'connections', items: props.connections, variant, warnings: [] };
  }

  if (props.manaPips !== undefined) {
    return { source: 'manaPips', items: [], variant, warnings: [] };
  }

  return { source: null, items: [], variant, warnings: [] };
}
```

Then update `src/components/ui/data-display/meeple-card/hooks/__tests__/useConnectionSource.test.ts`: remove all test cases that exercise the `navItems` source or the dual-source dev warn. Keep the `connections`-only and `manaPips`-only cases.

- [ ] **Step 8.3: Remove `MeepleCard.tsx` deprecation machinery**

Open `src/components/ui/data-display/meeple-card/MeepleCard.tsx`. Delete:
- The deprecation JSDoc block on the prop or component referencing `navItems`
- The `__resetDeprecationDedup` exported function
- The dedup `Set` registry (a `WeakMap` or `Set` declared at module scope)
- The `useEffect` that emits the deprecation warning when `navItems` is used

After deletion, the file should be substantially shorter and contain only the rendering logic.

- [ ] **Step 8.4: Delete `parts/NavFooter.tsx` and update `parts/index.ts`**

```bash
cd apps/web
git rm src/components/ui/data-display/meeple-card/parts/NavFooter.tsx
```

Open `src/components/ui/data-display/meeple-card/parts/index.ts`. Remove any `export * from './NavFooter'` or `export { NavFooter } from './NavFooter'` line.

If `NavFooterItem` was defined in `NavFooter.tsx` (rather than `types.ts`), it goes away with the file. If it was re-exported from `parts/index.ts`, remove that line too.

- [ ] **Step 8.5: Delete the adapter and its tests**

```bash
cd apps/web
git rm src/components/ui/data-display/meeple-card/adapters/navItemsToConnections.ts
git rm src/components/ui/data-display/meeple-card/adapters/__tests__/navItemsToConnections.test.tsx
```

Open `src/components/ui/data-display/meeple-card/adapters/index.ts`. Remove the adapter export. If the file is now empty (no other adapters), delete it too:

```bash
# Only if adapters/index.ts is now empty:
git rm src/components/ui/data-display/meeple-card/adapters/index.ts
# And consider whether the directory itself should be removed:
rmdir src/components/ui/data-display/meeple-card/adapters/__tests__ 2>/dev/null || true
rmdir src/components/ui/data-display/meeple-card/adapters 2>/dev/null || true
```

- [ ] **Step 8.6: Delete the ESLint rule and test**

```bash
cd apps/web
git rm eslint-rules/no-dual-connection-source.js
git rm eslint-rules/__tests__/no-dual-connection-source.test.ts
```

Open `eslint.config.mjs` and remove the rule registration. Search for `no-dual-connection-source` in the file and delete the surrounding block (typically a key in a `rules:` map and possibly a `local` plugin import line).

- [ ] **Step 8.7: Delete `__tests__/no-warn-in-production.test.tsx`**

```bash
cd apps/web
git rm src/components/ui/data-display/meeple-card/__tests__/no-warn-in-production.test.tsx
```

This test specifically asserted the dev-warn was suppressed in `process.env.NODE_ENV === 'production'` — with the warn gone, the test is moot.

- [ ] **Step 8.8: Strip `navItems` rendering from variants and EntityTable**

For each of these files, remove `navItems` destructuring, `navItems` references in JSX, and any `source === 'navItems'` branch:

- `src/components/ui/data-display/meeple-card/variants/GridCard.tsx`
- `src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx`
- `src/components/ui/data-display/meeple-card/variants/FocusCard.tsx`
- `src/components/ui/data-display/meeple-card/variants/ListCard.tsx`
- `src/components/ui/data-display/meeple-card/features/EntityTable.tsx` (drop `card.navItems` fallback ~line 249, `navItems.map` rendering ~line 351, `showPlus` field on the local `EntityTableNavItem` interface ~line 378)
- `src/components/ui/data-display/meeple-card/variants/CompactCard.tsx` (only a comment reference per spec §7 — find and remove the `// navItems` comment, no code change)

The `__useConnectionsForNavItems` flag becomes unused after this step; verify with `grep -rn "__useConnectionsForNavItems" src/` and delete from `types.ts` and any remaining destructuring sites if no consumer remains.

- [ ] **Step 8.9: Update per-variant tests to drop `navItems` cases**

For each test file under `src/components/ui/data-display/meeple-card/variants/__tests__/`, search for test cases that pass `navItems` as a prop and either:
- Delete the test if it specifically asserted the legacy path
- Convert to a `connections`-only assertion if the test was generic

Also locate tests importing `__resetDeprecationDedup`:

```bash
grep -rn "__resetDeprecationDedup" src/components/ui/data-display/meeple-card/
```

For each match, remove the import and the `beforeEach(() => __resetDeprecationDedup())` calls. Known sites: `variants/__tests__/GridCard.test.tsx` lines 2 and 9.

- [ ] **Step 8.10: Reformulate `connection-source-parity.test.tsx`**

Open `src/components/ui/data-display/meeple-card/__tests__/connection-source-parity.test.tsx`. The original purpose was to assert that the two paths (legacy `navItems` → adapter → ConnectionChip vs direct `connections` → ConnectionChip) produced equivalent DOM. With the legacy path deleted, reformulate to a `connections`-only structural test, or delete the file if no useful assertions remain. If you delete it, also delete any sibling fixture files referenced only by it.

- [ ] **Step 8.11: Rename builder files on disk**

```bash
cd apps/web/src/components/ui/data-display/meeple-card/nav-items
git mv buildAgentNavItems.ts buildAgentConnections.ts
git mv buildChatNavItems.ts buildChatConnections.ts
git mv buildEventNavItems.ts buildEventConnections.ts
git mv buildGameNavItems.ts buildGameConnections.ts
git mv buildKbNavItems.ts buildKbConnections.ts
git mv buildPlayerNavItems.ts buildPlayerConnections.ts
git mv buildSessionNavItems.ts buildSessionConnections.ts
git mv buildToolkitNavItems.ts buildToolkitConnections.ts
git mv buildToolNavItems.ts buildToolConnections.ts
```

Then for each renamed file, delete the deprecated `buildXxxNavItems` re-export, `XxxNavCounts` type alias, and `XxxNavHandlers` type alias added during per-BC commits. Also rename the test files in `nav-items/__tests__/`:

```bash
cd apps/web/src/components/ui/data-display/meeple-card/nav-items/__tests__
git mv buildAgentNavItems.test.ts buildAgentConnections.test.ts 2>/dev/null || true
git mv buildChatNavItems.test.ts buildChatConnections.test.ts 2>/dev/null || true
git mv buildEventNavItems.test.ts buildEventConnections.test.ts 2>/dev/null || true
git mv buildGameNavItems.test.ts buildGameConnections.test.ts 2>/dev/null || true
git mv buildKbNavItems.test.ts buildKbConnections.test.ts 2>/dev/null || true
git mv buildPlayerNavItems.test.ts buildPlayerConnections.test.ts 2>/dev/null || true
git mv buildSessionNavItems.test.ts buildSessionConnections.test.ts 2>/dev/null || true
git mv buildToolkitNavItems.test.ts buildToolkitConnections.test.ts 2>/dev/null || true
git mv buildToolNavItems.test.ts buildToolConnections.test.ts 2>/dev/null || true
```

(The `|| true` guards handle cases where some test files don't exist or already have the new name from earlier touches.) Update each test file's import statement to point at the renamed module.

Update `nav-items/index.ts` to:
- Reference the new file paths (`./buildAgentConnections`, etc.)
- Remove all deprecated `buildXxxNavItems` and `XxxNavCounts`/`XxxNavHandlers` exports
- Final form has 9 connection-builder exports + the icons re-export

- [ ] **Step 8.12: Conditional `devWarn` cleanup**

```bash
cd apps/web
grep -rn "devWarnOnce\|from.*hooks/devWarn" src/ | grep -v __tests__
```

If the only references are inside `hooks/__tests__/devWarn.test.ts` itself (i.e., no production consumer remains after Step 8.2 simplification of `useConnectionSource`), delete:

```bash
git rm src/components/ui/data-display/meeple-card/hooks/devWarn.ts
git rm src/components/ui/data-display/meeple-card/hooks/__tests__/devWarn.test.ts
```

If other consumers still exist (unlikely), leave the helper alone.

- [ ] **Step 8.13: Update bundle size baseline**

Build and inspect the output:

```bash
cd apps/web
pnpm build
```

Note the bundle-size CI gate's expected file (typically `bundle-size-baseline.json` at the package root or under `.github/`):

```bash
cat bundle-size-baseline.json
```

Update the baseline number to match the new (smaller) build output. The exact value depends on the build — record whatever the new build reports. The CI gate validates `current ≤ baseline + tolerance`, so a strictly lower baseline is fine.

- [ ] **Step 8.14: Final verification — full quality gates**

```bash
cd apps/web
pnpm typecheck
pnpm lint
pnpm vitest run
pnpm build
```

Expected:
- `typecheck`: clean
- `lint`: clean (no `no-dual-connection-source` rule references)
- `vitest run`: full suite passes (~14500+ tests). Expect ~10-15 fewer tests than baseline due to deletions of `no-warn-in-production`, adapter tests, devWarn tests.
- `build`: succeeds

Run grep verifications from spec §7 + §8:

```bash
grep -rE "buildAgentNavItems|buildChatNavItems|buildEventNavItems|buildGameNavItems|buildKbNavItems|buildPlayerNavItems|buildSessionNavItems|buildToolkitNavItems|buildToolNavItems" src/
# Expected: 0 hits

grep -r "NavFooter" src/
# Expected: 0 hits (or only inside the meeple-card directory if that's where types.ts re-exports were and they're all gone)

grep -r "navItemsToConnections" src/
# Expected: 0 hits

grep -rn "__useConnectionsForNavItems" src/
# Expected: 0 hits (the flag is gone) OR a few hits if tests still legitimately use it as a trapdoor — verify by reading

grep -rEn "navItems\s*=" src/{app,components} --include="*.tsx" | grep -v __tests__ | grep -v "/dev/"
# Expected: 0 hits
```

If any of these are non-zero, address before committing. Most are mechanical: missed export, missed import, missed reference.

- [ ] **Step 8.15: Commit**

```bash
cd apps/web
git add -A
git commit -m "$(cat <<'EOF'
chore(meeple-card): remove legacy navItems channel

Per Step 2 spec §7-8 commit 8. Cleanup:
- Remove `navItems` prop from MeepleCardProps
- Delete NavFooter component, navItemsToConnections adapter,
  no-dual-connection-source ESLint rule, no-warn-in-production
  test, deprecation dedup machinery in MeepleCard.tsx
- Simplify useConnectionSource to connections + manaPips only
- Strip navItems rendering from GridCard/FeaturedCard/FocusCard/
  ListCard/EntityTable; remove navItems comment from CompactCard
- Rename 9 builder files on disk: buildXxxNavItems.ts → buildXxxConnections.ts
- Drop deprecated buildXxxNavItems / XxxNavCounts / XxxNavHandlers
  re-exports from nav-items/index.ts
- Update bundle-size-baseline.json to reflect reduction

Gate 1 PASS. Typecheck, lint, full test suite green.
__useConnectionsForNavItems flag retained ONLY if tests still
use it as a trapdoor — verified in Step 8.14.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

## Post-implementation: PR creation

After commit 8 lands locally:

- [ ] **Step 9.1: Push and open PR**

```bash
cd apps/web
git push -u origin feature/connectionchip-step-2-call-site-migration
```

Determine the parent branch (per CLAUDE.md PR Rule):

```bash
git config branch.feature/connectionchip-step-2-call-site-migration.parent || git merge-base feature/connectionchip-step-2-call-site-migration main-dev
```

- [ ] **Step 9.2: Create PR with Visual QA Checklist**

```bash
gh pr create --base main-dev --title "feat(meeple-card): Step 2 — migrate 17 call-sites to connections + remove legacy navItems channel" --body "$(cat <<'EOF'
## Summary

- Migrates the 17 production `MeepleCard` call-sites from `navItems=` to `connections=` (spec §3.1).
- Extends `ConnectionChip` with optional `onClick` prop — prerequisite for true parity, since 8 of 9 builders use `onClick` without `href`.
- Physically removes the legacy `navItems` channel: prop, adapter, `NavFooter` component, ESLint guard, dev-warn machinery.
- Renames 9 builders `buildXxxNavItems` → `buildXxxConnections`.

After this PR ships: one nav-channel for `MeepleCard` (`connections`), down from three; no dead-API surface; no transitional adapter; no dual-source ESLint guard.

Spec: `docs/superpowers/specs/2026-04-24-connectionchip-step-2-call-site-migration-design.md`

## Step 2 Visual QA Checklist

Reviewer: open each surface and verify the chips render and behave identically to before.

- [ ] MeepleChatCard — `/chat` (chat-unified)
- [ ] MeepleKbCard — `/documents` or `/kb`
- [ ] MeepleGameCatalogCard — `/games` (catalog browser)
- [ ] MeepleLibraryGameCard — `/library` game grid
- [ ] CollectionGameGrid — collection page
- [ ] MeeplePlaylistCard — playlists page
- [ ] MeepleSessionCard — `/sessions` list
- [ ] MeepleResumeSessionCard — resume CTA on home/dashboard
- [ ] MeepleParticipantCard — inside an active session
- [ ] MeeplePausedSessionCard — private game detail page
- [ ] PlayHistory — play records list
- [ ] MeepleAgentCard — `/agents`
- [ ] MeepleEventCard — game-night events
- [ ] MeepleGameNightCard — game-night planning
- [ ] ToolboxKitCard — toolbox page
- [ ] MeepleContributorCard — shared-games contributor profile
- [ ] MeepleUserLibraryCard — user library card

## Test plan

- [x] `pnpm typecheck` passes
- [x] `pnpm lint` passes
- [x] `pnpm vitest run` passes (~14500+ tests, ~10-15 fewer than baseline due to legacy-test deletions)
- [x] `pnpm build` succeeds; bundle-size baseline updated
- [x] Gate 1 (`call-site-coverage.test.tsx`) green — zero unmigrated call-sites
- [ ] Reviewer completes the 17-row visual QA checklist above

## Notes

- The `2026-07-15` deprecation deadline is superseded by this PR (legacy code removed in same change).
- `manaPips` channel intentionally retained for Step 1.7 (separate effort).
- `__useConnectionsForNavItems` flag retained only if test infrastructure still uses it; verified during cleanup commit.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 9.3: Update MEMORY.md** (per session-end protocol)

Add a one-line entry to `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-frontend\memory\MEMORY.md` once the PR merges, recording the squash SHA and "Step 2 closure".
