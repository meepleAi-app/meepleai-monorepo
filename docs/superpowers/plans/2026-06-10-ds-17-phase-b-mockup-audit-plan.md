# DS-17 Phase B — Mockup Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Classify all 224 mockup files in `admin-mockups/design_files/` with explicit `design_intent`, generate 224 fidelity.json stubs, publish designer review queue, create tracking issues per obsolete (post designer sign-off).

**Architecture:** Sequential cluster-by-cluster fan-out — 6 ordered clusters audited sequentially by general-purpose subagents. Master orchestrator in main conversation aggregates structured JSON output, validates via zod, generates deliverables. Tracking issue creation gated by designer magic-phrase comment.

**Tech Stack:** Node ESM scripts, vitest (TDD), zod (schema), glob (file walking), gh CLI (issue creation), Agent tool dispatch (general-purpose subagents).

**Spec**: `docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md`

**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063). Previous phase (A): [#2120](https://github.com/meepleAi-app/meepleai-monorepo/issues/2120) MERGED PR #2124.

---

## File Structure

| Path | Action | Responsibility |
|------|--------|---------------|
| `apps/web/scripts/audit-mockups/discover-clusters.mjs` | CREATE | Walks `admin-mockups/design_files/`, classifies into 6 clusters via filename prefix + MOCKUPS_INDEX cross-ref, emits `manifest.json` |
| `apps/web/scripts/audit-mockups/audit-output-schema.mjs` | CREATE | zod `MockupClassificationSchema` + `ClusterOutputSchema`, exported for orchestrator + generate-deliverables |
| `apps/web/scripts/audit-mockups/generate-deliverables.mjs` | CREATE | Reads aggregated audit JSON, writes 224 fidelity.json + audit summary md + designer queue + tracking-issues drafts |
| `apps/web/scripts/audit-mockups/create-tracking-issues.mjs` | CREATE | Post-signoff: reads drafts, calls `gh issue create`, updates fidelity.json `obsolete_tracking_issue` |
| `apps/web/scripts/audit-mockups/__tests__/discover-clusters.test.ts` | CREATE | Unit tests for cluster classification logic |
| `apps/web/scripts/audit-mockups/__tests__/audit-output-schema.test.ts` | CREATE | Unit tests for zod schema |
| `apps/web/scripts/audit-mockups/__tests__/generate-deliverables.test.ts` | CREATE | Unit tests for deliverable generation (uses tmpdir fixtures) |
| `apps/web/scripts/audit-mockups/__tests__/create-tracking-issues.test.ts` | CREATE | Unit tests for tracking issue creation (mocked gh execa) |
| `apps/web/package.json` | MODIFY | Add 3 scripts: `audit-mockups:discover`, `audit-mockups:generate`, `audit-mockups:create-issues` |
| `audits/2026-06-10-mockup-design-intent-manifest.json` | CREATE | Output of discover (deterministic input contract) |
| `audits/2026-06-10-mockup-design-intent-audit.json` | CREATE | Output of master orchestrator (6 cluster aggregated) |
| `audits/2026-06-10-mockup-design-intent-audit.md` | CREATE | Summary table + pair disagreements + low confidence |
| `audits/tracking-issues-drafts.md` | CREATE | One section per obsolete (NOT created until sign-off) |
| `admin-mockups/design_files/*.fidelity.json` | CREATE × 224 | Stub fidelity files (one per source file) |
| `docs/for-developers/frontend/mockup-designer-review-queue.md` | CREATE | Markdown checklist for designer |
| `CLAUDE.md` | MODIFY | Add Phase B paragraph |

---

## Task 1: Pre-flight — sub-issue + branch + budget anchor

**Files:** none (workspace setup)

- [ ] **Step 1: Verify clean branch + anchor budget timestamp**

```bash
git branch --show-current
git status --short
date -u +%Y-%m-%dT%H:%M:%SZ > /tmp/phase-b-start.txt
echo "Phase B started at: $(cat /tmp/phase-b-start.txt)"
```

Expected:
- Current branch: `feature/issue-2063-ds-17-phase-b-mockup-audit` (created at end of brainstorming session)
- Working tree shows only the spec commit
- Timestamp anchored for budget tracking

- [ ] **Step 2: Create sub-issue**

```bash
gh issue create \
  --title "[DS-17 Phase B] Mockup audit — classify 224 design_files with design_intent + designer review queue" \
  --body "$(cat <<'EOF'
## Goal

Phase B of DS-17 umbrella roadmap: classify all 224 mockup files in admin-mockups/design_files/ with explicit design_intent (current | forward-refactor | forward-refactor-obsolete), generate fidelity.json stubs, publish designer review queue, open tracking issues per obsolete (post designer sign-off).

## Context

Phase 4 prelude (#2120) MERGED PR #2124 dba7898c1 — 12 baseline PNGs captured, Storybook provider wiring fixed. Phase B unblocks Phase C (migration sweep) by giving each sub-issue numeric scope from audit output.

Per user decisions in brainstorming session 2026-06-10:
- Scope: all 224 files (HTML + JSX twins both classified)
- Method: AI-assisted (subagent fan-out)
- Designer: review queue pre-merge with magic phrase approval
- Delivery: 1 PR big bang + 1 tracking issue per obsolete
- Architecture: Opt C sequential cluster-by-cluster (6 clusters)

## Refs

- Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
- Plan: docs/superpowers/plans/2026-06-10-ds-17-phase-b-mockup-audit-plan.md
- Umbrella: #2063
- Phase A: #2120 (MERGED)

## Acceptance criteria

- [ ] 224 fidelity.json files committed in admin-mockups/design_files/
- [ ] pnpm lint:fidelity --all passes 224/224
- [ ] audits/2026-06-10-mockup-design-intent-audit.json + .md committed
- [ ] docs/for-developers/frontend/mockup-designer-review-queue.md published
- [ ] Designer sign-off comment on PR (magic phrase regex)
- [ ] N GitHub tracking issues created (1 per obsolete), referenced in fidelity.json
- [ ] PR admin-squash merged to main-dev
- [ ] Umbrella #2063 body updated with Phase B row

🤖 Generated with Claude Code
EOF
)" 2>&1 | tail -3
```

Expected: GitHub issue URL — record `#NNNN` (substitute `#TBD` everywhere). Save to `/tmp/phase-b-issue.txt`:

```bash
echo "<NNNN>" > /tmp/phase-b-issue.txt
```

---

## Task 2: Create audit-output schema (TDD)

**Files:**
- Create: `apps/web/scripts/audit-mockups/audit-output-schema.mjs`
- Create: `apps/web/scripts/audit-mockups/__tests__/audit-output-schema.test.ts`

- [ ] **Step 1: Write failing test**

Write `apps/web/scripts/audit-mockups/__tests__/audit-output-schema.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import {
  MockupClassificationSchema,
  ClusterOutputSchema,
  type MockupClassification,
} from '../audit-output-schema.mjs';

describe('MockupClassificationSchema', () => {
  const valid: MockupClassification = {
    mockup_path: 'admin-mockups/design_files/sp4-library.html',
    design_intent: 'current',
    confidence: 0.85,
    reasoning: 'No markers found. Codebase route /library matches.',
    sub_components: ['LibraryHub', 'GameCard'],
    pair_disagreement: false,
    suggested_tracking_issue: null,
  };

  it('accepts valid current classification', () => {
    expect(() => MockupClassificationSchema.parse(valid)).not.toThrow();
  });

  it('accepts forward-refactor-obsolete with tracking issue', () => {
    const obsolete: MockupClassification = {
      ...valid,
      design_intent: 'forward-refactor-obsolete',
      suggested_tracking_issue: { title: 'X', body: 'Y' },
    };
    expect(() => MockupClassificationSchema.parse(obsolete)).not.toThrow();
  });

  it('rejects missing design_intent', () => {
    const { design_intent: _ignored, ...invalid } = valid;
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow(/design_intent/);
  });

  it('rejects invalid design_intent enum', () => {
    const invalid = { ...valid, design_intent: 'obsolete' };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow();
  });

  it('rejects confidence > 1', () => {
    const invalid = { ...valid, confidence: 1.5 };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow();
  });

  it('rejects confidence < 0', () => {
    const invalid = { ...valid, confidence: -0.1 };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow();
  });

  it('requires suggested_tracking_issue when design_intent=forward-refactor-obsolete', () => {
    const invalid = {
      ...valid,
      design_intent: 'forward-refactor-obsolete',
      suggested_tracking_issue: null,
    };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow(/tracking/i);
  });
});

describe('ClusterOutputSchema', () => {
  const sample: MockupClassification = {
    mockup_path: 'admin-mockups/design_files/auth.html',
    design_intent: 'current',
    confidence: 0.9,
    reasoning: 'OK',
    sub_components: [],
    pair_disagreement: false,
    suggested_tracking_issue: null,
  };

  it('accepts array of classifications', () => {
    expect(() => ClusterOutputSchema.parse([sample, sample])).not.toThrow();
  });

  it('rejects empty array', () => {
    expect(() => ClusterOutputSchema.parse([])).toThrow();
  });
});
```

- [ ] **Step 2: Run test to verify it fails (module doesn't exist)**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/audit-output-schema.test.ts 2>&1 | tail -10
```

Expected: FAIL with "Cannot find module '../audit-output-schema.mjs'".

- [ ] **Step 3: Implement schema**

Write `apps/web/scripts/audit-mockups/audit-output-schema.mjs`:

```js
/**
 * audit-output-schema.mjs — zod schema for mockup audit output (DS-17 Phase B).
 *
 * Single source of truth for the JSON structure each cluster auditor agent emits.
 * Used by: master orchestrator (validate agent output) + generate-deliverables.mjs
 * (load aggregated audit before generating fidelity.json + queue + drafts).
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
 *   Umbrella: #2063
 */

import { z } from 'zod';

const DesignIntent = z.enum(['current', 'forward-refactor', 'forward-refactor-obsolete']);

const TrackingIssue = z.object({
  title: z.string().min(1),
  body: z.string().min(1),
});

export const MockupClassificationSchema = z
  .object({
    mockup_path: z.string().min(1),
    design_intent: DesignIntent,
    confidence: z.number().min(0).max(1),
    reasoning: z.string().min(1),
    sub_components: z.array(z.string()),
    pair_disagreement: z.boolean(),
    suggested_tracking_issue: TrackingIssue.nullable(),
  })
  .refine(
    (data) =>
      data.design_intent !== 'forward-refactor-obsolete' ||
      data.suggested_tracking_issue !== null,
    {
      message: 'suggested_tracking_issue required when design_intent=forward-refactor-obsolete',
      path: ['suggested_tracking_issue'],
    }
  );

export const ClusterOutputSchema = z.array(MockupClassificationSchema).min(1);

/** @typedef {z.infer<typeof MockupClassificationSchema>} MockupClassification */
/** @typedef {z.infer<typeof ClusterOutputSchema>} ClusterOutput */
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/audit-output-schema.test.ts 2>&1 | tail -5
```

Expected: 8 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/scripts/audit-mockups/audit-output-schema.mjs apps/web/scripts/audit-mockups/__tests__/audit-output-schema.test.ts
git commit -m "feat(audit-mockups): #TBD zod schema for mockup classification output

DS-17 Phase B Task 2: define MockupClassificationSchema + ClusterOutputSchema
with refine() rule enforcing tracking issue presence for obsolete intent.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

(Substitute `#TBD` with sub-issue number from Task 1 Step 2.)

---

## Task 3: Create discover-clusters (TDD)

**Files:**
- Create: `apps/web/scripts/audit-mockups/discover-clusters.mjs`
- Create: `apps/web/scripts/audit-mockups/__tests__/discover-clusters.test.ts`

- [ ] **Step 1: Write failing tests**

Write `apps/web/scripts/audit-mockups/__tests__/discover-clusters.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { classifyFile, groupByPair, type ClusterId } from '../discover-clusters.mjs';

describe('classifyFile', () => {
  const cases: Array<[string, ClusterId]> = [
    ['00-hub.html', 'dev-fixtures'],
    ['04-design-system.html', 'dev-fixtures'],
    ['tokens.css', 'dev-fixtures'],
    ['data.js', 'dev-fixtures'],
    ['mobile-app.jsx', 'dev-fixtures'],
    ['state-matrix.html', 'dev-fixtures'],
    ['sp4-play-records-data.js', 'dev-fixtures'],

    ['auth-flow.html', 'auth'],
    ['onboarding.html', 'auth'],
    ['notifications.html', 'auth'],
    ['public.html', 'auth'],
    ['settings.html', 'auth'],
    ['verify-email.html', 'auth'],
    ['reset-password.html', 'auth'],

    ['sp3-join.html', 'sp3'],
    ['sp3-join.jsx', 'sp3'],
    ['hub-public.html', 'sp3'],
    ['library-public.html', 'sp3'],

    ['sp4-dashboard.html', 'sp4-core'],
    ['sp4-player-detail.html', 'sp4-core'],
    ['sp4-game-night.html', 'sp4-core'],
    ['sp4-library-desktop.html', 'sp4-core'],
    ['sp4-game-detail.html', 'sp4-core'],
    ['sp4-session-summary.html', 'sp4-core'],

    ['sp4-session-live.html', 'sp4-sessions'],
    ['sp4-toolkit-detail.html', 'sp4-sessions'],
    ['sp4-scores-live.html', 'sp4-sessions'],
    ['sp4-recap.html', 'sp4-sessions'],
    ['sp4-gamebook-upload.html', 'sp4-sessions'],

    ['sp6-admin-dashboard.html', 'sp6-7-nano'],
    ['sp7-rag-config.html', 'sp6-7-nano'],
    ['admin-users.html', 'sp6-7-nano'],
    ['nano-generator.html', 'sp6-7-nano'],
    ['rag-observability.html', 'sp6-7-nano'],
    ['observability-dashboard.html', 'sp6-7-nano'],
    ['generator-config.html', 'sp6-7-nano'],
  ];

  for (const [filename, expectedCluster] of cases) {
    it(`classifies ${filename} as ${expectedCluster}`, () => {
      expect(classifyFile(filename)).toBe(expectedCluster);
    });
  }

  it('falls back to sp6-7-nano with warning for unknown', () => {
    const warnings: string[] = [];
    const cluster = classifyFile('unknown-mystery.html', (msg) => warnings.push(msg));
    expect(cluster).toBe('sp6-7-nano');
    expect(warnings).toHaveLength(1);
    expect(warnings[0]).toMatch(/unknown-mystery/);
  });
});

describe('groupByPair', () => {
  it('pairs HTML and JSX twins', () => {
    const files = [
      { path: 'admin-mockups/design_files/sp3-join.html', type: 'html' as const },
      { path: 'admin-mockups/design_files/sp3-join.jsx', type: 'jsx' as const },
      { path: 'admin-mockups/design_files/standalone.html', type: 'html' as const },
    ];

    const grouped = groupByPair(files);
    const sp3Join = grouped.find((f) => f.path.endsWith('sp3-join.html'));
    expect(sp3Join?.pairKey).toBe('sp3-join');
    const sp3JoinJsx = grouped.find((f) => f.path.endsWith('sp3-join.jsx'));
    expect(sp3JoinJsx?.pairKey).toBe('sp3-join');
    const standalone = grouped.find((f) => f.path.endsWith('standalone.html'));
    expect(standalone?.pairKey).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run tests to verify failure**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/discover-clusters.test.ts 2>&1 | tail -10
```

Expected: FAIL with "Cannot find module".

- [ ] **Step 3: Implement discover-clusters**

Write `apps/web/scripts/audit-mockups/discover-clusters.mjs`:

```js
#!/usr/bin/env node
/**
 * discover-clusters.mjs — partition mockup files into 6 clusters (DS-17 Phase B).
 *
 * Walks admin-mockups/design_files/ and emits a deterministic manifest.json
 * with files grouped into:
 *   - dev-fixtures: design system playground, datasets, tokens
 *   - auth: login/register/onboarding/notifications/settings
 *   - sp3: public hub, join, library, KB
 *   - sp4-core: dashboard, players, sessions, game-night, library, game-detail
 *   - sp4-sessions: live, toolkit, scores, recap, gamebook
 *   - sp6-7-nano: admin, RAG, observability, generators (also: fallback)
 *
 * Manifest is the deterministic input contract — same files in, same clusters out.
 * Master orchestrator consumes manifest in order, dispatching one Agent per cluster.
 *
 * Usage:
 *   node discover-clusters.mjs --out audits/2026-06-10-mockup-design-intent-manifest.json
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
 *   Umbrella: #2063
 */

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, resolve, basename, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { globSync } from 'glob';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');
const DESIGN_FILES_DIR = resolve(REPO_ROOT, 'admin-mockups', 'design_files');

/** @typedef {'dev-fixtures' | 'auth' | 'sp3' | 'sp4-core' | 'sp4-sessions' | 'sp6-7-nano'} ClusterId */

const DEV_FIXTURE_NAMES = new Set([
  '00-hub.html',
  '01-screens.html',
  '02-desktop-patterns.html',
  '03-drawer-variants.html',
  '04-design-system.html',
  '05-dark-mode.html',
  'state-matrix.html',
  'components.css',
  'data.js',
  'mobile-app.jsx',
  'tokens.css',
  'sp4-play-records-data.js',
]);

const AUTH_PREFIXES = ['auth-', 'onboarding', 'notifications', 'public', 'settings', 'verify-', 'reset-'];
const SP3_PREFIXES = ['sp3-', 'hub-', 'library-public', 'join-'];
const SP4_SESSIONS_KEYWORDS = ['live', 'toolkit', 'scores', 'recap', 'gamebook'];
const SP4_CORE_KEYWORDS = ['dashboard', 'player', 'session', 'game-night', 'library', 'game-detail'];
const SP6_7_NANO_PREFIXES = ['sp6-', 'sp7-', 'admin-', 'nano-', 'rag-', 'observability', 'generator'];

/**
 * @param {string} filename
 * @param {(msg: string) => void} [onWarn]
 * @returns {ClusterId}
 */
export function classifyFile(filename, onWarn) {
  if (DEV_FIXTURE_NAMES.has(filename)) return 'dev-fixtures';

  if (filename.startsWith('sp4-')) {
    if (SP4_SESSIONS_KEYWORDS.some((k) => filename.includes(k))) return 'sp4-sessions';
    if (SP4_CORE_KEYWORDS.some((k) => filename.includes(k))) return 'sp4-core';
  }

  if (AUTH_PREFIXES.some((p) => filename.startsWith(p))) return 'auth';
  if (SP3_PREFIXES.some((p) => filename.startsWith(p))) return 'sp3';
  if (SP6_7_NANO_PREFIXES.some((p) => filename.startsWith(p))) return 'sp6-7-nano';

  onWarn?.(`Unknown filename "${filename}" — falling back to sp6-7-nano`);
  return 'sp6-7-nano';
}

/**
 * @param {Array<{path: string, type: 'html' | 'jsx'}>} files
 * @returns {Array<{path: string, type: 'html' | 'jsx', pairKey?: string}>}
 */
export function groupByPair(files) {
  const stems = new Map();
  for (const file of files) {
    const stem = basename(file.path, extname(file.path));
    stems.set(stem, (stems.get(stem) ?? 0) + 1);
  }
  return files.map((file) => {
    const stem = basename(file.path, extname(file.path));
    if (stems.get(stem) > 1) {
      return { ...file, pairKey: stem };
    }
    return file;
  });
}

function main() {
  const argv = process.argv.slice(2);
  const outIdx = argv.indexOf('--out');
  if (outIdx === -1) {
    console.error('Usage: discover-clusters.mjs --out <path>');
    process.exit(2);
  }
  const outPath = argv[outIdx + 1];

  const htmlFiles = globSync('*.html', { cwd: DESIGN_FILES_DIR });
  const jsxFiles = globSync('*.jsx', { cwd: DESIGN_FILES_DIR });
  const cssFiles = globSync('*.css', { cwd: DESIGN_FILES_DIR });
  const jsFiles = globSync('*.js', { cwd: DESIGN_FILES_DIR });

  /** @type {Array<{path: string, type: 'html' | 'jsx'}>} */
  const allFiles = [
    ...htmlFiles.map((f) => ({ path: `admin-mockups/design_files/${f}`, type: /** @type {const} */ ('html') })),
    ...jsxFiles.map((f) => ({ path: `admin-mockups/design_files/${f}`, type: /** @type {const} */ ('jsx') })),
    ...cssFiles.map((f) => ({ path: `admin-mockups/design_files/${f}`, type: /** @type {const} */ ('html') })),
    ...jsFiles.map((f) => ({ path: `admin-mockups/design_files/${f}`, type: /** @type {const} */ ('html') })),
  ];

  const paired = groupByPair(allFiles);

  /** @type {Map<ClusterId, Array<{path: string, type: string, pairKey?: string}>>} */
  const clusters = new Map([
    ['dev-fixtures', []],
    ['auth', []],
    ['sp3', []],
    ['sp4-core', []],
    ['sp4-sessions', []],
    ['sp6-7-nano', []],
  ]);

  const warnings = [];
  for (const file of paired) {
    const clusterId = classifyFile(basename(file.path), (msg) => warnings.push(msg));
    clusters.get(clusterId).push(file);
  }

  const orderedClusters = ['dev-fixtures', 'auth', 'sp3', 'sp4-core', 'sp4-sessions', 'sp6-7-nano'];

  const manifest = {
    generatedAt: new Date().toISOString().split('T')[0],
    totalFiles: paired.length,
    warnings,
    clusters: orderedClusters.map((clusterId, idx) => ({
      clusterId,
      files: clusters.get(clusterId),
      dependencies: orderedClusters.slice(0, idx),
    })),
  };

  const absoluteOut = resolve(REPO_ROOT, outPath);
  writeFileSync(absoluteOut, JSON.stringify(manifest, null, 2) + '\n');
  console.log(`Manifest written: ${outPath} (${paired.length} files in 6 clusters)`);
  for (const c of manifest.clusters) {
    console.log(`  - ${c.clusterId}: ${c.files.length} files`);
  }
  if (warnings.length) {
    console.warn(`Warnings (${warnings.length}):`);
    for (const w of warnings) console.warn(`  ${w}`);
  }
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main();
}
```

Note: the timestamp `new Date().toISOString().split('T')[0]` makes the manifest non-deterministic. We accept this for one-shot audit (manifest is regenerated only when re-running). For unit test purposes, only the partition logic matters.

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/discover-clusters.test.ts 2>&1 | tail -5
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/web/scripts/audit-mockups/discover-clusters.mjs apps/web/scripts/audit-mockups/__tests__/discover-clusters.test.ts
git commit -m "feat(audit-mockups): #TBD discover-clusters partitions 224 files into 6 clusters

DS-17 Phase B Task 3: deterministic cluster classification via filename prefix
+ keyword matching + HTML/JSX pair detection. Manifest is the input contract
for sequential orchestrator dispatch.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: Generate manifest

**Files:**
- Create: `audits/2026-06-10-mockup-design-intent-manifest.json`

- [ ] **Step 1: Add npm script for discover**

Use Edit on `apps/web/package.json`. Find the `"scripts"` block and add (alphabetical insertion near other audit/mockup scripts):

```json
"audit-mockups:discover": "node scripts/audit-mockups/discover-clusters.mjs",
```

Place this above `"audit:assets"` or wherever audit-* scripts cluster. Verify with grep:

```bash
grep "audit-mockups:discover" apps/web/package.json
```

- [ ] **Step 2: Create audits directory if missing + run discover**

```bash
mkdir -p audits
cd apps/web && pnpm audit-mockups:discover --out ../../audits/2026-06-10-mockup-design-intent-manifest.json 2>&1 | tail -10
```

Expected output: `Manifest written: ../../audits/2026-06-10-mockup-design-intent-manifest.json (224 files in 6 clusters)` with cluster size summary.

- [ ] **Step 3: Verify manifest counts**

```bash
jq '.totalFiles, [.clusters[] | "\(.clusterId): \(.files | length)"]' audits/2026-06-10-mockup-design-intent-manifest.json
```

Expected: total = 224 (or close — exact count depends on `*.css` + `*.js` files), 6 clusters with non-zero size for each.

- [ ] **Step 4: Commit manifest**

```bash
git add apps/web/package.json audits/2026-06-10-mockup-design-intent-manifest.json
git commit -m "chore(audit-mockups): #TBD generate cluster manifest (224 files)

DS-17 Phase B Task 4: run discover-clusters.mjs and commit the deterministic
manifest as input contract for the sequential orchestrator.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: Cluster 1 audit — dev-fixtures

**Files:**
- Create: `audits/cluster-outputs/dev-fixtures.json` (partial aggregate)

- [ ] **Step 1: Read manifest cluster 1**

```bash
mkdir -p audits/cluster-outputs
jq '.clusters[] | select(.clusterId=="dev-fixtures")' audits/2026-06-10-mockup-design-intent-manifest.json > /tmp/cluster-dev-fixtures.json
jq -r '.files[].path' /tmp/cluster-dev-fixtures.json
```

Expected: list of ~14 dev-fixture file paths.

- [ ] **Step 2: Dispatch Agent for cluster 1**

Invoke the Agent tool with subagent_type=general-purpose:

- **description**: "Audit dev-fixtures cluster"
- **prompt**:
  ```
  You are auditing 14 mockup files in cluster `dev-fixtures` for the MeepleAI DS-17 Phase B audit.

  Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
  Sub-issue: #<TBD from Task 1>

  This is the FIRST cluster — no previous aggregates exist yet.

  Files to audit (read each one and classify):
  <list of 14 paths from /tmp/cluster-dev-fixtures.json>

  For each file emit ONE JSON object matching schema in
  apps/web/scripts/audit-mockups/audit-output-schema.mjs.

  Detection rules:
  - dev-fixtures are by definition NOT mapped to any user-reachable route (they are
    design system playground / fake datasets / tokens). Default classification:
    "current" with confidence 0.9-1.0 if no markers found. suggested_tracking_issue
    is null.
  - If you find explicit markers (REFACTOR-FORWARD, design-forward, deprecated),
    classify as forward-refactor or forward-refactor-obsolete accordingly and
    populate suggested_tracking_issue with a reasonable title + body for a GitHub
    tracking issue.
  - sub_components for dev-fixtures is typically [] (they are not reused by stories).
  - pair_disagreement: false (no HTML+JSX pairs expected in dev-fixtures).

  Output: a single JSON array of 14 objects (one per file). NO prose, NO comments,
  NO markdown wrapping. Start with `[` and end with `]`. Validate against schema
  before returning.
  ```

- [ ] **Step 3: Parse + validate output**

Save agent output to `audits/cluster-outputs/dev-fixtures.json`. Validate:

```bash
node -e "
import('./apps/web/scripts/audit-mockups/audit-output-schema.mjs').then(m => {
  const data = JSON.parse(require('fs').readFileSync('audits/cluster-outputs/dev-fixtures.json', 'utf-8'));
  const result = m.ClusterOutputSchema.safeParse(data);
  if (!result.success) {
    console.error('Validation FAIL:', JSON.stringify(result.error.format(), null, 2));
    process.exit(1);
  }
  console.log('Cluster dev-fixtures validated:', data.length, 'classifications');
});
"
```

Expected: validation pass, count = files in cluster.

- [ ] **Step 4: If validation fails — retry once with stricter prompt**

If Step 3 fails: re-dispatch Agent with same paths + add to prompt:
> "Previous attempt FAILED schema validation with: <error from Step 3>. Re-emit STRICTLY matching the schema. Return ONLY the JSON array."

Save new output to same path. Re-run Step 3 validation. If still fails — STOP and escalate to user.

- [ ] **Step 5: No commit yet — aggregate file is intermediate**

Cluster outputs accumulate in `audits/cluster-outputs/`. Commit only after Task 10 (final aggregation).

---

## Task 6: Cluster 2 audit — auth

**Files:**
- Create: `audits/cluster-outputs/auth.json`

- [ ] **Step 1: Read manifest cluster 2 + previous aggregates**

```bash
jq '.clusters[] | select(.clusterId=="auth")' audits/2026-06-10-mockup-design-intent-manifest.json > /tmp/cluster-auth.json
jq -r '.files[].path' /tmp/cluster-auth.json
cat audits/cluster-outputs/dev-fixtures.json
```

- [ ] **Step 2: Dispatch Agent for cluster 2**

Invoke Agent (general-purpose):

- **description**: "Audit auth cluster"
- **prompt**:
  ```
  You are auditing ~25 mockup files in cluster `auth` for the MeepleAI DS-17 Phase B audit.

  Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md

  Previous clusters' classifications (cross-reference for shared components):
  <paste contents of audits/cluster-outputs/dev-fixtures.json>

  Files to audit:
  <list of ~25 paths from /tmp/cluster-auth.json>

  For each file emit ONE JSON object matching schema in
  apps/web/scripts/audit-mockups/audit-output-schema.mjs.

  Detection rules:
  - Open each file (HTML or JSX), search for markers: REFACTOR-FORWARD, Diverge,
    Pre-Stage, design-forward, obsolete, deprecated.
  - Cross-reference codebase routes: search apps/web/src/app/ for route matching
    the mockup name (e.g., auth-flow.html → /login + /register etc.). If route
    exists and renders matching design, classify as "current". If marker indicates
    target design ahead of codebase, classify as "forward-refactor". If marker
    indicates codebase has surpassed mockup (e.g., mockup shows old auth flow,
    but app/(public)/login uses new flow), classify as "forward-refactor-obsolete"
    + populate suggested_tracking_issue.
  - HTML+JSX pair handling: classify both files independently. If pairKey in manifest
    indicates a pair, check after classification: if intents differ across pair,
    set pair_disagreement=true on BOTH.
  - sub_components: list named React components referenced in the mockup (e.g.,
    ["LoginForm", "OAuthButton"]) — use grep to find component names in HTML or JSX.

  Output: a single JSON array of objects (one per file). NO prose. Start with [ end with ].
  ```

- [ ] **Step 3: Parse + validate output**

Save to `audits/cluster-outputs/auth.json`. Run validation script (same as Task 5 Step 3, swap filename).

- [ ] **Step 4: Retry on failure (same as Task 5 Step 4)**

---

## Task 7: Cluster 3 audit — sp3

**Files:**
- Create: `audits/cluster-outputs/sp3.json`

- [ ] **Step 1: Read manifest cluster 3 + previous aggregates**

```bash
jq '.clusters[] | select(.clusterId=="sp3")' audits/2026-06-10-mockup-design-intent-manifest.json > /tmp/cluster-sp3.json
cat audits/cluster-outputs/dev-fixtures.json audits/cluster-outputs/auth.json
```

- [ ] **Step 2: Dispatch Agent for cluster 3**

Invoke Agent (general-purpose):

- **description**: "Audit sp3 cluster"
- **prompt**: SAME as Task 6 Step 2 prompt BUT:
  - Replace `auth` with `sp3`
  - Replace `~25 mockup files` with `~30 mockup files`
  - Previous clusters section: paste BOTH dev-fixtures.json AND auth.json
  - Files to audit: paths from /tmp/cluster-sp3.json
  - Detection rules notes for sp3:
    > sp3 covers public hub, shared library, join flows, KB views. Many sp3 mockups
    > were superseded by sp4 (e.g., sp3-library was replaced by sp4-library-desktop).
    > Pay extra attention to forward-refactor-obsolete candidates here.

- [ ] **Step 3: Parse + validate output**

Save to `audits/cluster-outputs/sp3.json`. Run validation.

- [ ] **Step 4: Retry on failure**

---

## Task 8: Cluster 4 audit — sp4-core

**Files:**
- Create: `audits/cluster-outputs/sp4-core.json`

- [ ] **Step 1: Read manifest cluster 4 + previous aggregates**

```bash
jq '.clusters[] | select(.clusterId=="sp4-core")' audits/2026-06-10-mockup-design-intent-manifest.json > /tmp/cluster-sp4-core.json
cat audits/cluster-outputs/dev-fixtures.json audits/cluster-outputs/auth.json audits/cluster-outputs/sp3.json
```

- [ ] **Step 2: Dispatch Agent for cluster 4**

Invoke Agent (general-purpose):

- **description**: "Audit sp4-core cluster"
- **prompt**: SAME structure as Task 6 BUT:
  - Replace `auth` with `sp4-core`
  - Replace `~25 mockup files` with `~70 mockup files (largest cluster)`
  - Previous clusters: dev-fixtures + auth + sp3
  - Files: from /tmp/cluster-sp4-core.json
  - Detection rules notes for sp4-core:
    > sp4-core is the largest cluster (~70 files) covering dashboard, players,
    > sessions, game-night, library, game-detail. KNOWN forward-refactor-obsolete
    > precedent: sp4-dashboard.{html,jsx} is documented as obsolete (Asse C #1898
    > replaced it). suggested_tracking_issue for sp4-dashboard MUST reference
    > existing issue #2114 (do not create a new tracking issue title — body should
    > say "Already tracked in #2114").
    > Library + game-detail mockups have shipped fidelity examples — they should
    > classify as "current" with high confidence.

- [ ] **Step 3: Parse + validate output**

Save to `audits/cluster-outputs/sp4-core.json`. Run validation.

- [ ] **Step 4: Retry on failure**

---

## Task 9: Cluster 5 audit — sp4-sessions

**Files:**
- Create: `audits/cluster-outputs/sp4-sessions.json`

- [ ] **Step 1: Read manifest cluster 5 + previous aggregates**

```bash
jq '.clusters[] | select(.clusterId=="sp4-sessions")' audits/2026-06-10-mockup-design-intent-manifest.json > /tmp/cluster-sp4-sessions.json
cat audits/cluster-outputs/*.json
```

- [ ] **Step 2: Dispatch Agent for cluster 5**

Invoke Agent (general-purpose):

- **description**: "Audit sp4-sessions cluster"
- **prompt**: SAME structure as Task 6 BUT:
  - Replace `auth` with `sp4-sessions`
  - Replace `~25 mockup files` with `~40 mockup files`
  - Previous clusters: all 4 prior outputs
  - Files: from /tmp/cluster-sp4-sessions.json
  - Detection rules notes for sp4-sessions:
    > sp4-sessions covers live session view, toolkit detail, scores, recap,
    > gamebook upload/edit. Session live view was substantially refactored in
    > Asse A/D #1895 sub-issues — some sp4-session-live mockups may be
    > forward-refactor-obsolete. Check codebase apps/web/src/app/(authenticated)/
    > sessions/[id]/live/_components/SessionLiveView.tsx for current state.

- [ ] **Step 3: Parse + validate output**

Save to `audits/cluster-outputs/sp4-sessions.json`. Run validation.

- [ ] **Step 4: Retry on failure**

---

## Task 10: Cluster 6 audit — sp6-7-nano

**Files:**
- Create: `audits/cluster-outputs/sp6-7-nano.json`
- Create: `audits/2026-06-10-mockup-design-intent-audit.json` (final aggregate)

- [ ] **Step 1: Read manifest cluster 6 + previous aggregates**

```bash
jq '.clusters[] | select(.clusterId=="sp6-7-nano")' audits/2026-06-10-mockup-design-intent-manifest.json > /tmp/cluster-sp6-7-nano.json
cat audits/cluster-outputs/*.json
```

- [ ] **Step 2: Dispatch Agent for cluster 6**

Invoke Agent (general-purpose):

- **description**: "Audit sp6-7-nano cluster"
- **prompt**: SAME structure as Task 6 BUT:
  - Replace `auth` with `sp6-7-nano`
  - Replace `~25 mockup files` with `~45 mockup files (final cluster)`
  - Previous clusters: all 5 prior outputs
  - Files: from /tmp/cluster-sp6-7-nano.json
  - Detection rules notes for sp6-7-nano:
    > sp6-7-nano is the catch-all for admin, RAG observability, generators.
    > Most admin/RAG mockups are research/prototype quality and may be classified
    > as forward-refactor if a target design exists but the codebase ships a
    > simpler version. Be conservative with "forward-refactor-obsolete" here —
    > prefer "forward-refactor" unless markers are explicit.

- [ ] **Step 3: Parse + validate output**

Save to `audits/cluster-outputs/sp6-7-nano.json`. Run validation.

- [ ] **Step 4: Aggregate all 6 cluster outputs into final audit JSON**

```bash
jq -s '{
  generatedAt: now | strftime("%Y-%m-%d"),
  totalClassifications: (map(length) | add),
  byCluster: {
    "dev-fixtures": .[0],
    "auth": .[1],
    "sp3": .[2],
    "sp4-core": .[3],
    "sp4-sessions": .[4],
    "sp6-7-nano": .[5]
  }
}' audits/cluster-outputs/dev-fixtures.json audits/cluster-outputs/auth.json audits/cluster-outputs/sp3.json audits/cluster-outputs/sp4-core.json audits/cluster-outputs/sp4-sessions.json audits/cluster-outputs/sp6-7-nano.json > audits/2026-06-10-mockup-design-intent-audit.json

jq '.totalClassifications' audits/2026-06-10-mockup-design-intent-audit.json
```

Expected: total matches manifest.totalFiles (~224).

- [ ] **Step 5: Commit cluster outputs + aggregated audit JSON**

```bash
git add audits/cluster-outputs/ audits/2026-06-10-mockup-design-intent-audit.json
git commit -m "chore(audit-mockups): #TBD aggregate 6 cluster audits (~224 classifications)

DS-17 Phase B Tasks 5-10: sequential cluster-by-cluster fan-out complete.
Each cluster auditor agent emitted structured JSON validated against zod schema;
final aggregate is the source for generate-deliverables.mjs (next task).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 11: Create generate-deliverables (TDD)

**Files:**
- Create: `apps/web/scripts/audit-mockups/generate-deliverables.mjs`
- Create: `apps/web/scripts/audit-mockups/__tests__/generate-deliverables.test.ts`

- [ ] **Step 1: Write failing tests**

Write `apps/web/scripts/audit-mockups/__tests__/generate-deliverables.test.ts`:

```ts
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, rmSync, readFileSync, existsSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { generateDeliverables } from '../generate-deliverables.mjs';

describe('generateDeliverables', () => {
  let workDir: string;

  beforeEach(() => {
    workDir = mkdtempSync(join(tmpdir(), 'audit-deliverables-'));
  });

  afterEach(() => {
    rmSync(workDir, { recursive: true, force: true });
  });

  it('generates one fidelity.json per classification', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 2,
      byCluster: {
        'dev-fixtures': [
          {
            mockup_path: 'admin-mockups/design_files/00-hub.html',
            design_intent: 'current',
            confidence: 0.95,
            reasoning: 'No markers',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: null,
          },
        ],
        auth: [],
        sp3: [],
        'sp4-core': [
          {
            mockup_path: 'admin-mockups/design_files/sp4-dashboard.html',
            design_intent: 'forward-refactor-obsolete',
            confidence: 0.9,
            reasoning: 'Asse C #1898',
            sub_components: ['DashboardClient'],
            pair_disagreement: false,
            suggested_tracking_issue: { title: 'Tracked in #2114', body: 'Already tracked' },
          },
        ],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    expect(existsSync(join(workDir, 'design_files/00-hub.fidelity.json'))).toBe(true);
    expect(existsSync(join(workDir, 'design_files/sp4-dashboard.fidelity.json'))).toBe(true);

    const obsoleteFidelity = JSON.parse(
      readFileSync(join(workDir, 'design_files/sp4-dashboard.fidelity.json'), 'utf-8')
    );
    expect(obsoleteFidelity.acceptance.design_intent).toBe('forward-refactor-obsolete');
    expect(obsoleteFidelity.acceptance.obsolete_tracking_issue).toBe('');
  });

  it('emits designer queue with obsolete and pair_disagreement sections', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 2,
      byCluster: {
        'dev-fixtures': [],
        auth: [
          {
            mockup_path: 'admin-mockups/design_files/auth-flow.html',
            design_intent: 'current',
            confidence: 0.4,
            reasoning: 'Ambiguous',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: null,
          },
        ],
        sp3: [],
        'sp4-core': [
          {
            mockup_path: 'admin-mockups/design_files/sp4-old.html',
            design_intent: 'forward-refactor-obsolete',
            confidence: 0.9,
            reasoning: 'Replaced',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: { title: 'Mark obsolete', body: 'Replaced by X' },
          },
        ],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/mockup-designer-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/Obsolete candidates/);
    expect(queue).toMatch(/sp4-old\.html/);
    expect(queue).toMatch(/Low confidence/);
    expect(queue).toMatch(/auth-flow\.html/);
  });

  it('emits tracking-issues-drafts.md with one section per obsolete', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 1,
      byCluster: {
        'dev-fixtures': [],
        auth: [],
        sp3: [],
        'sp4-core': [
          {
            mockup_path: 'admin-mockups/design_files/sp4-x.html',
            design_intent: 'forward-refactor-obsolete',
            confidence: 0.9,
            reasoning: 'R',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: { title: 'X obsolete', body: 'Body Y' },
          },
        ],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    const drafts = readFileSync(join(workDir, 'audits-out/tracking-issues-drafts.md'), 'utf-8');
    expect(drafts).toMatch(/X obsolete/);
    expect(drafts).toMatch(/Body Y/);
  });

  it('emits summary markdown with cluster table', () => {
    const audit = {
      generatedAt: '2026-06-10',
      totalClassifications: 1,
      byCluster: {
        'dev-fixtures': [
          {
            mockup_path: 'admin-mockups/design_files/00-hub.html',
            design_intent: 'current',
            confidence: 0.95,
            reasoning: 'R',
            sub_components: [],
            pair_disagreement: false,
            suggested_tracking_issue: null,
          },
        ],
        auth: [],
        sp3: [],
        'sp4-core': [],
        'sp4-sessions': [],
        'sp6-7-nano': [],
      },
    };

    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateDeliverables({
      auditPath,
      mockupsDir: join(workDir, 'design_files'),
      auditsDir: join(workDir, 'audits-out'),
      docsDir: join(workDir, 'docs-out'),
    });

    const summary = readFileSync(
      join(workDir, 'audits-out/2026-06-10-mockup-design-intent-audit.md'),
      'utf-8'
    );
    expect(summary).toMatch(/Summary/);
    expect(summary).toMatch(/dev-fixtures.*1/);
  });
});
```

- [ ] **Step 2: Run tests to verify failure**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/generate-deliverables.test.ts 2>&1 | tail -10
```

Expected: FAIL with "Cannot find module".

- [ ] **Step 3: Implement generate-deliverables.mjs**

Write `apps/web/scripts/audit-mockups/generate-deliverables.mjs`:

```js
#!/usr/bin/env node
/**
 * generate-deliverables.mjs — emit 224 fidelity.json + 3 markdown deliverables.
 *
 * Reads the aggregated audit JSON (output of master orchestrator across 6 sequential
 * cluster auditor agents) and produces:
 *   - admin-mockups/design_files/<name>.fidelity.json (one per classified file)
 *   - audits/<date>-mockup-design-intent-audit.md (summary table)
 *   - docs/for-developers/frontend/mockup-designer-review-queue.md (designer checklist)
 *   - audits/tracking-issues-drafts.md (one section per obsolete)
 *
 * Tracking issues are NOT created here — that happens post designer sign-off via
 * create-tracking-issues.mjs.
 *
 * Usage:
 *   node generate-deliverables.mjs --audit audits/<date>-mockup-design-intent-audit.json
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
 *   Umbrella: #2063
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname, resolve, basename, extname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');

const FIDELITY_TEMPLATE = (mockupPath, designIntent) => ({
  _comment: `Generated by Phase B audit. See audits/<date>-mockup-design-intent-audit.json for source.`,
  mockup: {
    source: mockupPath,
    states: ['default'],
  },
  acceptance: {
    visual_diff_max_px: 5,
    color_delta_e_max: 3,
    tokens_used: 'canonical_only',
    legacy_token_names_forbidden: true,
    states_covered: ['default'],
    a11y_axe: 'AA',
    a11y_violations_max: 0,
    responsive_breakpoints: [375, 768, 1024, 1440],
    designer_approved_by: '',
    designer_approved_on: '',
    story_path: '',
    fixtures_path: '',
    design_intent: designIntent,
    viewports: ['desktop'],
    obsolete_tracking_issue: '',
  },
});

/**
 * @param {{
 *   auditPath: string,
 *   mockupsDir: string,
 *   auditsDir: string,
 *   docsDir: string
 * }} opts
 */
export function generateDeliverables(opts) {
  const { auditPath, mockupsDir, auditsDir, docsDir } = opts;
  const audit = JSON.parse(readFileSync(auditPath, 'utf-8'));

  mkdirSync(mockupsDir, { recursive: true });
  mkdirSync(auditsDir, { recursive: true });
  mkdirSync(docsDir, { recursive: true });

  const all = [];
  for (const clusterId of Object.keys(audit.byCluster)) {
    for (const item of audit.byCluster[clusterId]) {
      all.push({ ...item, clusterId });
    }
  }

  // 1. Write 1 fidelity.json per classification
  for (const item of all) {
    const sourceName = basename(item.mockup_path);
    const fidelityName = sourceName.replace(/\.(html|jsx|js|css)$/, '.fidelity.json');
    const fidelityPath = join(mockupsDir, fidelityName);
    const content = FIDELITY_TEMPLATE(item.mockup_path, item.design_intent);
    writeFileSync(fidelityPath, JSON.stringify(content, null, 2) + '\n');
  }

  // 2. Summary markdown
  const summaryRows = ['dev-fixtures', 'auth', 'sp3', 'sp4-core', 'sp4-sessions', 'sp6-7-nano'].map(
    (clusterId) => {
      const items = audit.byCluster[clusterId] || [];
      const current = items.filter((i) => i.design_intent === 'current').length;
      const forward = items.filter((i) => i.design_intent === 'forward-refactor').length;
      const obsolete = items.filter((i) => i.design_intent === 'forward-refactor-obsolete').length;
      return `| ${clusterId} | ${items.length} | ${current} | ${forward} | ${obsolete} |`;
    }
  );

  const pairDisagreements = all.filter((i) => i.pair_disagreement);
  const lowConfidence = all.filter((i) => i.confidence < 0.6);

  const summary = `# Mockup Design Intent Audit — ${audit.generatedAt}

## Summary

| Cluster | Total | current | forward-refactor | forward-refactor-obsolete |
|---------|-------|---------|------------------|---------------------------|
${summaryRows.join('\n')}

## Pair disagreements

${
  pairDisagreements.length
    ? pairDisagreements.map((i) => `- ${i.mockup_path} (${i.clusterId}) — ${i.reasoning}`).join('\n')
    : '_None._'
}

## Low confidence (< 0.6)

${
  lowConfidence.length
    ? lowConfidence.map((i) => `- ${i.mockup_path} (confidence ${i.confidence}): ${i.reasoning}`).join('\n')
    : '_None._'
}
`;

  writeFileSync(
    join(auditsDir, `${audit.generatedAt}-mockup-design-intent-audit.md`),
    summary
  );

  // 3. Designer review queue
  const obsolete = all.filter((i) => i.design_intent === 'forward-refactor-obsolete');
  const queue = `# Mockup Designer Review Queue — DS-17 Phase B

**Source**: \`audits/${audit.generatedAt}-mockup-design-intent-audit.md\`
**Generated**: ${audit.generatedAt}
**Auditor**: AI subagent fan-out (Phase B)

## How to approve

Comment on this PR with:

\`\`\`
DESIGNER APPROVED: ${audit.generatedAt} <your-name>
\`\`\`

After approval, tracking issues are created for \`forward-refactor-obsolete\` entries.

## Obsolete candidates (require review)

${
  obsolete.length
    ? obsolete
        .map(
          (i) =>
            `- [ ] \`${i.mockup_path}\` — ${i.reasoning}\n  - Suggested tracking: ${i.suggested_tracking_issue.title}`
        )
        .join('\n')
    : '_None._'
}

## Pair disagreements (require designer arbitration)

${
  pairDisagreements.length
    ? pairDisagreements.map((i) => `- [ ] \`${i.mockup_path}\` — ${i.reasoning}`).join('\n')
    : '_None._'
}

## Low confidence (< 0.6, optional review)

${
  lowConfidence.length
    ? lowConfidence
        .map((i) => `- [ ] \`${i.mockup_path}\` (confidence ${i.confidence}): ${i.reasoning}`)
        .join('\n')
    : '_None._'
}
`;

  writeFileSync(join(docsDir, 'mockup-designer-review-queue.md'), queue);

  // 4. Tracking issue drafts
  const drafts = `# Tracking Issues Drafts — DS-17 Phase B

NOT created until designer sign-off. After approval, \`create-tracking-issues.mjs\`
reads this file and creates GitHub issues.

${obsolete
  .map(
    (i, idx) => `## Draft ${idx + 1}: ${i.mockup_path}

**Title**: \`${i.suggested_tracking_issue.title}\`

**Body**:

${i.suggested_tracking_issue.body}

---
`
  )
  .join('\n')}
`;

  writeFileSync(join(auditsDir, 'tracking-issues-drafts.md'), drafts);

  console.log('Deliverables written:');
  console.log(`  - ${all.length} fidelity.json in ${mockupsDir}`);
  console.log(`  - 1 summary in ${auditsDir}`);
  console.log(`  - 1 designer queue in ${docsDir}`);
  console.log(`  - 1 tracking-issues-drafts in ${auditsDir}`);
  console.log(`  - Obsolete count: ${obsolete.length}`);
  console.log(`  - Pair disagreements: ${pairDisagreements.length}`);
  console.log(`  - Low confidence: ${lowConfidence.length}`);
}

function main() {
  const argv = process.argv.slice(2);
  const auditIdx = argv.indexOf('--audit');
  if (auditIdx === -1) {
    console.error('Usage: generate-deliverables.mjs --audit <path>');
    process.exit(2);
  }
  const auditPath = resolve(REPO_ROOT, argv[auditIdx + 1]);

  generateDeliverables({
    auditPath,
    mockupsDir: resolve(REPO_ROOT, 'admin-mockups/design_files'),
    auditsDir: resolve(REPO_ROOT, 'audits'),
    docsDir: resolve(REPO_ROOT, 'docs/for-developers/frontend'),
  });
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main();
}
```

- [ ] **Step 4: Run tests to verify pass**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/generate-deliverables.test.ts 2>&1 | tail -5
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/scripts/audit-mockups/generate-deliverables.mjs apps/web/scripts/audit-mockups/__tests__/generate-deliverables.test.ts
git commit -m "feat(audit-mockups): #TBD generate-deliverables produces fidelity.json + queue + drafts

DS-17 Phase B Task 11: read aggregated audit JSON, emit one fidelity.json per
classification + summary md + designer review queue + tracking issues drafts.
Tracking issues NOT created here — that happens post designer sign-off.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 12: Run generate-deliverables — produce ~224 fidelity.json

**Files:**
- Create: `admin-mockups/design_files/*.fidelity.json` (~224 files)
- Create: `audits/2026-06-10-mockup-design-intent-audit.md`
- Create: `docs/for-developers/frontend/mockup-designer-review-queue.md`
- Create: `audits/tracking-issues-drafts.md`

- [ ] **Step 1: Add npm script for generate**

Use Edit on `apps/web/package.json`. Add (near `audit-mockups:discover`):

```json
"audit-mockups:generate": "node scripts/audit-mockups/generate-deliverables.mjs",
```

- [ ] **Step 2: Run generate**

```bash
cd apps/web && pnpm audit-mockups:generate --audit ../../audits/2026-06-10-mockup-design-intent-audit.json 2>&1 | tail -15
```

Expected output:
```
Deliverables written:
  - ~224 fidelity.json in /.../admin-mockups/design_files
  - 1 summary in /.../audits
  - 1 designer queue in /.../docs/for-developers/frontend
  - 1 tracking-issues-drafts in /.../audits
  - Obsolete count: N
  - Pair disagreements: M
  - Low confidence: K
```

- [ ] **Step 3: Verify file counts**

```bash
ls admin-mockups/design_files/*.fidelity.json | wc -l
# Expected: matches total classifications (~224)

ls -la docs/for-developers/frontend/mockup-designer-review-queue.md \
       audits/2026-06-10-mockup-design-intent-audit.md \
       audits/tracking-issues-drafts.md
# Expected: all 3 files exist
```

- [ ] **Step 4: Verify lint:fidelity passes on generated files**

```bash
cd apps/web && pnpm lint:fidelity 2>&1 | tail -15
```

Expected: all generated fidelity.json files validate against `validate-fidelity.mjs` schema. Count matches the number of files written.

If failures: inspect the first failing file:

```bash
cd apps/web && node scripts/mockup-annotations/validate-fidelity.mjs ../../admin-mockups/design_files/<first-failing>.fidelity.json
```

Common issue: schema in `FIDELITY_TEMPLATE` of `generate-deliverables.mjs` doesn't match `validate-fidelity.mjs` schema. Fix by inspecting the existing schema definition and updating `FIDELITY_TEMPLATE`.

- [ ] **Step 5: Commit deliverables**

```bash
git add admin-mockups/design_files/*.fidelity.json
git add audits/2026-06-10-mockup-design-intent-audit.md
git add audits/tracking-issues-drafts.md
git add docs/for-developers/frontend/mockup-designer-review-queue.md
git add apps/web/package.json
git commit -m "feat(audit-mockups): #TBD generate 224 fidelity.json + designer queue + drafts

DS-17 Phase B Task 12: run generate-deliverables.mjs on aggregated audit.
Output:
- ~224 admin-mockups/design_files/<name>.fidelity.json stubs
- audits/2026-06-10-mockup-design-intent-audit.md (summary)
- docs/for-developers/frontend/mockup-designer-review-queue.md
- audits/tracking-issues-drafts.md

pnpm lint:fidelity passes 224/224.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 13: Create create-tracking-issues script (TDD)

**Files:**
- Create: `apps/web/scripts/audit-mockups/create-tracking-issues.mjs`
- Create: `apps/web/scripts/audit-mockups/__tests__/create-tracking-issues.test.ts`

- [ ] **Step 1: Write failing tests**

Write `apps/web/scripts/audit-mockups/__tests__/create-tracking-issues.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mkdtempSync, rmSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { parseDrafts, updateFidelityForIssue } from '../create-tracking-issues.mjs';

describe('parseDrafts', () => {
  it('extracts mockup paths + titles + bodies from drafts markdown', () => {
    const drafts = `# Tracking Issues Drafts — DS-17 Phase B

NOT created until designer sign-off.

## Draft 1: admin-mockups/design_files/sp4-old.html

**Title**: \`Mark sp4-old obsolete\`

**Body**:

Replaced by sp4-new in Asse C #1898.

---

## Draft 2: admin-mockups/design_files/sp3-deprecated.html

**Title**: \`Track sp3-deprecated\`

**Body**:

Reason for obsolescence.

---
`;

    const drafts2 = parseDrafts(drafts);
    expect(drafts2).toHaveLength(2);
    expect(drafts2[0].mockup_path).toBe('admin-mockups/design_files/sp4-old.html');
    expect(drafts2[0].title).toBe('Mark sp4-old obsolete');
    expect(drafts2[0].body).toMatch(/Replaced by sp4-new/);
    expect(drafts2[1].mockup_path).toBe('admin-mockups/design_files/sp3-deprecated.html');
  });

  it('returns empty array on no drafts', () => {
    const drafts = `# Tracking Issues Drafts — DS-17 Phase B\n\n_No obsolete classifications._`;
    expect(parseDrafts(drafts)).toEqual([]);
  });
});

describe('updateFidelityForIssue', () => {
  let workDir: string;

  beforeEach(() => {
    workDir = mkdtempSync(join(tmpdir(), 'fidelity-update-'));
  });

  it('updates obsolete_tracking_issue field', () => {
    const fidelity = {
      _comment: '',
      mockup: { source: 'admin-mockups/design_files/sp4-old.html', states: ['default'] },
      acceptance: {
        design_intent: 'forward-refactor-obsolete',
        obsolete_tracking_issue: '',
        visual_diff_max_px: 5,
        color_delta_e_max: 3,
        tokens_used: 'canonical_only',
        legacy_token_names_forbidden: true,
        states_covered: ['default'],
        a11y_axe: 'AA',
        a11y_violations_max: 0,
        responsive_breakpoints: [375, 768, 1024, 1440],
        designer_approved_by: '',
        designer_approved_on: '',
        story_path: '',
        fixtures_path: '',
        viewports: ['desktop'],
      },
    };
    const path = join(workDir, 'sp4-old.fidelity.json');
    writeFileSync(path, JSON.stringify(fidelity, null, 2));

    updateFidelityForIssue(path, 9999);

    const after = JSON.parse(readFileSync(path, 'utf-8'));
    expect(after.acceptance.obsolete_tracking_issue).toBe('#9999');
  });
});
```

- [ ] **Step 2: Run tests to verify failure**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/create-tracking-issues.test.ts 2>&1 | tail -10
```

Expected: FAIL with "Cannot find module".

- [ ] **Step 3: Implement create-tracking-issues**

Write `apps/web/scripts/audit-mockups/create-tracking-issues.mjs`:

```js
#!/usr/bin/env node
/**
 * create-tracking-issues.mjs — post designer sign-off: create GH issues + update fidelity refs.
 *
 * Reads audits/tracking-issues-drafts.md, parses each Draft N section, runs
 * `gh issue create` for each, updates the corresponding admin-mockups/design_files/
 * <name>.fidelity.json with the new issue number in `obsolete_tracking_issue`.
 *
 * Usage:
 *   node create-tracking-issues.mjs --drafts audits/tracking-issues-drafts.md
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
 *   Umbrella: #2063
 */

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');

/**
 * @param {string} draftsContent
 * @returns {Array<{mockup_path: string, title: string, body: string}>}
 */
export function parseDrafts(draftsContent) {
  const sections = draftsContent.split(/^## Draft \d+: /m).slice(1);
  /** @type {Array<{mockup_path: string, title: string, body: string}>} */
  const result = [];
  for (const section of sections) {
    const lines = section.split('\n');
    const mockup_path = lines[0].trim();
    const titleMatch = section.match(/\*\*Title\*\*: `([^`]+)`/);
    const bodyMatch = section.match(/\*\*Body\*\*:\s*\n\s*\n([\s\S]+?)\n---/);
    if (titleMatch && bodyMatch) {
      result.push({
        mockup_path,
        title: titleMatch[1],
        body: bodyMatch[1].trim(),
      });
    }
  }
  return result;
}

/**
 * @param {string} fidelityPath
 * @param {number} issueNumber
 */
export function updateFidelityForIssue(fidelityPath, issueNumber) {
  const content = JSON.parse(readFileSync(fidelityPath, 'utf-8'));
  content.acceptance.obsolete_tracking_issue = `#${issueNumber}`;
  writeFileSync(fidelityPath, JSON.stringify(content, null, 2) + '\n');
}

/**
 * @param {string} title
 * @param {string} body
 * @returns {number} issue number
 */
function createGithubIssue(title, body) {
  // Use heredoc-equivalent via stdin to avoid shell escaping issues
  const cmd = `gh issue create --title ${JSON.stringify(title)} --body-file -`;
  const result = execSync(cmd, { input: body, encoding: 'utf-8' });
  const match = result.match(/\/issues\/(\d+)$/m);
  if (!match) {
    throw new Error(`Failed to parse issue number from gh output: ${result}`);
  }
  return parseInt(match[1], 10);
}

function fidelityPathFor(mockup_path) {
  const sourceName = basename(mockup_path);
  const fidelityName = sourceName.replace(/\.(html|jsx|js|css)$/, '.fidelity.json');
  return resolve(REPO_ROOT, 'admin-mockups/design_files', fidelityName);
}

function main() {
  const argv = process.argv.slice(2);
  const draftsIdx = argv.indexOf('--drafts');
  if (draftsIdx === -1) {
    console.error('Usage: create-tracking-issues.mjs --drafts <path>');
    process.exit(2);
  }
  const draftsPath = resolve(REPO_ROOT, argv[draftsIdx + 1]);
  const drafts = parseDrafts(readFileSync(draftsPath, 'utf-8'));

  if (drafts.length === 0) {
    console.log('No drafts to process. Exiting.');
    return;
  }

  console.log(`Processing ${drafts.length} drafts...`);
  /** @type {Array<{draft: typeof drafts[0], issueNumber: number}>} */
  const created = [];

  for (const draft of drafts) {
    try {
      console.log(`Creating issue: ${draft.title}`);
      const issueNumber = createGithubIssue(draft.title, draft.body);
      created.push({ draft, issueNumber });
      const fidelityPath = fidelityPathFor(draft.mockup_path);
      if (existsSync(fidelityPath)) {
        updateFidelityForIssue(fidelityPath, issueNumber);
        console.log(`  → #${issueNumber} + updated ${basename(fidelityPath)}`);
      } else {
        console.warn(`  → #${issueNumber} but fidelity file missing: ${fidelityPath}`);
      }
    } catch (err) {
      console.error(`FAILED creating issue for ${draft.mockup_path}:`, err.message);
      console.error(`Rolling back: closing ${created.length} already-created issues...`);
      for (const c of created) {
        try {
          execSync(`gh issue close ${c.issueNumber} --comment "Rollback: Phase B batch failed."`);
        } catch (_) {}
      }
      process.exit(1);
    }
  }

  console.log(`Done. Created ${created.length} issues, updated ${created.length} fidelity files.`);
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main();
}
```

- [ ] **Step 4: Run tests to verify pass**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/create-tracking-issues.test.ts 2>&1 | tail -5
```

Expected: 3 passed.

- [ ] **Step 5: Add npm script + commit**

Use Edit on `apps/web/package.json` to add:

```json
"audit-mockups:create-issues": "node scripts/audit-mockups/create-tracking-issues.mjs",
```

Commit:

```bash
git add apps/web/scripts/audit-mockups/create-tracking-issues.mjs apps/web/scripts/audit-mockups/__tests__/create-tracking-issues.test.ts apps/web/package.json
git commit -m "feat(audit-mockups): #TBD create-tracking-issues post designer sign-off

DS-17 Phase B Task 13: parse drafts markdown, call gh issue create per obsolete
+ update corresponding fidelity.json with new issue number. Rollback on partial
failure (close any already-created issues).

NOT invoked yet — runs post-merge after designer signs off (Task 17).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 14: Update CLAUDE.md with Phase B paragraph

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add Phase B paragraph after Phase 4 prelude reference**

Use Edit on `CLAUDE.md`. Find the line:

```markdown
**Baseline 12 PNGs captured** (Library 9 + GameDetail 3) post DS-17 Phase 4 prelude #2120 — actual root cause was NOT dual react-intl modules but duplicate `preview.{ts,tsx}` files (legacy `preview.ts` loaded instead of Phase 2.5 `preview.tsx`) + missing `staticDirs`/MSW Service Worker + missing `parameters.nextjs.navigation` global mock. CI gate `continue-on-error: true` (`--blocking` flip post 14gg stable trajectory). Fix log: [`docs/for-developers/frontend/page-mock-story-pattern.md`](./docs/for-developers/frontend/page-mock-story-pattern.md) § Fix log Phase 4 prelude.
```

Add a new paragraph immediately after:

```markdown

**Mockup audit — DS-17 Phase B (#TBD)** — every mockup in `admin-mockups/design_files/` carries a `<name>.fidelity.json` stub with `design_intent` ∈ `{current, forward-refactor, forward-refactor-obsolete}` classified via sequential cluster-by-cluster AI audit (6 clusters: dev-fixtures, auth, sp3, sp4-core, sp4-sessions, sp6-7-nano). For new mockups, copy `docs/for-developers/frontend/templates/examples/sp4-library-desktop.fidelity.json` and update `design_intent` + `obsolete_tracking_issue`. Run `pnpm lint:fidelity` to validate. Audit output: `audits/2026-06-10-mockup-design-intent-audit.{json,md}`. Designer review queue: `docs/for-developers/frontend/mockup-designer-review-queue.md`.
```

(Substitute `#TBD` with sub-issue number.)

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: #TBD CLAUDE.md note for Phase B mockup audit (DS-17)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 15: Final verification + push + PR

**Files:** (none modified — pre-merge verify)

- [ ] **Step 1: Full verify**

```bash
cd apps/web && pnpm typecheck 2>&1 | tail -5
cd apps/web && pnpm lint 2>&1 | tail -5
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/ 2>&1 | tail -10
cd apps/web && pnpm lint:fidelity 2>&1 | tail -10
```

Expected: typecheck + lint + vitest all clean. lint:fidelity reports ~224 valid files.

- [ ] **Step 2: Verify git status is clean**

```bash
git status --short
```

Expected: no untracked or uncommitted changes (everything committed across Tasks 2-14).

- [ ] **Step 3: Push branch**

```bash
git push -u origin feature/issue-2063-ds-17-phase-b-mockup-audit 2>&1 | tail -5
```

- [ ] **Step 4: Create PR**

```bash
gh pr create --base main-dev --head feature/issue-2063-ds-17-phase-b-mockup-audit \
  --title "feat(audit-mockups): #TBD DS-17 Phase B — classify 224 mockups + designer review queue" \
  --body "$(cat <<'EOF'
## Goal

Phase B of DS-17 umbrella roadmap: classify all ~224 mockup files in admin-mockups/design_files/ with explicit design_intent + generate fidelity.json stubs + publish designer review queue. Tracking issues for obsoletes will be created post-designer-sign-off via `pnpm audit-mockups:create-issues`.

## What's in this PR

- 4 scripts under `apps/web/scripts/audit-mockups/`:
  - `discover-clusters.mjs` — deterministic 6-cluster partition manifest
  - `audit-output-schema.mjs` — zod schema for cluster auditor output
  - `generate-deliverables.mjs` — emit fidelity.json + queue + summary + drafts
  - `create-tracking-issues.mjs` — post-signoff gh issue creation + fidelity update
- Vitest unit tests for all 4 (TDD)
- `audits/2026-06-10-mockup-design-intent-manifest.json` — cluster manifest
- `audits/2026-06-10-mockup-design-intent-audit.json` — aggregated audit output
- `audits/2026-06-10-mockup-design-intent-audit.md` — summary table
- `audits/tracking-issues-drafts.md` — N obsolete drafts (NOT created yet)
- `docs/for-developers/frontend/mockup-designer-review-queue.md` — designer checklist
- ~224 `admin-mockups/design_files/<name>.fidelity.json` — stub fidelity files (all pass `pnpm lint:fidelity`)
- `CLAUDE.md` — Phase B paragraph

## DESIGNER REVIEW REQUIRED

Please review **`docs/for-developers/frontend/mockup-designer-review-queue.md`** in this PR. Sections:

1. **Obsolete candidates** — confirm each `forward-refactor-obsolete` classification
2. **Pair disagreements** — arbitrate HTML vs JSX twin classification
3. **Low confidence** (optional) — review borderline classifications

To approve, comment on this PR with:

\`\`\`
DESIGNER APPROVED: <ISO date> <your name>
\`\`\`

Example: \`DESIGNER APPROVED: 2026-06-15 alice-doe\`

Magic phrase regex: \`^DESIGNER APPROVED: \\d{4}-\\d{2}-\\d{2} [\\w\\s-]+$\`

After approval, run `pnpm audit-mockups:create-issues` from main-dev to create N GitHub tracking issues + update fidelity refs + amend this PR.

## Acceptance criteria

- [x] ~224 fidelity.json files committed
- [x] pnpm lint:fidelity passes
- [x] Audit summary committed
- [x] Designer queue published
- [ ] Designer sign-off comment (this PR)
- [ ] N tracking issues created (post sign-off, separate amend commit)
- [ ] Merged to main-dev (after sign-off + amend)

## Refs

- Closes #TBD
- Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
- Phase A: #2120 (MERGED PR #2124)
- Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
- Plan: docs/superpowers/plans/2026-06-10-ds-17-phase-b-mockup-audit-plan.md

🤖 Generated with Claude Code
EOF
)"
```

Expected: PR URL returned.

---

## Task 16: Wait for designer sign-off

**Files:** none (waiting on external review)

- [ ] **Step 1: Notify user PR is ready**

Report PR URL to user. Explain that the next step (Task 17) requires designer to comment with the magic phrase. User will signal when sign-off has been posted.

- [ ] **Step 2: Poll for sign-off (only when user signals to proceed)**

Once user confirms designer has commented:

```bash
gh pr view <PR#> --json comments --jq '.comments[].body' | grep -E '^DESIGNER APPROVED: [0-9]{4}-[0-9]{2}-[0-9]{2} [a-zA-Z0-9\s-]+$' | head -1
```

Expected: line matching the magic phrase regex. If empty → ask user to confirm designer signed off.

---

## Task 17: Post-signoff — create tracking issues + amend PR

**Files:**
- Modify: `admin-mockups/design_files/*.fidelity.json` (N files, only obsoletes)

- [ ] **Step 1: Run create-tracking-issues**

```bash
cd apps/web && pnpm audit-mockups:create-issues --drafts ../../audits/tracking-issues-drafts.md 2>&1 | tail -20
```

Expected output:
```
Processing N drafts...
Creating issue: <title>
  → #NNNN + updated <name>.fidelity.json
...
Done. Created N issues, updated N fidelity files.
```

If any failure: script auto-rolls back (closes any already-created issues). User must re-attempt after investigation.

- [ ] **Step 2: Verify fidelity.json updates**

```bash
grep -l "obsolete_tracking_issue.*#[0-9]" admin-mockups/design_files/*.fidelity.json | wc -l
# Expected: matches N (count of obsoletes from drafts.md)
```

- [ ] **Step 3: Commit + push amend**

```bash
git add admin-mockups/design_files/*.fidelity.json
git commit -m "chore(audit-mockups): #TBD link N tracking issues into fidelity files

Post designer sign-off — created N tracking issues for forward-refactor-obsolete
mockups and updated their fidelity.json obsolete_tracking_issue refs.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
git push 2>&1 | tail -3
```

---

## Task 18: Admin-squash merge + cleanup

**Files:** none (git ops)

- [ ] **Step 1: Admin-squash merge**

```bash
gh pr merge <PR#> --squash --admin --delete-branch \
  --subject "feat(audit-mockups): #TBD DS-17 Phase B — classify 224 mockups + designer review queue (#<PR#>)" 2>&1 | tail -5
```

- [ ] **Step 2: Verify merge**

```bash
gh pr view <PR#> --json state,mergedAt,mergeCommit --jq '{state, mergedAt, sha: .mergeCommit.oid[0:9]}'
```

Expected: state=MERGED, mergedAt ISO timestamp, short SHA.

- [ ] **Step 3: Cleanup + sync main-dev**

```bash
git stash push -m "transient-locks" -- .claude/scheduled_tasks.lock 2>&1 | head -2; true
git checkout main-dev
git pull --ff-only 2>&1 | tail -5
git branch -D feature/issue-2063-ds-17-phase-b-mockup-audit 2>&1 || true
git stash drop 0 2>&1 || true
git log --oneline -3
```

Expected: main-dev tip is the squash merge commit.

---

## Task 19: Umbrella body update

**Files:**
- Modify: GitHub issue #2063 body via `gh issue edit`

- [ ] **Step 1: Fetch current umbrella body**

```bash
gh issue view 2063 --json body --jq .body > umbrella-2063-body.md
grep -n "Phase 4 prelude\|Phase B" umbrella-2063-body.md | head -5
```

- [ ] **Step 2: Add Phase B row after Phase 4 prelude row**

Use Edit on `umbrella-2063-body.md`. Find:

```markdown
- [x] **DS-17 Phase 4 prelude** (#2120): Storybook provider wiring fix + 12 baseline PNGs captured → PR #2124 merged `dba7898c1` (admin-squash P145 33a volta). Removed legacy `preview.ts`, enabled `staticDirs`, generated `mockServiceWorker.js`, wired global `parameters.nextjs.navigation`, switched to production `IntlProvider` wrapper. Diagnostic regression test added (`apps/web/e2e/storybook/diagnostic.snapshot.spec.ts`). CI step still `continue-on-error: true` — `--blocking` flip deferred until 14gg stable trajectory.
```

Add immediately after:

```markdown

- [x] **DS-17 Phase B mockup audit** (#TBD): 224 fidelity.json stubs + audit summary + designer review queue + N tracking issues for obsoletes → PR #<PR#> merged `<SHA>` (admin-squash P145 34a volta). Sequential cluster-by-cluster fan-out: 6 ordered clusters (dev-fixtures, auth, sp3, sp4-core, sp4-sessions, sp6-7-nano). Designer sign-off via magic phrase on PR. Unblocks Phase 3 sweep by giving each sub-issue numeric scope from audit output.
```

Substitute `#TBD`, `#<PR#>`, `<SHA>` with actual values.

- [ ] **Step 3: Apply update**

```bash
gh issue edit 2063 --body-file umbrella-2063-body.md 2>&1 | head -3
rm umbrella-2063-body.md
```

Expected: issue URL printed.

- [ ] **Step 4: Verify**

```bash
gh issue view 2063 --json body --jq .body | grep -A1 "Phase B mockup audit" | head -5
```

Expected: new row visible.

---

## Self-Review Checklist

(Run inline after writing — fixes applied where issues found.)

**1. Spec coverage:**
- Architecture sequential cluster-by-cluster → Tasks 5-10 ✓
- Component 1 discover-clusters → Task 3 ✓
- Component 2 audit-output-schema → Task 2 ✓
- Component 3 master orchestrator → Tasks 5-10 in this conversation ✓
- Component 4 generate-deliverables → Task 11 ✓
- Component 5 designer sign-off gate → Task 16 ✓ + create-tracking-issues Task 13 ✓
- Data flow → Tasks 4 (manifest) + 5-10 (sequential audits) + 12 (deliverables) + 17 (post-signoff) ✓
- Error handling per-agent → Tasks 5-10 Step 4 retry once + escalate ✓
- Error handling per-mockup pair disagreement → captured in cluster prompts + generate-deliverables ✓
- Error handling tracking issue rollback → Task 13 implementation ✓
- Testing → Tasks 2/3/11/13 each have TDD step ✓
- Acceptance criteria → Task 15 PR body lists ✓
- Out of scope → not addressed in plan tasks (correctly) ✓

**2. Placeholder scan:**
- `#TBD` for sub-issue, `#<PR#>` for PR, `<SHA>` for merge — all intentional executor fills
- No "TODO", "implement later", "appropriate error handling" found
- Code blocks present for every code step

**3. Type consistency:**
- `MockupClassification` defined in Task 2, used in Tasks 5-10 prompts and Task 11 test fixtures ✓
- `ClusterOutput` (array) defined in Task 2, used in Task 5-10 validation ✓
- `classifyFile` signature `(filename, onWarn?) => ClusterId` consistent in Task 3 implementation and tests ✓
- `generateDeliverables` opts shape `{ auditPath, mockupsDir, auditsDir, docsDir }` consistent across Task 11 test/impl ✓
- `parseDrafts` returns `Array<{mockup_path, title, body}>` consistent in Task 13 ✓

**4. Spec gaps:** none identified.

**5. Decision gates explicit:**
- Tasks 5-10 each have validation-fail decision gate (retry once → escalate)
- Task 12 lint:fidelity decision gate (inspect first failing + fix FIDELITY_TEMPLATE if schema mismatch)
- Task 16 designer sign-off poll
- Task 17 create-tracking-issues rollback on partial failure

All issues fixed inline. No re-review needed.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-10-ds-17-phase-b-mockup-audit-plan.md`. Two execution options:

**1. Subagent-Driven** — dispatch fresh subagent per task, review between tasks. Note: Tasks 5-10 (cluster audits) already use Agent dispatch internally; wrapping each Task in an outer subagent doubles delegation overhead.

**2. Inline Execution (recommended for this plan)** — execute tasks in this session using executing-plans, batch with checkpoints. The cluster audits in Tasks 5-10 are themselves Agent invocations; orchestration in the main conversation keeps the aggregation state coherent.

Proceeding with inline execution + code-reviewer agent review of plan first per user request pattern ("scrivi piano, review piano, implementa piano").
