# ConnectionChip Step 1.6 — Renderer Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire `ConnectionChip` / `ConnectionChipStrip` into 5 MeepleCard variants via a central `useConnectionSource` hook + `navItems→connections` adapter, preserving DOM parity for existing call-sites and adding a dev-only deprecation warning.

**Architecture:** Branch by Abstraction. New hook centralises source precedence (`connections` > `navItems` > `manaPips`). Default renderer path for legacy `navItems` remains `<NavFooter>`; an opt-in internal flag `__useConnectionsForNavItems` exercises the adapter path. ESLint rule forbids co-presence of sources.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Tailwind 4, Radix Popover (via ConnectionChipPopover), Vitest + RTL + @testing-library/user-event.

**Spec reference:** `docs/superpowers/specs/2026-04-23-connectionchip-step-1.6-renderer-integration.md`

**Branch:** `feature/meeplecard-connectionchip-step-1.6` (branched from `main-dev`)

---

## Task 1: Capture DOM regression baseline

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/renderer-integration-baseline.test.tsx`
- Fixture: reuse existing `__tests__/fixtures/navItems.ts` if present, else inline

- [ ] **Step 1.1: Write baseline test asserting current DOM counts**

For each of Grid / List / Featured / Focus, render with a 3-item `navItems` fixture and assert:
- number of `role="link"`
- number of `role="button"`
- array of `aria-label` strings on links
- presence (or absence) of `data-testid="nav-footer"` wrapper

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GridCard } from '../GridCard';
import { ListCard } from '../ListCard';
import { FeaturedCard } from '../FeaturedCard';
import { FocusCard } from '../FocusCard';

const mockNavItems3 = [
  { label: '3 sessioni', entity: 'session' as const, count: 3, href: '/s/1', icon: <i data-testid="icon-s" /> },
  { label: '2 KB docs',  entity: 'kb'      as const, count: 2, href: '/k/1', icon: <i data-testid="icon-k" /> },
  { label: 'Nuovo',      entity: 'player'  as const, count: 0, showPlus: true, onPlusClick: () => {}, icon: <i data-testid="icon-p" /> },
];

describe.each([
  ['GridCard',     GridCard],
  ['ListCard',     ListCard],
  ['FeaturedCard', FeaturedCard],
  ['FocusCard',    FocusCard],
])('%s baseline with navItems', (_name, Variant) => {
  it('renders the expected nav DOM (baseline)', () => {
    render(<Variant entity="game" title="X" navItems={mockNavItems3} />);
    const links = screen.queryAllByRole('link');
    const buttons = screen.queryAllByRole('button');
    // These counts become the baseline; failing later means DOM drift.
    expect({ links: links.length, buttons: buttons.length }).toMatchInlineSnapshot();
    expect(links.map(l => l.getAttribute('aria-label'))).toMatchInlineSnapshot();
  });
});
```

- [ ] **Step 1.2: Run tests to let Vitest fill the inline snapshots**

Run: `pnpm --filter @meepleai/web test renderer-integration-baseline -u`
Expected: PASS. The `toMatchInlineSnapshot()` calls now contain the literal baseline values.

- [ ] **Step 1.3: Commit baseline**

```bash
git checkout -b feature/meeplecard-connectionchip-step-1.6
git add apps/web/src/components/ui/data-display/meeple-card/variants/__tests__/renderer-integration-baseline.test.tsx
git commit -m "test(meeple-card): capture DOM baseline for Grid/List/Featured/Focus navItems rendering"
```

---

## Task 2: Create `useConnectionSource` hook (TDD)

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/hooks/useConnectionSource.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/hooks/__tests__/useConnectionSource.test.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/hooks/index.ts` (create if absent)

- [ ] **Step 2.1: Write failing tests (Level A, cases A1–A7)**

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useConnectionSource, __resetWarnDedup } from '../useConnectionSource';

describe('useConnectionSource', () => {
  beforeEach(() => { __resetWarnDedup(); vi.restoreAllMocks(); });

  it('A1: returns source=null for empty props', () => {
    const { result } = renderHook(() => useConnectionSource({}));
    expect(result.current.source).toBeNull();
    expect(result.current.items).toEqual([]);
  });

  it('A2: connections=[] → source=connections, items=[]', () => {
    const { result } = renderHook(() => useConnectionSource({ connections: [] }));
    expect(result.current.source).toBe('connections');
    expect(result.current.items).toEqual([]);
  });

  it('A3: connections with items', () => {
    const cs = [{ entityType: 'session' as const, count: 1 }];
    const { result } = renderHook(() => useConnectionSource({ connections: cs }));
    expect(result.current.source).toBe('connections');
    expect(result.current.items).toEqual(cs);
  });

  it('A4: navItems=[] → source=null', () => {
    const { result } = renderHook(() => useConnectionSource({ navItems: [] }));
    expect(result.current.source).toBeNull();
  });

  it('A5: navItems with items → source=navItems', () => {
    const { result } = renderHook(() => useConnectionSource({ navItems: [{ label: 'x', entity: 'session', icon: null }] }));
    expect(result.current.source).toBe('navItems');
  });

  it('A6: manaPips → source=manaPips', () => {
    const { result } = renderHook(() => useConnectionSource({ manaPips: [{ state: 'ok' } as any] }));
    expect(result.current.source).toBe('manaPips');
  });

  it('A7: connections + navItems → connections wins + warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const { result } = renderHook(() =>
      useConnectionSource({ connections: [{ entityType: 'kb', count: 1 }], navItems: [{ label: 'y', entity: 'kb', icon: null }] })
    );
    expect(result.current.source).toBe('connections');
    expect(warn).toHaveBeenCalledWith(expect.stringMatching(/dual source|mix/i));
  });
});
```

- [ ] **Step 2.2: Run tests to verify they fail**

Run: `pnpm --filter @meepleai/web test useConnectionSource`
Expected: all 7 tests FAIL with "useConnectionSource is not a function".

- [ ] **Step 2.3: Implement the hook minimally**

```ts
// apps/web/src/components/ui/data-display/meeple-card/hooks/useConnectionSource.ts
import type { ConnectionChipProps, MeepleCardProps } from '../types';

type Source = 'connections' | 'navItems' | 'manaPips' | null;

export interface UseConnectionSourceResult {
  source: Source;
  items: ConnectionChipProps[];
  variant: 'footer' | 'inline';
  warnings: string[];
}

const warnedInstances = new WeakSet<object>();
export function __resetWarnDedup() { /* test-only helper */ (warnedInstances as any).clear?.(); }

function devWarn(msg: string) {
  if (process.env.NODE_ENV !== 'production') console.warn(msg);
}

export function useConnectionSource(props: Pick<MeepleCardProps, 'connections' | 'connectionsVariant' | 'navItems' | 'manaPips'>): UseConnectionSourceResult {
  const warnings: string[] = [];
  const variant = props.connectionsVariant === 'inline' ? 'inline' : 'footer';

  if (props.connections !== undefined) {
    if (props.navItems !== undefined || props.manaPips !== undefined) {
      const msg = '[MeepleCard] Dual source detected: `connections` takes precedence over `navItems`/`manaPips`. Remove one to silence this warning.';
      warnings.push(msg);
      devWarn(msg);
    }
    return { source: 'connections', items: props.connections, variant, warnings };
  }
  if (props.navItems && props.navItems.length > 0) {
    return { source: 'navItems', items: [], variant, warnings };
  }
  if (props.manaPips !== undefined) {
    return { source: 'manaPips', items: [], variant, warnings };
  }
  return { source: null, items: [], variant, warnings };
}
```

- [ ] **Step 2.4: Run tests to verify they pass**

Run: `pnpm --filter @meepleai/web test useConnectionSource`
Expected: all 7 PASS.

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/hooks/
git commit -m "feat(meeple-card): add useConnectionSource hook with precedence rules"
```

---

## Task 3: Add `iconOverride` to `ConnectionChipProps` + render it

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`

- [ ] **Step 3.1: Write failing test**

```tsx
it('renders iconOverride instead of default entity icon when provided', () => {
  const Custom = () => <svg data-testid="custom-icon" />;
  const { container } = render(<ConnectionChip entityType="kb" count={3} iconOverride={<Custom />} />);
  expect(container.querySelector('[data-testid="custom-icon"]')).toBeTruthy();
});
```

- [ ] **Step 3.2: Run to verify FAIL**

Run: `pnpm test ConnectionChip`
Expected: new test fails with "iconOverride prop type missing" or the custom icon not found in DOM.

- [ ] **Step 3.3: Add the field to `ConnectionChipProps`**

```ts
// types.ts — add to ConnectionChipProps
/**
 * Optional icon node to render instead of the default Lucide icon for `entityType`.
 * Used by the `navItems → connections` adapter to preserve custom icons.
 */
iconOverride?: import('react').ReactNode;
```

- [ ] **Step 3.4: Use `iconOverride` in ConnectionChip.tsx**

Replace `<Icon ... />` at L91 with:

```tsx
{iconOverride ?? (
  <Icon size={iconPx} strokeWidth={ENTITY_ICON_STROKE} aria-hidden="true" style={{ opacity: isEmpty ? (hasCreate ? 0.7 : 0.45) : 1 }} />
)}
```

And destructure `iconOverride` from props at L41.

- [ ] **Step 3.5: Run tests**

Run: `pnpm test ConnectionChip`
Expected: 19/19 PASS (18 existing + 1 new).

- [ ] **Step 3.6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
git commit -m "feat(meeple-card): add optional iconOverride to ConnectionChip"
```

---

## Task 4: Create `navItemsToConnections` adapter (TDD)

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/adapters/navItemsToConnections.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/adapters/__tests__/navItemsToConnections.test.ts`

- [ ] **Step 4.1: Write failing tests — one per warning code W1–W3, plus happy paths**

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { navItemsToConnections } from '../navItemsToConnections';

describe('navItemsToConnections', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('maps label/entity/count/href/disabled 1:1', () => {
    const out = navItemsToConnections([
      { label: 'L', entity: 'session', count: 3, href: '/s', disabled: true, icon: <i /> },
    ]);
    expect(out[0]).toMatchObject({ entityType: 'session', label: 'L', count: 3, href: '/s', disabled: true });
    expect(out[0].iconOverride).toBeDefined();
  });

  it('maps count=0 + showPlus + onPlusClick → onCreate', () => {
    const fn = vi.fn();
    const out = navItemsToConnections([{ label: 'L', entity: 'player', count: 0, showPlus: true, onPlusClick: fn, icon: null }]);
    expect(out[0].onCreate).toBe(fn);
  });

  it('W3: count>0 + onPlusClick → onCreate dropped + warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const fn = vi.fn();
    const out = navItemsToConnections([{ label: 'L', entity: 'player', count: 2, showPlus: true, onPlusClick: fn, icon: null }]);
    expect(out[0].onCreate).toBeUndefined();
    expect(warn).toHaveBeenCalledWith(expect.stringMatching(/onPlusClick.*dropped.*count>0/));
  });

  it('W2: onClick without href → warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    navItemsToConnections([{ label: 'L', entity: 'session', onClick: () => {}, icon: null }]);
    expect(warn).toHaveBeenCalledWith(expect.stringMatching(/onClick.*no href/));
  });
});
```

- [ ] **Step 4.2: Run to verify FAIL**

Run: `pnpm test navItemsToConnections`
Expected: all tests FAIL with "module not found".

- [ ] **Step 4.3: Implement the adapter**

```ts
// adapters/navItemsToConnections.ts
import type { ConnectionChipProps, NavFooterItem } from '../types';

function devWarn(msg: string) {
  if (process.env.NODE_ENV !== 'production') console.warn(msg);
}

export function navItemsToConnections(items: NavFooterItem[]): ConnectionChipProps[] {
  return items.map((it, idx) => {
    const count = it.count ?? 0;
    let onCreate: (() => void) | undefined;
    if (it.showPlus && it.onPlusClick) {
      if (count === 0) onCreate = it.onPlusClick;
      else devWarn(`[MeepleCard adapter] navItems[${idx}].onPlusClick was dropped: showPlus is rendered only when count>0 is false. Expose it via onCreate for count=0.`);
    }
    if (it.onClick && !it.href) {
      devWarn(`[MeepleCard adapter] navItems[${idx}].onClick has no href fallback; ConnectionChip doesn't expose an onClick slot without href. Consider adding href.`);
    }
    return {
      entityType: it.entity,
      label: it.label,
      count,
      href: it.href,
      disabled: it.disabled,
      onCreate,
      iconOverride: it.icon,
    };
  });
}
```

- [ ] **Step 4.4: Run — all PASS**

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/adapters/
git commit -m "feat(meeple-card): add navItems→connections adapter with lossy-field warnings"
```

---

## Task 5: Wire `useConnectionSource` into MeepleCard dispatcher + variants

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts` (add internal `__useConnectionsForNavItems`)
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/ListCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/FeaturedCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/CompactCard.tsx`
- Tests: `variants/__tests__/GridCard.test.tsx` (+ List, Featured, Focus, Compact)

- [ ] **Step 5.1: Add internal flag to types**

```ts
// MeepleCardProps — append (non-documented, internal)
/** @internal Opt-in: use ConnectionChipStrip for navItems-source rendering (A/B in tests). Remove in Step 2. */
__useConnectionsForNavItems?: boolean;
```

- [ ] **Step 5.2: Write failing tests — one `connections` path case per variant (from S2/S3/S6)**

Add to each variant's `__tests__` file:

```tsx
import { ConnectionChipStrip } from '../../parts/ConnectionChipStrip';

describe('GridCard connections path', () => {
  it('S2: connections=[] renders no nav DOM, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<GridCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(screen.queryByTestId('nav-footer')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(<GridCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />);
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });

  it('S3: navItems path emits one warn per instance (deduped)', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const props = { entity: 'game' as const, title: 'X', navItems: [{ label: 'L', entity: 'session' as const, icon: null }] };
    const { rerender } = render(<GridCard {...props} />);
    rerender(<GridCard {...props} />);
    expect(warn.mock.calls.filter(c => /navItems.*deprecated/.test(c[0]))).toHaveLength(1);
  });
});
```

Mirror for List/Featured/Focus; Compact uses S10 (connections ignored, no warn).

- [ ] **Step 5.3: Run — all FAIL (expected)**

- [ ] **Step 5.4: Update `MeepleCard.tsx` to emit deprecation warn once**

```tsx
// MeepleCard.tsx
import { useEffect } from 'react';

const deprecationWarned = new WeakSet<object>();
function emitNavItemsDeprecation(propsRef: object) {
  if (process.env.NODE_ENV === 'production') return;
  if (deprecationWarned.has(propsRef)) return;
  deprecationWarned.add(propsRef);
  console.warn(
    '[MeepleCard] The `navItems` prop is deprecated. Migrate to `connections` by 2026-07-15.\n' +
    '  See docs/superpowers/specs/2026-04-23-connectionchip-step-1.6-renderer-integration.md\n' +
    '  This warning is shown once per MeepleCard instance in development mode.'
  );
}
export const __resetDeprecationDedup = () => { (deprecationWarned as any).clear?.(); };

function MeepleCardImpl(props: MeepleCardProps) {
  useEffect(() => {
    if (props.navItems && props.navItems.length > 0 && props.connections === undefined) {
      emitNavItemsDeprecation(props);
    }
  }, [props]);
  const Variant = variantMap[props.variant ?? 'grid'];
  return <Variant {...props} />;
}
```

- [ ] **Step 5.5: In each variant, add a connections slot**

Pattern for GridCard (before `<NavFooter>`):

```tsx
const { source, items, variant: csVariant } = useConnectionSource(props);
// ...
{source === 'connections' && <ConnectionChipStrip items={items} variant={csVariant} />}
{source === 'navItems' && !props.__useConnectionsForNavItems && <NavFooter items={props.navItems!} size="md" />}
{source === 'navItems' && props.__useConnectionsForNavItems && (
  <ConnectionChipStrip items={navItemsToConnections(props.navItems!)} variant={csVariant} />
)}
```

Compact: only the `source === 'connections'` branch returns null (S10 — documented no-op). Manual addition to its JSX where existing `<ManaPips>` sits.

Focus: its inline chip row is replaced by the same dispatch; preserve the outer wrapper classes that set height/gap.

- [ ] **Step 5.6: Run variant tests — all PASS**

- [ ] **Step 5.7: Run Level C baseline test**

Expected: still PASS (DOM unchanged for default `navItems` path because `__useConnectionsForNavItems` is undefined).

- [ ] **Step 5.8: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/
git commit -m "feat(meeple-card): wire useConnectionSource + ConnectionChipStrip into 5 variants"
```

---

## Task 6: Add adapter-flag integration tests (S4, S5, S9)

**Files:**
- Modify: `variants/__tests__/GridCard.test.tsx` (S4)
- Modify: `variants/__tests__/FocusCard.test.tsx` (S9)
- Modify: `adapters/__tests__/navItemsToConnections.test.ts` (S5 already covered)

- [ ] **Step 6.1: Write S4 for Grid**

```tsx
it('S4: navItems adapter path renders ConnectionChipStrip with equivalent DOM', () => {
  render(
    <GridCard
      entity="game"
      title="X"
      navItems={[{ label: '3 sessioni', entity: 'session', count: 3, href: '/s', icon: <i data-testid="i1" /> }]}
      __useConnectionsForNavItems
    />
  );
  expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  expect(screen.getByTestId('i1')).toBeInTheDocument();
  expect(screen.queryByTestId('nav-footer')).toBeNull();
  expect(screen.getByRole('link', { name: /3.*session/i })).toBeInTheDocument();
});
```

- [ ] **Step 6.2: Write S9 for Focus**

```tsx
it('S9: FocusCard with adapter flag keeps 3 interactive elements, aria-labels preserved', () => {
  render(
    <FocusCard
      entity="game" title="X" __useConnectionsForNavItems
      navItems={[
        { label: '3 sessioni', entity: 'session', count: 3, href: '/s/1', icon: <i /> },
        { label: '2 KB',       entity: 'kb',      count: 2, href: '/k/1', icon: <i /> },
        { label: 'Nuovo',      entity: 'player',  count: 0, showPlus: true, onPlusClick: () => {}, icon: <i /> },
      ]}
    />
  );
  const interactive = [...screen.queryAllByRole('link'), ...screen.queryAllByRole('button')];
  expect(interactive).toHaveLength(3);
  const labels = interactive.map(el => el.getAttribute('aria-label')?.toLowerCase());
  expect(labels.filter(Boolean)).toHaveLength(3);
});
```

- [ ] **Step 6.3: Run — all PASS**

- [ ] **Step 6.4: Commit**

```bash
git commit -am "test(meeple-card): add adapter-flag integration tests for Grid/Focus"
```

---

## Task 7: Add `@deprecated` JSDoc to `navItems` and `manaPips`

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`

- [ ] **Step 7.1: Annotate `MeepleCardProps.navItems` and `MeepleCardProps.manaPips`**

```ts
/**
 * @deprecated Use `connections` instead. Migrate by 2026-07-15.
 * See docs/superpowers/specs/2026-04-23-connectionchip-step-1.6-renderer-integration.md
 */
navItems?: NavFooterItem[];

/**
 * @deprecated Use `connections` instead. Runtime migration deferred to Step 1.7.
 * Runtime rendering is unchanged in Step 1.6; this JSDoc raises awareness only.
 */
manaPips?: ManaPip[];
```

- [ ] **Step 7.2: Run typecheck**

Run: `pnpm --filter @meepleai/web typecheck`
Expected: PASS. TS will still accept usages; editors surface the deprecation strikethrough.

- [ ] **Step 7.3: Commit**

```bash
git commit -am "chore(meeple-card): mark navItems and manaPips as @deprecated (removal 2026-07-15)"
```

---

## Task 8: Create ESLint rule `meeple-card/no-dual-connection-source`

**Files:**
- Create: `apps/web/eslint-rules/index.js`
- Create: `apps/web/eslint-rules/no-dual-connection-source.js`
- Create: `apps/web/eslint-rules/__tests__/no-dual-connection-source.test.js`
- Modify: `apps/web/.eslintrc.json`

- [ ] **Step 8.1: Write the rule**

```js
// apps/web/eslint-rules/no-dual-connection-source.js
/** @type {import('eslint').Rule.RuleModule} */
module.exports = {
  meta: {
    type: 'problem',
    docs: { description: 'Disallow passing both `connections` and `navItems`/`manaPips` to <MeepleCard>.' },
    messages: {
      dualSource: 'Cannot mix `connections` with `navItems`/`manaPips` on the same MeepleCard. Pick one source.',
    },
    schema: [],
  },
  create(context) {
    return {
      JSXOpeningElement(node) {
        if (node.name.type !== 'JSXIdentifier') return;
        if (node.name.name !== 'MeepleCard') return;
        const attrNames = new Set(
          node.attributes
            .filter(a => a.type === 'JSXAttribute' && a.name?.type === 'JSXIdentifier')
            .map(a => a.name.name)
        );
        const hasConnections = attrNames.has('connections');
        const hasNav = attrNames.has('navItems');
        const hasMana = attrNames.has('manaPips');
        if (hasConnections && (hasNav || hasMana)) {
          context.report({ node, messageId: 'dualSource' });
        }
      },
    };
  },
};
```

- [ ] **Step 8.2: Write the plugin index**

```js
// apps/web/eslint-rules/index.js
module.exports = {
  rules: {
    'no-dual-connection-source': require('./no-dual-connection-source'),
  },
};
```

- [ ] **Step 8.3: Register the plugin in `.eslintrc.json`**

Add/merge:

```json
{
  "plugins": ["meeple-card"],
  "settings": { "meeple-card": {} },
  "rules": {
    "meeple-card/no-dual-connection-source": "error"
  }
}
```

Require local plugin via `.eslintrc.js` if `.eslintrc.json` is being used (convert to `.eslintrc.js` if needed to `require()` the local folder). Alternatively, symlink into `node_modules/eslint-plugin-meeple-card` via a workspace script.

> **Note:** if the project already uses `eslint-config-next` with flat config, adapt the registration accordingly. Keep the rule file platform-agnostic.

- [ ] **Step 8.4: Write rule tests**

```js
// apps/web/eslint-rules/__tests__/no-dual-connection-source.test.js
const { RuleTester } = require('eslint');
const rule = require('../no-dual-connection-source');

const tester = new RuleTester({
  parserOptions: { ecmaVersion: 2022, sourceType: 'module', ecmaFeatures: { jsx: true } },
});

tester.run('no-dual-connection-source', rule, {
  valid: [
    { code: '<MeepleCard navItems={x} />' },
    { code: '<MeepleCard connections={y} />' },
    { code: '<MeepleCard manaPips={z} />' },
    { code: '<MeepleCard />' },
    { code: '<SomeOther connections={a} navItems={b} />' },
  ],
  invalid: [
    { code: '<MeepleCard connections={[]} navItems={[]} />', errors: [{ messageId: 'dualSource' }] },
    { code: '<MeepleCard connections={y} manaPips={z} />', errors: [{ messageId: 'dualSource' }] },
  ],
});
```

- [ ] **Step 8.5: Run rule tests**

Run: `pnpm --filter @meepleai/web exec vitest run eslint-rules` (or `node --test`, depending on existing test runner wiring).
Expected: all PASS.

- [ ] **Step 8.6: Run full project lint**

Run: `pnpm --filter @meepleai/web lint`
Expected: 0 errors. The rule has **no co-presence in the codebase today** (audit §3.2).

- [ ] **Step 8.7: Commit**

```bash
git add apps/web/eslint-rules/ apps/web/.eslintrc.*
git commit -m "feat(meeple-card): add eslint rule no-dual-connection-source"
```

---

## Task 9: Clean up `parts/index.ts` barrel + dev playground demos

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts`
- Modify: `apps/web/src/app/(authenticated)/dev/meeple-card/page.tsx` (or equivalent)

- [ ] **Step 9.1: Export new primitives from the barrel**

```ts
// parts/index.ts
export * from './ConnectionChip';
export * from './ConnectionChipPopover';
export * from './ConnectionChipStrip';
export * from './LifecycleStateBadge';
export * from './OwnershipBadge';
// existing exports kept below
```

- [ ] **Step 9.2: Add two demos to dev playground**

One demo: MeepleCard with `connections=[...]` + `connectionsVariant='footer'`.
Second demo: MeepleCard with `navItems=[...]` + `__useConnectionsForNavItems={true}` (adapter path). Label both visually.

- [ ] **Step 9.3: Manual visual inspection**

Start dev server (`pnpm dev`), visit the dev playground, screenshot both demos side-by-side with the same navItems. Verify row heights match visually. Attach screenshot to PR.

- [ ] **Step 9.4: Commit**

```bash
git commit -am "chore(meeple-card): export new primitives + add renderer-integration dev demos"
```

---

## Task 10: Production-safety check + PR

**Files:**
- Create: `variants/__tests__/no-warn-in-production.test.ts` (guard against risk #2 in spec §11)

- [ ] **Step 10.1: Write production-env test**

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render } from '@testing-library/react';

describe('deprecation warn is production-safe', () => {
  const origEnv = process.env.NODE_ENV;
  beforeEach(() => { (process.env as any).NODE_ENV = 'production'; });
  afterEach(() => { (process.env as any).NODE_ENV = origEnv; });

  it('emits no warn in production', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const { MeepleCard } = await import('../../MeepleCard');
    render(<MeepleCard entity="game" title="X" navItems={[{ label: 'L', entity: 'session', icon: null }]} />);
    expect(warn).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 10.2: Run full test suite**

Run: `pnpm --filter @meepleai/web test`
Expected: all green. Previous baselines still match (Level C regression).

- [ ] **Step 10.3: Run typecheck + lint**

Run: `pnpm --filter @meepleai/web typecheck && pnpm --filter @meepleai/web lint`
Expected: 0 errors.

- [ ] **Step 10.4: Update memory + commit**

```bash
git commit -am "test(meeple-card): add production-env safety test for deprecation warn"
```

- [ ] **Step 10.5: Push + open PR**

Target: parent branch. Detect with:
```bash
PARENT=$(git config branch.$(git branch --show-current).parent || echo main-dev)
```

```bash
git push -u origin feature/meeplecard-connectionchip-step-1.6
gh pr create --base $PARENT --title "feat(meeple-card): Step 1.6 ConnectionChip renderer integration" --body "$(cat <<'EOF'
## Summary
- Wire ConnectionChip / ConnectionChipStrip into 5 MeepleCard variants via new `useConnectionSource` hook.
- Add `navItems→connections` adapter behind opt-in flag `__useConnectionsForNavItems` (default OFF → zero DOM change).
- Mark `navItems` and `manaPips` as `@deprecated`; emit dev-only warn for `navItems` (dedup per instance, gated to non-production).
- Add ESLint rule `meeple-card/no-dual-connection-source` to prevent co-presence (audit: 0 current violations).
- Add `iconOverride` optional prop to ConnectionChip so adapter preserves custom icons.
- No call-site migrated here (Step 2).

## Test plan
- [x] Unit: 7 `useConnectionSource` cases
- [x] Unit: 4 `navItemsToConnections` cases (incl. lossy-field warns)
- [x] Unit: 1 new `ConnectionChip iconOverride` test
- [x] Integration: `connections=[]` + `connections=[...]` path per variant
- [x] Integration: `navItems` default path + dedup per variant
- [x] Integration: adapter-flag path for Grid + Focus
- [x] Regression: Level C structural DOM baseline captured and still matches
- [x] Lint: new ESLint rule green on full codebase
- [x] Safety: no warn in `NODE_ENV=production`
- [x] Manual: dev playground visual inspection (screenshots attached)

Spec: docs/superpowers/specs/2026-04-23-connectionchip-step-1.6-renderer-integration.md
Plan: docs/superpowers/plans/2026-04-23-connectionchip-step-1.6-renderer-integration.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 10.6: Request review, address comments, merge, delete branch**

```bash
# after merge
git checkout $PARENT && git pull
git branch -D feature/meeplecard-connectionchip-step-1.6
```

Update memory with squash commit SHA and status.

---

## Coverage map — AC → Tasks

| AC | Covered by |
|---|---|
| R1.6.1 `useConnectionSource` | Task 2 |
| R1.6.2 `connections` renders strip | Task 5 (Step 5.2) |
| R1.6.3 `connections=[]` no-render | Task 5 (Step 5.2 S2) |
| R1.6.4 `navItems` DOM unchanged + warn | Tasks 1 (baseline), 5 (Step 5.4/5.7), 10 (prod safety) |
| R1.6.5 ESLint rule | Task 8 |
| R1.6.6 `@deprecated` JSDoc | Task 7 |

## Coverage map — BDD scenarios → Tests

| Scenario | Test location |
|---|---|
| S1 | Task 2 Step 2.1 (A7) hook warn + Task 5 Step 5.2 Grid (DOM: strip present, NavFooter absent when connections+navItems co-present) |
| S2 | Task 5 Step 5.2 (per variant) |
| S3 | Task 5 Step 5.2 dedup test (per variant) |
| S4 | Task 6 Step 6.1 (Grid) |
| S5 | Task 4 Step 4.1 (adapter) |
| S6 | Task 5 Step 5.2 (Grid — variant assertion) |
| S7 | Task 5 Step 5.2 (Grid — manaPips preserved) |
| S8 | Task 5 Step 5.2 (per variant) |
| S9 | Task 6 Step 6.2 (Focus) |
| S10 | Task 5 Step 5.5 (Compact no-op) |
| S11 | Task 8 Step 8.4 (ESLint invalid) |
| S12 | Task 8 Step 8.4 (ESLint valid) |
