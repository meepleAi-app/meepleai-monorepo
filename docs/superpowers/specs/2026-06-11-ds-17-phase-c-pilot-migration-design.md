# DS-17 Phase C-1 — Pilot Migration Design

**Date**: 2026-06-11
**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) — DS-17 Mockup-to-App Fidelity
**Predecessors**: Phase A ([#2120](https://github.com/meepleAi-app/meepleai-monorepo/issues/2120) MERGED PR #2124) · Phase B ([#2127](https://github.com/meepleAi-app/meepleai-monorepo/issues/2127) MERGED PR #2128 `66e924233`)
**Pattern reference**: `docs/for-developers/frontend/page-mock-story-pattern.md` + Phase 2.5 pilots (Library 9 + GameDetail 3 stories)

---

## Goal

Migrare 46 mockup pilot (auth 12 + sp3 16 + sp6-7-nano 18) → 46 Storybook stories seguendo argTypes matrix pattern (DEC-P3-3) shipped Phase 2.5. Phase C-1 valida il pattern hybrid AI scaffold + human verify; Phase C-2 (sp4-core 106 + sp4-sessions 50, 156 mockup) sarà follow-up sub-issue post-validation.

Phase B audit fornisce numeric scope: 46 mockup migrabili (104 esclusi: 17 dev-fixtures + 5 obsoleti + sp4-core/sessions deferred to C-2).

## User decisions (brainstorming 2026-06-11)

| # | Decisione | Scelta | Rationale |
|---|-----------|--------|-----------|
| 1 | Scope Phase C | **PILOT-FIRST** (46 mockup, ~1-2 settimane) | Valida pattern + Storybook scale + CI behavior prima di committarsi al volume sp4-core (106) |
| 2 | PR/sub-issue granularity | **3 sub-issue separate** (1 per cluster) | Incrementale + designer review per cluster; admin-squash merge 3 volte |
| 3 | Cluster order | **auth → sp3 → sp6-7-nano** | Dependency order (auth foundational), public-facing secondo, prototype-quality terzo |
| 4 | Execution mode | **Hybrid AI scaffold + human verify** | AI pre-flight maximizes ROI su axis discovery + MSW gap analysis; human controlla quality finale + commit |
| 5 | CI gate flip | **Post full Phase C completion** (~131 stories total) | Conservative: continue-on-error fino a Phase C-2 completion + 14gg stable trajectory |
| 6 | Orchestration | **Opt A — Pre-flight AI batch per cluster** | 1 AI dispatch genera scaffolds per intero cluster; human itera + commit 1-by-1 |

## Architecture

### 3 sub-issue sequenziali

```
DS-17-9-auth (12 mockup)        →  PR + merge  →  DS-17-10-sp3 (16 mockup)        →  PR + merge  →  DS-17-11-sp6-7-nano (18 mockup)        →  PR + merge
```

Each sub-issue ha lifecycle 4-fase:

```
Phase 1 — AI pre-flight batch (~30min compute)
   └→ scaffolds/<cluster>/<mockup-stem>/ dirs committed as "chore(stories): #NNNN <cluster> scaffolds (AI pre-flight)"

Phase 2 — Human iteration (~1-2gg)
   └→ N commits "feat(stories): #NNNN <mockup-name> story" (1 per mockup)

Phase 3 — Cluster integration (~0.5gg)
   └→ cluster.snapshot.spec.ts + designer queue + fidelity.json refs + scaffolds cleanup

Phase 4 — PR + admin-squash merge + umbrella update + next cluster trigger
```

### Total Phase C-1 effort

- 12 auth + 16 sp3 + 18 sp6-7-nano = 46 stories
- Per cluster: ~1.5-2gg AI + human + integration
- 3 cluster sequential: ~5-6gg total
- Plus 3 admin-squash merges + 3 umbrella updates: +0.5gg
- **Total Phase C-1: ~6-7 working days** (~1-1.5 settimane realistic)

## Components

### 1. Cluster Scaffold Generator (AI pre-flight dispatch)

**Input**: cluster subset of `audits/2026-06-10-mockup-design-intent-manifest.json`

**Dispatch**: 1 Agent(general-purpose) per cluster (NOT per mockup) reads all N non-obsolete mockups, emits structured output.

**Output structure** per mockup:
```
apps/web/scripts/audit-mockups/scaffolds/<cluster>/<mockup-stem>/
├── story.draft.tsx         # Story file scaffold with argTypes matrix + meta
├── fixture.draft.ts        # Fixture data + MSW handlers stub
├── axis-discovery.md       # Documented axis + frame matrix table
└── msw-gap-analysis.md     # Missing handlers + endpoint mapping
```

**Detection rules**:
- Skip mockup with `design_intent: 'forward-refactor-obsolete'` (5 mockup, tracked #2138-2147)
- Read HTML+JSX twin; if pair_disagreement flagged → use HTML as canonical (per MOCKUPS_INDEX pairing rule), document choice
- Cross-ref `apps/web/src/app/<route>/page.tsx` for component path
- Identify axis via grep on JSX twin: `stateOverride`, `variant`, `initialTab`, `initialView`, `drawerOpen`, `bulk`
- Identify frames via `DesktopFrame label="NN · ..."` and `PhoneShell key={s.id}` arrays

### 2. Story File Pattern

**Path convention**: co-locata con component, named `<component>.stories.tsx`.

Mirror Phase 2.5 pilot structure. JSDoc header con `@mockup admin-mockups/design_files/<name>.html`.

**meta** shape:
```tsx
const meta: Meta<typeof <Component>> = {
  title: 'Pages/<SP>/<Cluster> / <Mockup Name>',
  component: <RealClientComponent>,
  parameters: {
    layout: 'fullscreen',
    docs: { description: { component: '<mockup description>' } },
  },
  argTypes: {
    // mirror axis from JSX twin, each documented
    state: { control: 'select', options: ['default', 'empty', 'loading', 'error'], description: '...' },
    // ... other axis
  },
  args: { /* first frame defaults */ },
};
```

**Frame exports**: `FrameNN_ShortName: Story` (1 per Desktop frame del mockup stage), name mirrors mockup JSX label.

### 3. Fixture File Pattern

**Path**: `apps/web/src/__tests__/fixtures/mockup-pilots/<cluster>/<mockup-stem>.ts`

Co-located con Phase 2.5 fixtures (`library.ts`, `game-detail.ts`).

**Exports**: named `MOCK_<CLUSTER>_<NAME>_<STATE>` (es. `MOCK_AUTH_LOGIN_DEFAULT`).

**MSW handlers** tagged per state in same file:
```ts
export function mswForState(state: AuthLoginState) {
  if (state === 'loading') return [http.post('*/api/v1/auth/login', () => new Promise(() => {}))];
  // ... other states
}
```

### 4. Snapshot Spec Pattern

**Path**: `apps/web/e2e/storybook/<cluster>.snapshot.spec.ts`

Mirror `library.snapshot.spec.ts` + `game-detail.snapshot.spec.ts` (post Phase 4 prelude fix).

1 spec file per cluster, contains FRAMES array with slug + file PNG name per story.

### 5. Designer Review Queue Generator

**New script**: `apps/web/scripts/audit-mockups/generate-cluster-review-queue.mjs`

**Output**: `docs/for-developers/frontend/c1-<cluster>-review-queue.md`

Lists shipped stories + design_intent classification (Phase B) + open obsolete reclassifications (post-Phase B candidates discovered during Phase C iteration).

### 6. Human iteration checklist (per mockup)

Used by human in Phase 2 of each sub-issue:

- [ ] Read `axis-discovery.md` + verify matches mockup JSX twin
- [ ] Refine `story.draft.tsx` → `<route-path>/<component>.stories.tsx`
- [ ] Refine `fixture.draft.ts` → `__tests__/fixtures/mockup-pilots/<cluster>/<name>.ts`
- [ ] Run `pnpm storybook` → story renders no error wall
- [ ] `pnpm test:storybook:snapshots:update` for new story
- [ ] Visual diff vs mockup HTML (browser side-by-side)
- [ ] Commit `feat(stories): #NNNN <mockup-name> story` (1 commit per story)

## Data flow

```
[admin-mockups/design_files/<cluster>/*.{html,jsx}]
        │
        ▼
[audits/2026-06-10-mockup-design-intent-audit.json]  ← cluster subset (Phase B output)
        │
        ▼
┌─── Phase 1: AI Pre-flight Scaffold Generator ───────────┐
│  Master orchestrator (in conversation):                  │
│    1. Filter audit JSON for target cluster               │
│    2. Skip mockup with design_intent='forward-refactor-  │
│       obsolete' (#2138-2147 tracking)                    │
│    3. Dispatch ONE Agent(general-purpose):               │
│       - Read each non-obsolete mockup HTML+JSX twin      │
│       - Cross-ref apps/web/src/app/<route>/page.tsx      │
│       - Identify axis + frames                            │
│       - Inspect Phase 2.5 fixtures for pattern reuse     │
│       - Emit scaffold dir per mockup                     │
│  Output: scaffolds/<cluster>/                            │
└─────────────────────────────────────────────────────────┘
        │
        ▼
[git commit "chore(stories): #NNNN <cluster> scaffolds (AI pre-flight)"]
        │
        ▼
┌─── Phase 2: Human iteration (1 story at a time) ────────┐
│  For each mockup in cluster:                             │
│    1. Read scaffold drafts                                │
│    2. Refine story + fixture                              │
│    3. pnpm storybook → verify renders                    │
│    4. pnpm test:storybook:snapshots:update                │
│    5. Visual diff vs mockup HTML                          │
│    6. git commit "feat(stories): #NNNN <name>"           │
└─────────────────────────────────────────────────────────┘
        │
        ▼
┌─── Phase 3: Cluster integration ────────────────────────┐
│    1. Update cluster.snapshot.spec.ts FRAMES array       │
│    2. pnpm lint:fidelity (verify refs)                    │
│    3. pnpm typecheck + pnpm lint                          │
│    4. Update fidelity.json story_path + fixtures_path    │
│    5. Generate designer review queue                      │
│    6. rm -rf scaffolds/<cluster>/                         │
│    7. git commit "chore(stories): #NNNN finalize cluster"│
└─────────────────────────────────────────────────────────┘
        │
        ▼
[PR + admin-squash merge → main-dev]
        │
        ▼
[Umbrella body update + trigger next cluster sub-issue]
```

## Error handling

### Per AI pre-flight dispatch

| Failure mode | Behavior |
|---|---|
| Agent timeout (>10min) | Mark cluster scaffolds as partial, escalate to user; manual re-dispatch for missing mockup |
| Invalid scaffold structure (missing draft file) | Skip that mockup, log to `scaffolds/<cluster>/_skipped.md`, continue |
| AI hallucinates component path | Cross-check against `apps/web/src/app/**/page.tsx` glob; flag in axis-discovery.md if no match |
| Axis discovery wrong | Documented in axis-discovery.md; human catches in Phase 2 iteration. Acceptable since drafts |

### Per human iteration

| Edge case | Behavior |
|---|---|
| Story doesn't render (provider missing) | Phase 4 prelude fixed common providers. New gap → file bug + temp `parameters.docs.disable: true`, continue |
| Unmocked dependency | Add to fixture MSW handlers. Structural (SSR-only) → flag in story header + skip baseline |
| Impossible frame state combinations | Document in axis-discovery.md, restrict argTypes via control options. Story still renders default |
| Snapshot diff > 5% threshold | Inspect: real divergence vs flake. Re-run; if persists, file follow-up |
| HTML+JSX pair_disagreement | Default to HTML semantic (MOCKUPS_INDEX rule). Document choice in story header |

### Per cluster integration

| Failure mode | Behavior |
|---|---|
| `pnpm lint:fidelity` fails | Inspect first failing file; common: missing required field; fix inline (Phase B FIDELITY_TEMPLATE reference) |
| `pnpm typecheck` fails | Inspect import errors, prop signatures; fix inline |
| Snapshot spec slug mismatch | Cross-check meta.title vs Storybook URL slug |
| Mockup obsolete reclassification discovered | Don't ship story; add to `_skipped.md` + propose Phase B-style tracking issue |

### Per PR + merge

| Failure mode | Behavior |
|---|---|
| CI fails on Storybook build | Pre-push hook catches; fix before push |
| Designer review queue not generated | Run new `generate-cluster-review-queue.mjs` (mirror Phase B generate-deliverables) |
| Merge conflict with main-dev | Rebase; typical only in fidelity.json |
| Scaffolds not cleaned post-merge | Phase 3 step 6 runs `rm -rf`; verify pre-commit; follow-up cleanup PR if missed |

## Testing strategy

### Unit tests (TDD, vitest)

1. **`generate-cluster-review-queue.mjs`** (new):
   - Given aggregated cluster JSON + N stories → emits queue markdown
   - Given obsolete mockup → marks "DEFERRED post-Phase-B-tracking-#NNNN"
   - Given pair_disagreement → queue calls it out

2. **Cluster scaffold generator dispatch contract** (integration test mocking Agent):
   - Given 3 mockup fixture → emit 3 scaffold dirs
   - Given obsolete mockup → skipped from scaffold output

### Integration tests (Playwright + Storybook)

3. **Cluster snapshot specs** (per cluster):
   - Given N stories shipped + baselines → `pnpm test:storybook:snapshots` passes N/N
   - Given intentional fixture change → snapshot diff detected (smoke gate)
   - Runs as part of Phase 3 cluster integration BEFORE PR

### Smoke tests (manual, Phase 2 human iteration)

- Story renders in `pnpm storybook` (no error wall)
- Visual diff vs mockup HTML side-by-side
- MSW handlers work (switch argTypes states)

### Regression tests (post-merge)

- **Diagnostic spec** (Phase 4 prelude): extend STORIES array with cluster pilot slug to ensure provider chain works
- **lint:fidelity all** nightly cron: catches fidelity.json drift

### Manual verification (post-cluster-merge)

- Sample 3 random stories → verify designer queue references correct mockup
- Sample 1 obsolete skipped → verify fidelity.json refs Phase B tracking issue
- Verify `scaffolds/<cluster>/` dir deleted post-merge

## Acceptance criteria (per cluster sub-issue)

- [ ] N stories committed under `apps/web/src/<route-path>/<component>.stories.tsx`
- [ ] N fixture files under `apps/web/src/__tests__/fixtures/mockup-pilots/<cluster>/`
- [ ] 1 snapshot spec under `apps/web/e2e/storybook/<cluster>.snapshot.spec.ts`
- [ ] N baseline PNGs captured (Desktop, Mobile opt-in via fidelity.json)
- [ ] `pnpm test:storybook:snapshots` passes N/N
- [ ] `pnpm lint:fidelity` passes
- [ ] `pnpm typecheck` + `pnpm lint` clean
- [ ] Designer review queue published (`docs/for-developers/frontend/c1-<cluster>-review-queue.md`)
- [ ] Diagnostic spec STORIES array extended with 1 cluster sample slug
- [ ] Umbrella body updated post merge with cluster row
- [ ] `scaffolds/<cluster>/` deleted

## Acceptance criteria (Phase C-1 closure)

- [ ] DS-17-9 auth merged (~12 stories)
- [ ] DS-17-10 sp3 merged (~14 stories — 2 forward-refactor flagged for designer review)
- [ ] DS-17-11 sp6-7-nano merged (~18 stories)
- [ ] CI continue-on-error preserved (flip deferred to Phase C-2 completion)
- [ ] Phase C-2 sub-issue opened for sp4-core (106) + sp4-sessions (50)
- [ ] Umbrella body updated with Phase C-1 closure row

## Out of scope

- Phase C-2 sp4-core (106 mockup) — deferred sub-issue post Phase C-1 validation
- Phase C-2 sp4-sessions (50 mockup) — deferred
- Mobile viewport stories — opt-in only via fidelity.json `viewports: ['desktop', 'mobile']` per mockup (rare)
- CI gate flip to blocking — deferred to Phase D after full Phase C completion + 14gg stable
- Designer formal sign-off process — same as Phase B (skipped per user decision, reclassification candidates handled via tracking issues)
- Phase B tracking issues addressing (#2138-2153) — separate workstreams
- Storybook source-of-truth elevation (Phase E) — optional, post-Phase D

## References

- Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity
- Phase A: [#2120](https://github.com/meepleAi-app/meepleai-monorepo/issues/2120) MERGED PR #2124 `dba7898c1`
- Phase B: [#2127](https://github.com/meepleAi-app/meepleai-monorepo/issues/2127) MERGED PR #2128 `66e924233`
- Pattern doc: `docs/for-developers/frontend/page-mock-story-pattern.md`
- Phase 2.5 redesign spec: `docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md`
- Phase 4 prelude spec: `docs/superpowers/specs/2026-06-10-ds-17-phase-4-prelude-intl-hardening-design.md`
- Audit output: `audits/2026-06-10-mockup-design-intent-audit.json` (229 classifications)
- Coverage gap report: `audits/2026-06-10-mockup-coverage-gap-report.md`
- Nav-chrome/BGG/naming audit: `audits/2026-06-10-nav-chrome-bgg-naming-audit.md`
- DEC-P3-1..5 from `docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md` § 2
- Fidelity schema: `apps/web/scripts/mockup-annotations/validate-fidelity.mjs` (post Phase B PENDING sentinel patch)
- Phase 2.5 pilot stories (reference implementation): `apps/web/src/app/(authenticated)/library/_content.stories.tsx` + `games/[id]/_components/GameDetailView.stories.tsx`
- Phase 2.5 snapshot specs: `apps/web/e2e/storybook/{library,game-detail}.snapshot.spec.ts`
- Phase B tracking issues (mockup obsolete cleanup): #2138-#2147
- Phase B tracking issues (architecture/coverage gaps): #2148-#2153
