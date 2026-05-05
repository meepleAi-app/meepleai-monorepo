# ConnectionChip Step 1.6 — Renderer Integration Spec

> **Status**: Draft — awaiting approval
> **Author**: Claude (Opus 4) + user Socratic review
> **Date**: 2026-04-23
> **Preceding step**: PR #545 (a11y fixes, merged) — ConnectionChip primitive and Popover fully tested (145/145 pass)
> **Following step**: Step 2 call-site migration sweep (games, agents, sessions, library, admin) — out of scope here
> **Deprecation deadline**: `2026-07-15` (legacy `navItems` warn emitted in dev until this date; runtime removal planned for Step 3)

---

## 1. Goal (one sentence)

Wire the existing, tested `ConnectionChip`/`ConnectionChipStrip` primitives into the five active `MeepleCard` variants so that call-sites passing the already-declared `connections` / `connectionsVariant` props produce visible DOM, while preserving byte-for-byte DOM parity for all existing `navItems`-based call-sites via a lossless adapter and a dev-only deprecation warning.

## 2. Non-goals

| Non-goal | Rationale |
|---|---|
| Migrate any existing call-site from `navItems` to `connections` | Call-site migration is Step 2 (per-area PRs). Touching call-sites here breaks isolation. |
| Migrate `manaPips` at runtime | Deferred to hypothetical Step 1.7. Here we add `@deprecated` JSDoc only; `manaPips` continues to render exactly as today. |
| Modify `HeroCard` variant | Hero uses only `manaPips` (no `navItems`), out of scope for nav-channel consolidation. |
| Playwright visual regression | Deferred to Step 2 (where real user flows exist). Here we rely on structural DOM + a11y tests. |
| Remove `NavFooter.tsx`, `ManaPips.tsx`, or the `connectionsVariant` name | Physical removal is Step 3 (post-deadline). |
| Change any public prop name that is currently consumed | Additive only — no rename, no breaking change. |

## 3. Context — current state

### 3.1 Three parallel channels (today)

| Channel | Source prop | Renderer part | Consumed by |
|---|---|---|---|
| Nav (explicit) | `navItems: NavFooterItem[]` | `<NavFooter>` | GridCard, ListCard, FeaturedCard, FocusCard (inline, no NavFooter wrapper) |
| Nav (decorative) | `manaPips: ManaPip[]` | `<ManaPips>` | GridCard, CompactCard, HeroCard |
| Nav (new, unconsumed) | `connections: ConnectionChipProps[]` + `connectionsVariant` | — (no renderer today) | 0 call-sites |

### 3.2 Call-site audit (2026-04-23)

| File pattern | `navItems=` | `manaPips=` | `connections=` |
|---|:---:|:---:|:---:|
| Game cards (Catalog, Library, Collection, GameNight, Playlist) | ✅ (~10) | — | — |
| Agent/Chat/KB/Session/Playback/Participant cards | ✅ (~10) | — | — |
| `games/[id]/page.tsx` (detail) | — | ✅ | — |
| `library/sections/PersonalLibrarySection.tsx` | — | ✅ | — |
| `library/sections/CatalogCarouselSection.tsx` | — | ✅ | — |
| Dev playground `dev/meeple-card/page.tsx` | ✅ (multiple) | — | — |

**Facts**:
- **0** call-sites currently pass `connections` to MeepleCard (prop is "dead API").
- **0** call-sites mix `navItems` with `manaPips` on the same MeepleCard (disjoint usage).
- Co-presence `connections + navItems` or `connections + manaPips` is therefore prevented by construction today; the ESLint rule (R1.6.5) codifies this invariant before Step 2 opens the door.

### 3.3 Known technical constraints

- `ConnectionChipProps` (types.ts L66–79) has no `icon` override slot; `NavFooterItem.icon: ReactNode` is a custom node. Adapter must carry it → new optional field `iconOverride?: ReactNode` added to `ConnectionChipProps`.
- `NavFooterItem.onPlusClick` + `showPlus` is not 1:1 expressible via `ConnectionChip.onCreate` (chip shows plus only when `count === 0 && onCreate`). This is a known **lossy** path; adapter emits a dev warn when it drops `onPlusClick` for `count > 0`.
- `FocusCard` renders nav chips inline (no `<NavFooter>` wrapper) with custom classes. The wire-in must produce the same visual row height and the same role/name tree when fed the equivalent `connections` array.

---

## 4. Architecture — Branch by Abstraction (Q1=A)

```
MeepleCard(props)
   │
   ▼
useConnectionSource(props) ──► { items: ConnectionChipProps[] | null, variant, source: 'connections' | 'navItems' | 'manaPips' | null, warnings: string[] }
   │
   ├─ source === 'connections'      → render <ConnectionChipStrip items={items} variant={variant} />
   ├─ source === 'navItems'         → adapt navItems → ConnectionChipProps[], render <ConnectionChipStrip> (new path) OR render <NavFooter> (legacy path, feature-flag)  ← see R1.6.4
   ├─ source === 'manaPips'         → render <ManaPips> exactly as today (untouched)
   └─ source === null               → render nothing (no strip, no footer)
```

### 4.1 Hook precedence (Q6)

`useConnectionSource` returns the first matching source in this order:

1. `connections !== undefined` → `'connections'`
   - `connections === []` → `source='connections'`, `items=[]` → **empty strip, no DOM** (Q7=A: explicit no-render)
   - `connections` with items → render strip
2. `navItems !== undefined && navItems.length > 0` → `'navItems'` (adapter path)
3. `manaPips !== undefined` → `'manaPips'` (legacy path, untouched in 1.6)
4. otherwise → `null`

**Rule**: if `connections` is defined, `navItems` and `manaPips` are ignored (should be caught by ESLint first, but runtime is defensive).

### 4.2 Feature flag for legacy→new (R1.6.4)

To keep risk low, the `navItems` adapter is **behind a prop-level opt-in** during 1.6:

- Default for `source === 'navItems'`: continue to render `<NavFooter>` (existing behaviour, zero DOM change).
- Opt-in via a new internal flag `__useConnectionsForNavItems` (undocumented, dev-only in 1.6): renders `<ConnectionChipStrip>` adapted from navItems. This flag exists to let us A/B the adapter in tests and the dev playground before Step 2 flips it.
- Public prop `connectionsVariant`: fully honoured when `source === 'connections'`. When `source === 'navItems'` and flag is off, ignored.

> Decision rationale: avoids a big-bang swap of ~20 consumer wrappers in one PR. The adapter path is exercised by unit+integration tests in 1.6; the actual DOM swap happens per-consumer in Step 2.

---

## 5. Acceptance Criteria

### R1.6.1 — `useConnectionSource` hook exists and dispatches correctly

**Given** `MeepleCardProps`,
**When** `useConnectionSource(props)` is called,
**Then** it returns `{ source, items, variant, warnings }` following the precedence table in §4.1, and is the **only** place in the dispatcher that reads `connections`/`navItems`/`manaPips` together.

**Verification**: unit tests in `useConnectionSource.test.ts` covering all 7 precedence cases (see §6).

### R1.6.2 — `connections`-source call-sites render `ConnectionChipStrip`

**Given** a MeepleCard in any of **{Grid, List, Featured, Focus}** variant,
**When** `connections={[...]}` is passed with at least one item,
**Then** a `<ConnectionChipStrip>` is rendered in the variant's connection slot, with `variant = connectionsVariant ?? 'footer'`, and **no** `<NavFooter>` or legacy inline chip row is rendered.

**Exclusion**: CompactCard is a documented no-op for `connections` (tight layout, no nav slot). See S10.

**Verification**: per-variant integration tests query `screen.getByTestId('connection-chip-strip')` and assert the number of chips equals `connections.length`.

### R1.6.3 — `connections=[]` produces no connection DOM (Q7=A)

**Given** `connections={[]}` on any variant,
**Then** no `<ConnectionChipStrip>`, no `<NavFooter>`, no manaPips row is rendered, and no warning is emitted.

**Rationale**: empty array is a valid explicit "none" expression (distinct from `undefined`). Consumers migrating from `navItems` can force "no connections" by passing `connections={[]}` without falling back to `navItems`.

### R1.6.4 — `navItems`-source call-sites unchanged at DOM level (default path)

**Given** any existing call-site passing `navItems={[...]}` and **not** passing `connections`,
**When** the MeepleCard renders,
**Then** the resulting DOM tree under the nav slot is **byte-for-byte identical** to `main-dev@HEAD` before this PR (snapshot baseline captured in Task 1), and a dev-only `console.warn` is emitted exactly once per MeepleCard instance per page session with the deprecation message.

**Warn format** (A2=my recommendation, dedup via WeakSet):

```
[MeepleCard] The `navItems` prop is deprecated. Migrate to `connections` by 2026-07-15.
  See https://github.com/DegrassiAaron/meepleai-monorepo/issues/<step-2-issue-id> for migration examples.
  This warning is shown once per MeepleCard instance in development mode.
```

Dedup strategy: module-level `WeakSet<object>` keyed by the props object reference; `process.env.NODE_ENV !== 'production'` gate.

### R1.6.5 — ESLint rule `meeple-card/no-dual-connection-source`

**Given** a JSX element `<MeepleCard ...>`,
**When** it has both `connections` and `navItems` props, or both `connections` and `manaPips` props,
**Then** lint fails with error: `Cannot mix \`connections\` with \`navItems\`/\`manaPips\` on the same MeepleCard. Pick one source.`

**Scope**: the rule lives in `apps/web/eslint-rules/no-dual-connection-source.js` (new folder) and is registered inline in `apps/web/.eslintrc.json` via `plugins` + `rules` entry. No separate npm package.

**A3 confirmed**: inline custom rule (not npm package), registered via `eslintPluginMeepleCard` local plugin object.

### R1.6.6 — `manaPips` marked `@deprecated` (doc-only)

**Given** the `MeepleCardProps.manaPips` field in `types.ts`,
**Then** it carries a `@deprecated` JSDoc tag pointing to the same deadline and migration guide, **but** the runtime rendering path for `source === 'manaPips'` is **unchanged** (no warn, no DOM diff, no test change).

**Rationale** (Q2/Q3 clarification): manaPips are conceptually equivalent to connections (nav + create), but physical migration is non-trivial (Hero/Compact use manaPips decoratively with popover logic) and is deferred. JSDoc raises awareness without triggering churn.

---

## 6. BDD Scenarios (Q8=C)

All scenarios are in Vitest + RTL. Language: Given/When/Then.

### S1 — connections wins over navItems at runtime (defensive)

```
Given  a MeepleCard with connections=[{entityType:'session',count:3}]
  and  a mistakenly-passed navItems=[{label:'X', entity:'session', href:'/x', icon:<i/>}]
When   the MeepleCard renders
Then   a ConnectionChipStrip with exactly 1 chip is in the DOM
  and  no NavFooter is in the DOM
  and  a dev warn mentions "dual source" (defensive — ESLint should have caught it)
```

### S2 — connections empty array means no-render (R1.6.3)

```
Given  a MeepleCard with connections=[]
When   the MeepleCard renders
Then   no ConnectionChipStrip is in the DOM
  and  no NavFooter is in the DOM
  and  no console.warn is emitted
```

### S3 — navItems default path preserved (R1.6.4)

```
Given  a MeepleCard (any of Grid/List/Featured/Focus) with navItems=[{label:'3 sessioni', entity:'session', href:'/s/1', icon:<i data-testid="nf-icon"/>}]
When   the MeepleCard renders
Then   the rendered <NavFooter> element is present
  and  screen.getByTestId('nf-icon') resolves
  and  exactly 1 dev warn matching /navItems.*deprecated/ is emitted
  and  rendering the same component twice with the same props object emits only 1 warn total
```

### S4 — navItems adapter path (flag on) renders equivalent ConnectionChipStrip

```
Given  a MeepleCard with navItems=[{label:'3 sessioni', entity:'session', count:3, href:'/s', icon:<i/>}]
  and  __useConnectionsForNavItems=true
When   the MeepleCard renders
Then   a ConnectionChipStrip with 1 chip is in the DOM
  and  the chip has aria-label matching /3.*session/i
  and  the chip icon node is the provided <i/> (via iconOverride)
  and  no NavFooter is in the DOM
```

### S5 — navItems.onPlusClick + count>0 is lossy (warns)

```
Given  navItems=[{label:'x', entity:'player', count:2, showPlus:true, onPlusClick: fn}]
  and  __useConnectionsForNavItems=true
When   the adapter runs
Then   a dev warn matches /onPlusClick.*dropped.*count>0/
  and  the resulting ConnectionChipProps has no onCreate
  and  chip renders without the plus overlay
```

### S6 — connectionsVariant respected

```
Given  connections=[{entityType:'kb',count:1}], connectionsVariant='inline'
When   the MeepleCard renders (GridCard)
Then   <ConnectionChipStrip variant="inline"> is used (no border-top, no labels)
```

### S7 — manaPips path untouched (R1.6.6)

```
Given  a MeepleCard with manaPips=[{state:'ok', ...}] and no connections, no navItems
When   the MeepleCard renders
Then   the <ManaPips> element is present exactly as in main-dev@HEAD
  and  no deprecation warn is emitted (doc-only deprecation)
  and  no ConnectionChipStrip is rendered
```

### S8 — source=null renders no nav DOM

```
Given  a MeepleCard with no connections, no navItems, no manaPips
When   the MeepleCard renders
Then   no ConnectionChipStrip, no NavFooter, no ManaPips element is in the DOM
```

### S9 — FocusCard inline chip row migrates cleanly (adapter flag on)

```
Given  a FocusCard with navItems=[3 items, each with icon, href, badge]
  and  __useConnectionsForNavItems=true
When   the FocusCard renders
Then   the inline chip row contains exactly 3 <button> or <a> elements with aria-labels
  and  the total inline row height matches the baseline (no layout shift)
  and  all original hrefs are preserved as anchor href attributes
```

### S10 — CompactCard has no nav slot (no-op)

```
Given  a CompactCard with connections=[{entityType:'session',count:1}]
When   the CompactCard renders
Then   no ConnectionChipStrip is rendered (Compact has no nav slot by design)
  and  no warn is emitted about the ignored connections
  and  existing manaPips path continues to render if provided
```

> **Decision deferred**: whether Compact gains a nav slot is a Step 2 product question. In 1.6 Compact explicitly ignores `connections`. Documented here, tested in S10.

### S11 — ESLint rule fires on co-presence (R1.6.5)

```
Given  source code: <MeepleCard entity="game" title="x" connections={[]} navItems={[]} />
When   eslint runs
Then   the lint output contains "meeple-card/no-dual-connection-source"
  and  the rule's error message matches /Cannot mix .connections. with .navItems.\/.manaPips./
```

### S12 — ESLint rule does NOT fire on single source

```
Given  source code containing <MeepleCard navItems={x} /> or <MeepleCard connections={y} /> or <MeepleCard manaPips={z} />
When   eslint runs
Then   no error for rule "meeple-card/no-dual-connection-source"
```

---

## 7. Test matrix (Q9=C, Q10=C)

### 7.1 Level A — unit (hook)

File: `apps/web/src/components/ui/data-display/meeple-card/hooks/__tests__/useConnectionSource.test.ts`

| Case | props | Expected `source` |
|---|---|---|
| A1 | `{}` | `null` |
| A2 | `{ connections: [] }` | `'connections'` (items=[]) |
| A3 | `{ connections: [x] }` | `'connections'` |
| A4 | `{ navItems: [] }` | `null` (empty navItems treated as none, matches current NavFooter behaviour) |
| A5 | `{ navItems: [x] }` | `'navItems'` |
| A6 | `{ manaPips: [x] }` | `'manaPips'` |
| A7 | `{ connections: [x], navItems: [y] }` | `'connections'` + warn |

### 7.2 Level B — integration (per-variant)

Files: `variants/__tests__/GridCard.test.tsx`, `ListCard.test.tsx`, `CompactCard.test.tsx`, `FeaturedCard.test.tsx`, `FocusCard.test.tsx`. **Add** the following assertions per variant (no existing test removed):

| Variant | Adds assertions from | Skip cases |
|---|---|---|
| Grid | S2, S3, S4, S6, S7, S8 | — |
| List | S2, S3, S4, S6, S8 | S7 (List doesn't use manaPips) |
| Compact | S10 | S3/S4 (no navItems in Compact) |
| Featured | S2, S3, S4, S8 | S7 |
| Focus | S2, S3, S4, S9 | S7 |

### 7.3 Level C — regression (structural DOM baseline)

File: `variants/__tests__/renderer-integration-baseline.test.tsx` (new).

For each variant V in {Grid, List, Featured, Focus} (Compact excluded — no navItems):

1. Render V with a representative `navItems` fixture (`mockNavItems3`).
2. Assert **counts** of `role="link"`, `role="button"`, `data-testid="nav-footer-*"` match the baseline captured in Task 1.
3. Assert the accessible name tree via `screen.getAllByRole('link').map(a => a.getAttribute('aria-label'))` matches a snapshot array.

> No HTML snapshot — we rely on structural counts + aria-label arrays. This is robust to Tailwind class reordering.

### 7.4 NOT in scope

- No Playwright visual regression (Q10=C).
- No cross-browser screenshot diff.
- No full snapshot (`toMatchSnapshot()` on full DOM) — brittle against unrelated Tailwind changes.

---

## 8. Adapter contract — `navItems → ConnectionChipProps`

### 8.1 Field map

| `NavFooterItem` | `ConnectionChipProps` | Notes |
|---|---|---|
| `entity` | `entityType` | direct |
| `label` | `label` | direct |
| `count` | `count` | direct (default 0) |
| `href` | `href` | direct |
| `disabled` | `disabled` | direct |
| `onClick` | — | **lossy** — chips use `href` or `onCreate`; `onClick` has no 1:1 slot. Warn if present and `href` absent. |
| `onPlusClick` + `showPlus` (count=0) | `onCreate` | mapped when `count === 0 && showPlus === true` |
| `onPlusClick` + `showPlus` (count>0) | — | **lossy** — warn (S5) |
| `icon` | `iconOverride` (new field) | new optional prop on `ConnectionChipProps` so chip can render the custom node instead of default Lucide icon |

### 8.2 Warnings emitted by the adapter

| Code | Trigger | Level |
|---|---|---|
| `W1` | `navItems` present, deprecation | dev warn, deduped per MeepleCard instance |
| `W2` | `onClick` present without `href` → event cannot fire | dev warn (per navItem) |
| `W3` | `showPlus && onPlusClick && count > 0` → plus suppressed | dev warn (per navItem) |
| `W4` | `connections` + `navItems` both present (runtime defence) | dev warn |

All warnings gated by `process.env.NODE_ENV !== 'production'`.

### 8.3 New optional field on `ConnectionChipProps`

```ts
export interface ConnectionChipProps {
  // ... existing fields
  /**
   * Optional icon node to render instead of the default Lucide icon for `entityType`.
   * Used by the `navItems → connections` adapter to preserve custom icons.
   */
  iconOverride?: ReactNode;
}
```

`ConnectionChip` renders `iconOverride` in both the main chip face and the popover header when provided; otherwise falls back to `entityIcons[entityType]`.

---

## 9. Scope summary — variants touched

| Variant | Touched in 1.6? | Reason |
|---|:---:|---|
| GridCard | ✅ | has `navItems`, dispatcher wire-in + adapter path |
| ListCard | ✅ | has `navItems` (size=sm), dispatcher wire-in |
| CompactCard | ⚠️ partial | no `navItems`; only gets `source='connections'` branch + S10 test to assert no-op on connections (future-proof); `manaPips` untouched |
| FeaturedCard | ✅ | has `navItems`, dispatcher wire-in |
| FocusCard | ✅ | has inline nav chip row (no NavFooter wrapper) — adapter must match DOM shape (S9) |
| HeroCard | ❌ | no `navItems`, only `manaPips`. Out of scope per Q2. |

---

## 10. Success metrics

1. All 12 BDD scenarios pass as Vitest tests.
2. Call-site DOM byte-for-byte unchanged for **all** existing `navItems` consumers (captured via structural baseline in Task 1, asserted in Level C regression).
3. `apps/web/pnpm test` green; `apps/web/pnpm lint` green including new ESLint rule.
4. `grep -r "connections=" apps/web/src/` still returns **0** MeepleCard call-sites at PR-merge time (Step 2 hasn't opened yet).
5. ConnectionChip test counts: 145 → 145 + delta (no tests removed; only added).
6. Dev playground `dev/meeple-card/page.tsx` gains at least one `connections=` demo + one `__useConnectionsForNavItems=true` demo for manual visual inspection.

## 11. Risks + mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|:---:|:---:|---|
| `FocusCard` inline chip row adapter produces slightly different DOM height → layout shift | M | M | Level C regression asserts counts + aria-labels; manual screenshot comparison in dev playground before merge |
| Dev warn fires in production due to env misconfig | L | M | Guard `process.env.NODE_ENV !== 'production'`; add test that spies `console.warn` with `NODE_ENV='production'` and asserts no emission |
| WeakSet dedup leaks identities across test runs | L | L | Per-test `beforeEach` resets the module-level WeakSet via a test-only export `__resetWarnDedup()` |
| ESLint rule implementation false-positives on destructured props | M | L | Rule only triggers on literal JSX props (static analysis); documented in rule's tests with both positive and negative cases |
| `iconOverride` field renamed later | L | L | Field is optional and additive; renaming is a follow-up; low-risk |

## 12. Open questions — **none remaining**

All Q1–Q10 and A1–A3 resolved in preceding Socratic sessions. Ready for approval.

---

## Appendix A — referenced files

- `apps/web/src/components/ui/data-display/meeple-card/types.ts` (L66–124)
- `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` (dispatcher)
- `apps/web/src/components/ui/data-display/meeple-card/variants/{Grid,List,Compact,Featured,Focus,Hero}Card.tsx`
- `apps/web/src/components/ui/data-display/meeple-card/parts/{ConnectionChip,ConnectionChipStrip,ConnectionChipPopover,NavFooter,ManaPips}.tsx`
- `apps/web/src/components/ui/data-display/meeple-card/parts/index.ts` (barrel to clean up — add ConnectionChip{,Strip,Popover} exports)
- `apps/web/.eslintrc.json` (extend with local plugin)

## Appendix B — deprecation timeline

| Date | Milestone |
|---|---|
| **2026-04-23** | Step 1.6 merged: `@deprecated` JSDoc + dev warn live |
| **2026-05–2026-06** | Step 2: per-area migration PRs (games, agents, sessions, library, admin) |
| **2026-07-15** | Deadline: all call-sites migrated |
| **2026-07-16+** | Step 3: physical removal of `NavFooter.tsx`, `ManaPips.tsx`, rename `connectionsVariant → variant` if desired |
