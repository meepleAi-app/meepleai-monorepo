# DS-17 Phase C-1 Pilot Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate 46 mockup pilot (auth 12 + sp3 16 + sp6-7-nano 18) → 46 Storybook stories using argTypes matrix pattern (DEC-P3-3), via 3 sequential sub-issues with hybrid AI scaffold + human iteration lifecycle.

**Architecture:** 3 sub-issue sequenziali (auth → sp3 → sp6-7-nano). Each sub-issue has 4-phase lifecycle: (1) AI pre-flight batch dispatch generates scaffolds for ALL mockup in cluster, (2) human iterates 1 story at a time with TDD-style refine+commit, (3) cluster integration with snapshot spec + designer queue + fidelity refs, (4) PR + admin-squash merge.

**Tech Stack:** Next.js 16 + React 19 + Storybook 10.4.1 + @storybook/nextjs (Webpack), vitest 4.1 + Playwright snapshot tests, react-intl + TanStack Query + MSW, zod for fidelity.json validation. Phase 2.5 pilot stories (Library + GameDetail) are the reference implementation.

**Spec**: `docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md`

**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063). Predecessors: Phase A [#2120](https://github.com/meepleAi-app/meepleai-monorepo/issues/2120) MERGED PR #2124, Phase B [#2127](https://github.com/meepleAi-app/meepleai-monorepo/issues/2127) MERGED PR #2128 `66e924233`.

---

## File Structure (per sub-issue)

| Path | Action | Responsibility |
|------|--------|---------------|
| `apps/web/scripts/audit-mockups/scaffolds/<cluster>/<mockup-stem>/story.draft.tsx` | CREATE (Phase 1, temp) | Story scaffold with argTypes matrix + meta |
| `apps/web/scripts/audit-mockups/scaffolds/<cluster>/<mockup-stem>/fixture.draft.ts` | CREATE (Phase 1, temp) | Fixture data + MSW handlers stub |
| `apps/web/scripts/audit-mockups/scaffolds/<cluster>/<mockup-stem>/axis-discovery.md` | CREATE (Phase 1, temp) | Documented axis + frame matrix |
| `apps/web/scripts/audit-mockups/scaffolds/<cluster>/<mockup-stem>/msw-gap-analysis.md` | CREATE (Phase 1, temp) | Missing handlers + endpoint mapping |
| `apps/web/src/<route-path>/<component>.stories.tsx` | CREATE (Phase 2, final) | Story file (mirror Phase 2.5 pilot) |
| `apps/web/src/__tests__/fixtures/mockup-pilots/<cluster>/<mockup-stem>.ts` | CREATE (Phase 2, final) | Fixture data + MSW handlers |
| `apps/web/e2e/storybook/<cluster>.snapshot.spec.ts` | CREATE (Phase 3) | Per-cluster snapshot spec mirroring `library.snapshot.spec.ts` |
| `apps/web/e2e/storybook/<cluster>.snapshot.spec.ts-snapshots/*.png` | CREATE (Phase 3) | Baseline PNGs per Desktop viewport |
| `apps/web/scripts/audit-mockups/generate-cluster-review-queue.mjs` | CREATE (Task 4, once across all sub-issue) | New script — emits designer queue per cluster |
| `apps/web/scripts/audit-mockups/__tests__/generate-cluster-review-queue.test.ts` | CREATE (Task 4) | TDD tests for the queue generator |
| `docs/for-developers/frontend/c1-<cluster>-review-queue.md` | CREATE (Phase 3) | Designer review queue per cluster |
| `admin-mockups/design_files/<mockup-stem>.fidelity.json` | MODIFY (Phase 3) | Set `story_path` + `fixtures_path` fields (currently empty) |
| `apps/web/e2e/storybook/diagnostic.snapshot.spec.ts` | MODIFY (Phase 3, once per cluster) | Extend STORIES array with 1 cluster sample slug |
| `CLAUDE.md` | MODIFY (Phase 4) | Update mockup migration pattern paragraph with cluster row |

---

## Sub-issue 1: DS-17-9 auth (12 mockup → 12 stories)

### Task 1: Pre-flight — sub-issue + branch + budget anchor

**Files:** none (workspace setup)

- [ ] **Step 1: Verify clean main-dev + on correct branch**

```bash
git status --short
git branch --show-current
date -u +%Y-%m-%dT%H:%M:%SZ > /tmp/phase-c1-auth-start.txt
echo "Phase C-1 auth started at: $(cat /tmp/phase-c1-auth-start.txt)"
```

Expected: branch `feature/issue-2063-ds-17-phase-c-pilot-migration` (created at end of brainstorming, spec already committed `7e64d5c42`), working tree clean except scheduled_tasks.lock.

- [ ] **Step 2: Create DS-17-9 sub-issue**

```bash
gh issue create --title "[DS-17 Phase C-1] DS-17-9 auth cluster migration — 12 mockup to Storybook stories" --body "$(cat <<'EOF'
## Goal

Migrate 12 auth cluster mockup files to Storybook stories following argTypes matrix pattern (DEC-P3-3).

## Scope (12 mockup from Phase B audit)

- auth-flow.html / .jsx (8 auth routes covered)
- onboarding.html / .jsx
- notifications.html / .jsx
- public.html / .jsx
- settings.html / .jsx
- sp5-profile-settings.html / .jsx

HTML+JSX twins dedup: 6 unique stories.

## Pattern reference

- docs/for-developers/frontend/page-mock-story-pattern.md
- apps/web/src/app/(authenticated)/library/_content.stories.tsx (Phase 2.5 pilot)
- apps/web/src/app/(authenticated)/games/[id]/_components/GameDetailView.stories.tsx (Phase 2.5 pilot)

## Lifecycle (4-phase)

1. AI pre-flight scaffold dispatch (~30min)
2. Human iteration: 12 stories committed 1 at a time (~1.5gg)
3. Cluster integration: snapshot.spec + designer queue + fidelity refs + cleanup (~0.5gg)
4. PR + admin-squash merge + umbrella update

## Refs

- Spec: docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md
- Umbrella: #2063
- Phase B audit: #2127 PR #2128 66e924233
- Phase A: #2120 PR #2124 dba7898c1

🤖 Generated with Claude Code
EOF
)" 2>&1 | tail -3
```

Expected: GitHub issue URL — record `#NNNN` as auth sub-issue number. Save to `/tmp/phase-c1-auth-issue.txt`:

```bash
echo "<NNNN>" > /tmp/phase-c1-auth-issue.txt
```

- [ ] **Step 3: Verify Storybook + app pre-flight**

```bash
ls apps/web/.storybook/preview.tsx apps/web/.storybook/main.ts
ls admin-mockups/design_files/auth-flow.html admin-mockups/design_files/auth-flow.jsx
ls admin-mockups/design_files/auth-flow.fidelity.json
```

Expected: all files exist. Storybook config is post-Phase 4 prelude fix (preview.ts deleted, staticDirs enabled, nextjs.navigation parameter set).

---

### Task 2: Read Phase 2.5 pilot pattern + audit data

**Files:** none (research)

- [ ] **Step 1: Re-read pattern doc**

```bash
cat docs/for-developers/frontend/page-mock-story-pattern.md | head -120
```

Expected: understand axis discovery + Frame export pattern + fixture co-location.

- [ ] **Step 2: Inspect Phase 2.5 pilot story for reference**

```bash
cat apps/web/src/app/\(authenticated\)/library/_content.stories.tsx
```

Expected: ~150 lines, 9 Frame exports for Library, `mswForState` switcher, full argTypes matrix.

- [ ] **Step 3: Extract auth cluster from Phase B audit**

```bash
jq '.byCluster.auth' audits/2026-06-10-mockup-design-intent-audit.json > /tmp/auth-cluster.json
jq -r '.[].mockup_path' /tmp/auth-cluster.json
```

Expected: 12 paths listed (6 HTML + 6 JSX twins). Verify all `design_intent: current` (no obsolete in auth per Phase B).

```bash
jq -r '.[] | select(.design_intent != "current") | .mockup_path' /tmp/auth-cluster.json
```

Expected: empty (no obsolete in auth cluster).

---

### Task 3: Read fidelity validation contract

**Files:** none (research)

- [ ] **Step 1: Inspect fidelity.json schema**

```bash
head -100 apps/web/scripts/mockup-annotations/validate-fidelity.mjs | grep -E "story_path|fixtures_path|design_intent|viewports"
```

Expected: confirm `story_path` + `fixtures_path` fields exist as `z.string().default('')`. Phase 3 step will populate them.

- [ ] **Step 2: Verify current auth fidelity files**

```bash
jq '.acceptance | {story_path, fixtures_path, design_intent, viewports}' admin-mockups/design_files/auth-flow.fidelity.json
```

Expected: `story_path: ""`, `fixtures_path: ""`, `design_intent: "current"`, `viewports: ["desktop"]`.

---

### Task 4: TDD — create generate-cluster-review-queue.mjs

**Files:**
- Create: `apps/web/scripts/audit-mockups/__tests__/generate-cluster-review-queue.test.ts`
- Create: `apps/web/scripts/audit-mockups/generate-cluster-review-queue.mjs`

- [ ] **Step 1: Write failing tests**

Write `apps/web/scripts/audit-mockups/__tests__/generate-cluster-review-queue.test.ts`:

```ts
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, rmSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { generateClusterReviewQueue } from '../generate-cluster-review-queue.mjs';

describe('generateClusterReviewQueue', () => {
  let workDir: string;

  beforeEach(() => {
    workDir = mkdtempSync(join(tmpdir(), 'cluster-queue-'));
  });

  afterEach(() => {
    rmSync(workDir, { recursive: true, force: true });
  });

  it('emits queue with shipped stories table', () => {
    const audit = {
      clusterId: 'auth',
      classifications: [
        {
          mockup_path: 'admin-mockups/design_files/auth-flow.html',
          design_intent: 'current',
          confidence: 0.9,
          reasoning: 'auth-flow shipped',
          sub_components: ['LoginScreen'],
          pair_disagreement: false,
          suggested_tracking_issue: null,
        },
      ],
      stories: [
        {
          mockup_stem: 'auth-flow',
          story_path: 'apps/web/src/app/(public)/(auth)/auth-flow.stories.tsx',
          fixtures_path: 'apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts',
          frame_count: 6,
        },
      ],
    };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateClusterReviewQueue({
      auditPath,
      outDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/c1-auth-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/auth-flow/);
    expect(queue).toMatch(/6 frames/);
    expect(queue).toMatch(/story_path.*auth-flow\.stories\.tsx/);
  });

  it('flags obsolete mockup with DEFERRED note', () => {
    const audit = {
      clusterId: 'auth',
      classifications: [
        {
          mockup_path: 'admin-mockups/design_files/sp4-old.html',
          design_intent: 'forward-refactor-obsolete',
          confidence: 0.9,
          reasoning: 'Obsolete',
          sub_components: [],
          pair_disagreement: false,
          suggested_tracking_issue: { title: 'T', body: 'B' },
          obsolete_tracking_issue_ref: '#9999',
        },
      ],
      stories: [],
    };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateClusterReviewQueue({
      auditPath,
      outDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/c1-auth-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/DEFERRED post-Phase-B-tracking-#9999/);
    expect(queue).toMatch(/sp4-old\.html/);
  });

  it('throws on malformed input (missing clusterId, Code-reviewer Finding 6)', () => {
    // Validate input contract — converts silent failures into loud build failures.
    // Phase B audit shape is { byCluster: { auth: [...] } } — NOT { clusterId, classifications }.
    // A future executor running the generator on the Phase B JSON directly (skipping
    // Task 16 Step 1 jq construction) would silently emit c1-null-review-queue.md.
    const malformed = { byCluster: { auth: [] } };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(malformed));

    expect(() =>
      generateClusterReviewQueue({
        auditPath,
        outDir: join(workDir, 'docs-out'),
      })
    ).toThrow(/Invalid cluster audit JSON.*clusterId|classifications/);
  });

  it('calls out pair_disagreement explicitly', () => {
    const audit = {
      clusterId: 'auth',
      classifications: [
        {
          mockup_path: 'admin-mockups/design_files/foo.html',
          design_intent: 'current',
          confidence: 0.7,
          reasoning: 'HTML canonical, JSX twin differs',
          sub_components: [],
          pair_disagreement: true,
          suggested_tracking_issue: null,
        },
      ],
      stories: [
        {
          mockup_stem: 'foo',
          story_path: 'apps/web/foo.stories.tsx',
          fixtures_path: 'apps/web/fix.ts',
          frame_count: 1,
        },
      ],
    };
    const auditPath = join(workDir, 'audit.json');
    writeFileSync(auditPath, JSON.stringify(audit));

    generateClusterReviewQueue({
      auditPath,
      outDir: join(workDir, 'docs-out'),
    });

    const queue = readFileSync(join(workDir, 'docs-out/c1-auth-review-queue.md'), 'utf-8');
    expect(queue).toMatch(/Pair disagreements/);
    expect(queue).toMatch(/foo\.html/);
  });
});
```

- [ ] **Step 2: Run test to verify failure**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/generate-cluster-review-queue.test.ts 2>&1 | tail -10
```

Expected: FAIL with "Cannot find module".

- [ ] **Step 3: Implement generate-cluster-review-queue.mjs**

Write `apps/web/scripts/audit-mockups/generate-cluster-review-queue.mjs`:

```js
#!/usr/bin/env node
/**
 * generate-cluster-review-queue.mjs — emit designer review queue per Phase C cluster.
 *
 * Reads cluster-scoped audit JSON (subset of Phase B output annotated with
 * Phase C shipped stories) and emits markdown queue listing shipped stories,
 * obsolete deferred (post-Phase-B-tracking), and pair disagreements.
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md
 *   Umbrella: #2063
 *   Pattern: Phase B generate-deliverables.mjs (queue section)
 */

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');

/**
 * @param {{
 *   auditPath: string,
 *   outDir: string
 * }} opts
 */
export function generateClusterReviewQueue(opts) {
  const { auditPath, outDir } = opts;
  const data = JSON.parse(readFileSync(auditPath, 'utf-8'));

  // Code-reviewer Finding 6: validate input contract to convert silent
  // malformed-output failures into loud build failures.
  if (!data.clusterId || !Array.isArray(data.classifications)) {
    throw new Error(
      `Invalid cluster audit JSON at ${auditPath}: missing clusterId or classifications array. ` +
        `Expected shape: { clusterId, classifications[], stories[] }. Got keys: ${Object.keys(data).join(', ')}`
    );
  }
  const clusterId = data.clusterId;

  mkdirSync(outDir, { recursive: true });

  const shippedStories = data.stories || [];
  const obsolete = (data.classifications || []).filter(
    (c) => c.design_intent === 'forward-refactor-obsolete'
  );
  const pairDisagreements = (data.classifications || []).filter((c) => c.pair_disagreement);

  const queue = `# Designer Review Queue — DS-17 Phase C-1 cluster: ${clusterId}

**Generated**: ${new Date().toISOString().split('T')[0]}
**Source**: Phase C-1 cluster migration (post Phase B audit)
**Cluster**: ${clusterId}

## Shipped stories (require designer review)

${
  shippedStories.length
    ? shippedStories
        .map(
          (s) =>
            `- [ ] \`${s.mockup_stem}\` (${s.frame_count} frames)\n  - story_path: \`${s.story_path}\`\n  - fixtures_path: \`${s.fixtures_path}\``
        )
        .join('\n')
    : '_None._'
}

## Obsolete deferred (Phase B tracking)

${
  obsolete.length
    ? obsolete
        .map(
          (o) =>
            `- \`${o.mockup_path}\` — DEFERRED post-Phase-B-tracking-${o.obsolete_tracking_issue_ref || '#TBD'}: ${o.reasoning}`
        )
        .join('\n')
    : '_None._'
}

## Pair disagreements (require arbitration)

${
  pairDisagreements.length
    ? pairDisagreements
        .map((p) => `- \`${p.mockup_path}\` — ${p.reasoning}`)
        .join('\n')
    : '_None._'
}

## How to approve

Comment on PR with magic phrase:

\`\`\`
DESIGNER APPROVED: <ISO date> <your-name>
\`\`\`

(Same protocol as Phase B; sign-off optional per user decision.)
`;

  writeFileSync(join(outDir, `c1-${clusterId}-review-queue.md`), queue);
  console.log(`Designer queue written: c1-${clusterId}-review-queue.md`);
  console.log(`  - Shipped stories: ${shippedStories.length}`);
  console.log(`  - Obsolete deferred: ${obsolete.length}`);
  console.log(`  - Pair disagreements: ${pairDisagreements.length}`);
}

function main() {
  const argv = process.argv.slice(2);
  const auditIdx = argv.indexOf('--audit');
  const outIdx = argv.indexOf('--out');
  if (auditIdx === -1 || outIdx === -1) {
    console.error('Usage: generate-cluster-review-queue.mjs --audit <path> --out <dir>');
    process.exit(2);
  }
  generateClusterReviewQueue({
    auditPath: resolve(REPO_ROOT, argv[auditIdx + 1]),
    outDir: resolve(REPO_ROOT, argv[outIdx + 1]),
  });
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main();
}
```

- [ ] **Step 4: Run tests to verify pass**

```bash
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/generate-cluster-review-queue.test.ts 2>&1 | tail -5
```

Expected: 3 passed.

- [ ] **Step 5: Add npm script**

Use Edit on `apps/web/package.json`. Find existing `audit-mockups:create-issues` line and add immediately after:

```json
"audit-mockups:cluster-queue": "node scripts/audit-mockups/generate-cluster-review-queue.mjs",
```

- [ ] **Step 6: Commit**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add apps/web/scripts/audit-mockups/generate-cluster-review-queue.mjs apps/web/scripts/audit-mockups/__tests__/generate-cluster-review-queue.test.ts apps/web/package.json
git commit -m "feat(audit-mockups): #${ISSUE} cluster review queue generator (TDD)

DS-17 Phase C-1 Task 4: per-cluster designer queue script.

3 unit tests:
- shipped stories table with story_path + fixtures_path
- obsolete deferred with #N tracking ref
- pair disagreements explicit callout

Pattern mirrors Phase B generate-deliverables.mjs queue section.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 5: AI pre-flight scaffold dispatch for auth cluster

**Files:**
- Create: `apps/web/scripts/audit-mockups/scaffolds/auth/<6 dirs>/` (one per unique mockup stem)

- [ ] **Step 1: List auth cluster unique mockups (HTML canonical)**

```bash
jq -r '.[] | select(.mockup_path | endswith(".html")) | .mockup_path' /tmp/auth-cluster.json
```

Expected: 6 HTML paths (auth-flow, onboarding, notifications, public, settings, sp5-profile-settings).

- [ ] **Step 2: Dispatch Agent for scaffold generation**

Invoke `Agent(general-purpose)`:

- **description**: "Pre-flight scaffolds for auth cluster"
- **prompt**:
  ```
  Generate Storybook story scaffolds for the auth cluster (DS-17 Phase C-1, sub-issue #<from Task 1 Step 2>).

  Spec: docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md
  Pattern reference: apps/web/src/app/(authenticated)/library/_content.stories.tsx
  Phase 2.5 pilot fixture: apps/web/src/__tests__/fixtures/mockup-pilots/library.ts

  For each of these 6 unique mockup HTML files (HTML+JSX twins handled as pair, HTML canonical):
  1. admin-mockups/design_files/auth-flow.html (covers 8 routes: /login, /register, /reset-password, /oauth-callback, /verify-email, /verification-pending, /verification-success, /invitation-expired)
  2. admin-mockups/design_files/onboarding.html (/welcome, /onboarding, /setup, /setup-account)
  3. admin-mockups/design_files/notifications.html (/notifications, /notifications/preferences)
  4. admin-mockups/design_files/public.html (/)
  5. admin-mockups/design_files/settings.html (/settings + 7 sub-routes)
  6. admin-mockups/design_files/sp5-profile-settings.html (/profile?tab=settings + 6 section query-param)

  For each mockup:
  1. Read the HTML + JSX twin (cross-reference for axis)
  2. Identify the canonical Client component in apps/web/src/app/<route>/<component>.tsx or similar
  3. Discover axis from JSX twin: grep stateOverride, variant, initialTab, initialView, drawerOpen, bulk
  4. Identify Frame matrix: DesktopFrame label="NN · ..." + PhoneShell key={s.id}
  5. Check MSW gaps: list API endpoints that need handlers (cross-ref with existing apps/web/src/__tests__/mocks/handlers.ts)
  6. Emit 4 files in apps/web/scripts/audit-mockups/scaffolds/auth/<mockup-stem>/:
     - story.draft.tsx (story scaffold with argTypes matrix, meta, N Frame exports)
     - fixture.draft.ts (MOCK_AUTH_<NAME>_<STATE> constants + mswForState switcher)
     - axis-discovery.md (axis table + frame list + JSX evidence with line refs)
     - msw-gap-analysis.md (endpoint list + existing handler refs + gaps)

  IMPORTANT:
  - Story file should USE the real Client component (DO NOT re-implement)
  - Default args = first frame matrix values
  - Frame export naming: FrameNN_ShortName: Story with name mirroring mockup JSX label
  - Use 'use client'-aware components only (Storybook 10.4 client boundary)
  - For multi-route mockup (auth-flow → 8 routes, settings → 8), pick ONE canonical component to render with state variant (most general)
  - JSX twin classification differs from HTML in Phase B: NONE in auth (all classified 'current')

  Output ONLY the file dir structure with content. NO prose.

  Working directory: D:/Repositories/meepleai-monorepo-frontend
  ```

- [ ] **Step 3: Verify scaffolds emitted (per-directory check, Code-reviewer Finding 3)**

```bash
ls apps/web/scripts/audit-mockups/scaffolds/auth/
```

Expected: 6 subdirs (auth-flow, onboarding, notifications, public, settings, sp5-profile-settings).

Per-directory file integrity check:

```bash
for d in apps/web/scripts/audit-mockups/scaffolds/auth/*/; do
  count=$(find "$d" -maxdepth 1 -type f | wc -l)
  echo "$(basename $d): $count files (expected 4)"
  if [ "$count" -lt 4 ]; then
    echo "  ⚠ MISSING FILES — re-dispatch required for $(basename $d)"
    ls "$d"
  fi
done
```

Expected: each dir = 4 files (story.draft.tsx + fixture.draft.ts + axis-discovery.md + msw-gap-analysis.md). If any dir <4: re-dispatch ONLY for the missing dir with explicit instruction to fill the missing artifacts. Total file count `find ... -type f | wc -l` is NOT sufficient (could be 24 with uneven distribution); ONLY the per-dir check above gates progress.

- [ ] **Step 4: Commit scaffolds (pre-flight stage)**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add apps/web/scripts/audit-mockups/scaffolds/auth/
git commit -m "chore(stories): #${ISSUE} auth cluster scaffolds (AI pre-flight)

DS-17 Phase C-1 Task 5: AI pre-flight scaffold generation for auth cluster.
6 mockup × 4 files = 24 scaffold drafts (story + fixture + axis + msw-gap).

Scaffolds are TEMPORARY — deleted in Phase 3 step (Task 18). Git history
retains them via this commit for rollback if needed.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 6-11: Per-mockup human iteration (auth cluster, 6 stories)

For each of the 6 unique auth mockups, follow the template below. Each iteration creates ONE story commit.

#### Task 6 — auth-flow.html → auth-flow.stories.tsx

**Files:**
- Read: `apps/web/scripts/audit-mockups/scaffolds/auth/auth-flow/{story,fixture}.draft.{tsx,ts}` + `axis-discovery.md` + `msw-gap-analysis.md`
- Create: `apps/web/src/app/(public)/(auth)/auth-flow.stories.tsx` (final path TBD by axis-discovery component path)
- Create: `apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts`

- [ ] **Step 1: Read scaffold drafts + axis discovery**

```bash
cat apps/web/scripts/audit-mockups/scaffolds/auth/auth-flow/axis-discovery.md
cat apps/web/scripts/audit-mockups/scaffolds/auth/auth-flow/msw-gap-analysis.md
```

Cross-check axis discovery against actual JSX:

```bash
grep -n "stateOverride\|initialTab\|drawerOpen\|MOBILE_STATES" admin-mockups/design_files/auth-flow.jsx | head -10
```

If axis mismatch: open the JSX file + re-derive axis manually. Update axis-discovery.md inline.

- [ ] **Step 2: Verify Client component path exists**

```bash
ls apps/web/src/app/\(public\)/\(auth\)/login/page.tsx apps/web/src/app/\(public\)/\(auth\)/register/page.tsx 2>&1 | head -3
```

Expected: paths exist. Phase C-1 spec § 1 confirms `(public)/(auth)/<route>/page.tsx` is the canonical location.

- [ ] **Step 3: Refine story.draft.tsx → final stories.tsx**

Copy + refine:

```bash
mkdir -p apps/web/src/app/\(public\)/\(auth\)
cp apps/web/scripts/audit-mockups/scaffolds/auth/auth-flow/story.draft.tsx apps/web/src/app/\(public\)/\(auth\)/auth-flow.stories.tsx
```

Use Edit on the new file to:
- Verify `import { LoginForm } from '@/components/auth/LoginForm';` or similar (cross-ref with `apps/web/src/components/auth/` actual exports)
- Fix any `// TODO`, `// XXX` markers from scaffold
- **CRITICAL — Code-reviewer Finding 1 — title prefix convention**: derive title prefix from Phase 2.5 pilot reference:
  ```bash
  grep "title:" apps/web/src/app/\(authenticated\)/library/_content.stories.tsx
  ```
  Expected: `title: 'Pages/SP4/Library Mockup Matrix'` → slug prefix `pages-sp4-library-mockup-matrix`. Adopt the SAME `Pages/<SP>/<Cluster> <Mockup>` convention. For auth: `meta.title = 'Pages/Auth/Auth Flow'` → slug `pages-auth-auth-flow`. Do NOT add spaces around `/` separators (Phase 2.5 used single-slash). Verify the title BEFORE adding Frame exports — wrong prefix breaks every snapshot test in Task 13.
- Verify Frame exports match axis-discovery.md frame list
- Add JSDoc `@mockup admin-mockups/design_files/auth-flow.html` at top
- Verify argTypes mirror axis (states + screen variant)

- [ ] **Step 4: Refine fixture.draft.ts → final fixture.ts**

```bash
mkdir -p apps/web/src/__tests__/fixtures/mockup-pilots/auth
cp apps/web/scripts/audit-mockups/scaffolds/auth/auth-flow/fixture.draft.ts apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts
```

Use Edit to:
- Verify exports: `MOCK_AUTH_LOGIN_DEFAULT`, `MOCK_AUTH_REGISTER_DEFAULT`, etc.
- Verify `mswForState` switcher uses MSW http handlers from `msw` package (NOT `msw/node`)
- Cross-check endpoint paths with `apps/web/src/__tests__/mocks/handlers.ts`

- [ ] **Step 5: Run Storybook + verify story renders**

```bash
cd apps/web && pnpm storybook 2>&1 | tail -3 &
sleep 5
```

Open browser to `http://localhost:6006/?path=/story/pages-auth-auth-flow--frame-01-login`. Expected:
- No `[React Intl] Could not find required intl object` error
- No `Could not find required` error
- Story content renders (login form visible)

Kill Storybook dev server:

```bash
pkill -f "storybook dev" 2>&1 || true
```

- [ ] **Step 6: Capture snapshot baseline (single story)**

Run snapshot update for this story only (temporary spec — will create full cluster spec in Task 17):

```bash
cd apps/web && pnpm test:storybook:snapshots:update --grep "auth-flow" 2>&1 | tail -5
```

Expected: at least 1 PNG baseline written under `apps/web/e2e/storybook/auth-flow.snapshot.spec.ts-snapshots/` (if temporary spec exists; otherwise this step is deferred to Task 17).

Skip this step now — full cluster snapshot captured in Task 17.

- [ ] **Step 7: Commit story**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add apps/web/src/app/\(public\)/\(auth\)/auth-flow.stories.tsx apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts
git commit -m "feat(stories): #${ISSUE} auth-flow story + fixture

DS-17 Phase C-1 Task 6: auth-flow.html mockup migrated to Storybook story.
6 Frame exports (Login + Register + Reset + OAuth + VerifyEmail + Setup2FA).
Fixture exports MSW handlers for default + loading + error states.

Refs: Phase 2.5 pilot pattern docs/for-developers/frontend/page-mock-story-pattern.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

#### Task 7 — onboarding.html → onboarding.stories.tsx

Same template as Task 6, substituting:
- Mockup: `onboarding.html`
- Routes: `/welcome`, `/onboarding`, `/setup`, `/setup-account`
- Frames: 5-step wizard (Welcome / Games / Agents / Session / Complete)
- Component: search `apps/web/src/app/(authenticated)/onboarding/` for `OnboardingGenericWizard.tsx` or similar
- Fixture exports: `MOCK_AUTH_ONBOARDING_<STEP>_DEFAULT`
- Commit message: `feat(stories): #${ISSUE} onboarding story + fixture`

Replicate all 7 sub-steps from Task 6 with these substitutions.

#### Task 8 — notifications.html → notifications.stories.tsx

Same template, substituting:
- Mockup: `notifications.html`
- Routes: `/notifications`, `/notifications/preferences`
- Frames: 5 phone screens (filters bar, grouped feed, detail drawer, empty state, settings link)
- Component: `apps/web/src/app/(authenticated)/notifications/_components/NotificationsFeed.tsx` or similar
- Fixture exports: `MOCK_AUTH_NOTIFICATIONS_<STATE>`
- Commit message: `feat(stories): #${ISSUE} notifications story + fixture`

#### Task 9 — public.html → public.stories.tsx

Same template, substituting:
- Mockup: `public.html`
- Routes: `/` (landing)
- Frames: hero + features + stats + CTAs + how-it-works
- Component: `apps/web/src/app/(public)/page.tsx` (PublicLayout)
- Fixture exports: `MOCK_AUTH_PUBLIC_DEFAULT`
- Commit message: `feat(stories): #${ISSUE} public landing story + fixture`

#### Task 10 — settings.html → settings.stories.tsx

Same template, substituting:
- Mockup: `settings.html`
- Routes: `/settings` + 7 sub-routes
- Frames: 7 panel variants (Profile/Account/Preferences/Notifications/API Keys/Services/Notifications)
- Component: `apps/web/src/components/features/settings/` (verify with `ls`)
- Fixture exports: `MOCK_AUTH_SETTINGS_<PANEL>_DEFAULT`
- Commit message: `feat(stories): #${ISSUE} settings story + fixture`

#### Task 11 — sp5-profile-settings.html → sp5-profile-settings.stories.tsx

Same template, substituting:
- Mockup: `sp5-profile-settings.html`
- Routes: `/profile?tab=settings` + 6 section query-param variants
- Frames: 8 sub-components (ProfileTabBar + SettingsTab + 6 sections + 2FA wizard)
- Component: `apps/web/src/app/(authenticated)/profile/page.tsx` + `SettingsTab.tsx`
- Fixture exports: `MOCK_AUTH_SP5_PROFILE_<SECTION>`
- Commit message: `feat(stories): #${ISSUE} sp5-profile-settings story + fixture`

---

### Task 12: Snapshot bookkeeping checkpoint

**Files:** none (verification)

- [ ] **Step 1: Verify 6 stories committed**

```bash
git log --oneline main-dev..HEAD | grep "feat(stories)" | head -10
ls apps/web/src/app/\(public\)/\(auth\)/auth-flow.stories.tsx apps/web/src/app/\(authenticated\)/onboarding/onboarding.stories.tsx 2>&1 | head -2
```

Expected: 6 commits + at least 6 .stories.tsx files visible.

- [ ] **Step 2: Verify 6 fixtures committed**

```bash
ls apps/web/src/__tests__/fixtures/mockup-pilots/auth/ | wc -l
```

Expected: 6.

---

### Task 13: Create cluster snapshot spec

**Files:**
- Create: `apps/web/e2e/storybook/auth.snapshot.spec.ts`

- [ ] **Step 1: Write snapshot spec mirroring library pattern**

Write `apps/web/e2e/storybook/auth.snapshot.spec.ts`:

```ts
/**
 * @mockup DS-17 Phase C-1 cluster auth (#<from Task 1 Step 2>)
 *
 * Auth cluster snapshot suite — 6 mockup × N Frame exports.
 * Mirrors apps/web/e2e/storybook/library.snapshot.spec.ts pattern (Phase 2.5+4 prelude).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md
 */

import { test, expect } from '@playwright/test';

const FRAMES = [
  // auth-flow (6 frames)
  { slug: 'pages-auth-auth-flow--frame-01-login', file: 'auth-flow-01-login.png' },
  { slug: 'pages-auth-auth-flow--frame-02-register', file: 'auth-flow-02-register.png' },
  { slug: 'pages-auth-auth-flow--frame-03-recover-password', file: 'auth-flow-03-recover-password.png' },
  { slug: 'pages-auth-auth-flow--frame-04-reset-password', file: 'auth-flow-04-reset-password.png' },
  { slug: 'pages-auth-auth-flow--frame-05-verify-email', file: 'auth-flow-05-verify-email.png' },
  { slug: 'pages-auth-auth-flow--frame-06-setup-2fa', file: 'auth-flow-06-setup-2fa.png' },

  // onboarding (5 frames)
  { slug: 'pages-auth-onboarding--frame-01-welcome', file: 'onboarding-01-welcome.png' },
  { slug: 'pages-auth-onboarding--frame-02-games', file: 'onboarding-02-games.png' },
  { slug: 'pages-auth-onboarding--frame-03-agents', file: 'onboarding-03-agents.png' },
  { slug: 'pages-auth-onboarding--frame-04-session', file: 'onboarding-04-session.png' },
  { slug: 'pages-auth-onboarding--frame-05-complete', file: 'onboarding-05-complete.png' },

  // notifications (5 frames)
  { slug: 'pages-auth-notifications--frame-01-feed', file: 'notifications-01-feed.png' },
  { slug: 'pages-auth-notifications--frame-02-empty', file: 'notifications-02-empty.png' },
  { slug: 'pages-auth-notifications--frame-03-detail', file: 'notifications-03-detail.png' },
  { slug: 'pages-auth-notifications--frame-04-filters', file: 'notifications-04-filters.png' },
  { slug: 'pages-auth-notifications--frame-05-preferences', file: 'notifications-05-preferences.png' },

  // public (5 frames)
  { slug: 'pages-auth-public--frame-01-hero', file: 'public-01-hero.png' },
  { slug: 'pages-auth-public--frame-02-features', file: 'public-02-features.png' },
  { slug: 'pages-auth-public--frame-03-stats', file: 'public-03-stats.png' },
  { slug: 'pages-auth-public--frame-04-pricing', file: 'public-04-pricing.png' },
  { slug: 'pages-auth-public--frame-05-cta', file: 'public-05-cta.png' },

  // settings (7 frames)
  { slug: 'pages-auth-settings--frame-01-profile', file: 'settings-01-profile.png' },
  { slug: 'pages-auth-settings--frame-02-account', file: 'settings-02-account.png' },
  { slug: 'pages-auth-settings--frame-03-preferences', file: 'settings-03-preferences.png' },
  { slug: 'pages-auth-settings--frame-04-notifications', file: 'settings-04-notifications.png' },
  { slug: 'pages-auth-settings--frame-05-api-keys', file: 'settings-05-api-keys.png' },
  { slug: 'pages-auth-settings--frame-06-services', file: 'settings-06-services.png' },
  { slug: 'pages-auth-settings--frame-07-security', file: 'settings-07-security.png' },

  // sp5-profile-settings (8 frames)
  { slug: 'pages-auth-sp5-profile-settings--frame-01-profile-tab', file: 'sp5-profile-settings-01-profile-tab.png' },
  { slug: 'pages-auth-sp5-profile-settings--frame-02-settings-tab', file: 'sp5-profile-settings-02-settings-tab.png' },
  { slug: 'pages-auth-sp5-profile-settings--frame-03-security', file: 'sp5-profile-settings-03-security.png' },
  { slug: 'pages-auth-sp5-profile-settings--frame-04-2fa-setup', file: 'sp5-profile-settings-04-2fa-setup.png' },
  { slug: 'pages-auth-sp5-profile-settings--frame-05-2fa-codes', file: 'sp5-profile-settings-05-2fa-codes.png' },
  { slug: 'pages-auth-sp5-profile-settings--frame-06-api-keys', file: 'sp5-profile-settings-06-api-keys.png' },
  { slug: 'pages-auth-sp5-profile-settings--frame-07-preferences', file: 'sp5-profile-settings-07-preferences.png' },
  { slug: 'pages-auth-sp5-profile-settings--frame-08-services', file: 'sp5-profile-settings-08-services.png' },
];

for (const { slug, file } of FRAMES) {
  test(`Auth ${file.replace(/\.png$/, '')} matches snapshot`, async ({ page }) => {
    await page.goto(`/iframe.html?id=${slug}&viewMode=story`);
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2000);
    await expect(page).toHaveScreenshot(file, { fullPage: true });
  });
}
```

NOTE: actual frame slugs WILL DIFFER based on real story file output. Adjust FRAMES array after stories are committed (Tasks 6-11) by reading actual exports.

- [ ] **Step 1a (NEW — Code-reviewer Finding 1): Derive FRAMES array from actual committed story exports**

Hardcoded FRAMES below is a STARTING TEMPLATE. The actual story file paths and `meta.title` values may differ. BEFORE writing the spec, derive ground-truth from committed stories:

```bash
# Find all new story files (committed after Phase B merge 66e924233)
git diff --name-only main-dev..HEAD -- 'apps/web/src/**/*.stories.tsx' > /tmp/auth-stories.txt
cat /tmp/auth-stories.txt
# Expected: 6 story files

# For each story file, extract title + Frame exports
while read story_file; do
  echo "=== $story_file ==="
  grep -E "title:|^export const Frame" "$story_file" | head -20
done < /tmp/auth-stories.txt
```

Use the output to build the FRAMES array: each Frame export becomes one entry with slug derived from `title.toLowerCase().replace(/[\s/]/g, '-').replace(/-+/g, '-')` + `--` + Frame export name converted (camelCase → kebab-case).

Example derivation:
- title: `'Pages/Auth/Auth Flow'` → slug prefix: `pages-auth-auth-flow`
- export: `Frame01_Login` → slug suffix: `--frame-01-login`
- Combined slug: `pages-auth-auth-flow--frame-01-login`
- PNG filename: `auth-flow-01-login.png` (mockup-stem + frame number + label)

The FRAMES TEMPLATE below should be UPDATED inline to match. Do NOT write the spec until all 6 stories are committed (Tasks 6-11) and FRAMES is verified against actual exports.

- [ ] **Step 2: Verify slugs match story exports (broader grep, Code-reviewer Finding 1)**

```bash
# Broader grep covers all paths where new stories may have landed
git diff --name-only main-dev..HEAD -- 'apps/web/src/**/*.stories.tsx' | xargs grep -E "title:|^export const Frame" 2>&1 | head -80
```

Cross-check: for every Frame export found, the FRAMES array MUST contain a matching slug entry. If any export is missing from FRAMES, the snapshot test for that frame fails with "story not found" (navigation timeout, no baseline). Adjust FRAMES array inline.

- [ ] **Step 3: Commit cluster spec (without baselines yet)**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add apps/web/e2e/storybook/auth.snapshot.spec.ts
git commit -m "test(snapshot): #${ISSUE} auth cluster snapshot spec (FRAMES array)

DS-17 Phase C-1 Task 13: per-cluster snapshot spec mirroring Phase 2.5
library + game-detail pattern (post Phase 4 prelude waitForLoadState fix).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 14: Build Storybook + capture cluster baselines

**Files:**
- Create: `apps/web/e2e/storybook/auth.snapshot.spec.ts-snapshots/*.png` (~36 baselines)

- [ ] **Step 1: Kill stale Storybook processes**

```bash
netstat -ano | findstr :6007 2>&1 | head -3
# If LISTENING, kill the PID via taskkill //PID <pid> //F
pkill -f "http-server.*6007" 2>&1 || true
pkill -f "storybook build" 2>&1 || true
```

- [ ] **Step 2: Run snapshot update**

```bash
cd apps/web && pnpm test:storybook:snapshots:update 2>&1 | tail -10
```

Expected: ~36 stories captured (auth-flow 6 + onboarding 5 + notifications 5 + public 5 + settings 7 + sp5-profile-settings 8). Plus 4 existing diagnostic + 12 from Phase 2.5.

If story fails to render: inspect the test-results dir error-context.md, fix the story file (provider issue, missing fixture import, etc.), re-run.

- [ ] **Step 3: Verify baseline PNG count**

```bash
ls apps/web/e2e/storybook/auth.snapshot.spec.ts-snapshots/ | wc -l
```

Expected: ~36 PNGs.

- [ ] **Step 4: Smoke test the gate**

Modify ONE fixture to force snapshot diff, verify gate catches:

```bash
# Example: modify auth-flow.ts MOCK_AUTH_LOGIN_DEFAULT.email to "smoketest@example.com"
# Run snapshot test (NOT update)
cd apps/web && pnpm test:storybook:snapshots --grep "auth-flow-01" 2>&1 | tail -5
# Expected: FAIL with visible diff
# Revert the fixture change
```

If smoke test fails to detect: tolerance too loose, investigate Playwright snapshot config.

- [ ] **Step 5: Commit baselines**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add apps/web/e2e/storybook/auth.snapshot.spec.ts-snapshots/
git commit -m "test(snapshot): #${ISSUE} auth cluster baselines (~36 PNGs)

DS-17 Phase C-1 Task 14: capture initial cluster baselines for all
6 mockup × N frames. Smoke test gate verified.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 15: Update fidelity.json refs

**Files:**
- Modify: `admin-mockups/design_files/{auth-flow,onboarding,notifications,public,settings,sp5-profile-settings}.fidelity.json` (6 files)

- [ ] **Step 1: Update auth-flow.fidelity.json**

Use Edit on `admin-mockups/design_files/auth-flow.fidelity.json`. Find:

```json
    "story_path": "",
    "fixtures_path": "",
```

Replace with:

```json
    "story_path": "apps/web/src/app/(public)/(auth)/auth-flow.stories.tsx",
    "fixtures_path": "apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts",
```

- [ ] **Step 2: Repeat Step 1 for remaining 5 fidelity files**

Per file: substitute relative paths from Tasks 7-11 commits. Verify each updated file:

```bash
jq '.acceptance | {story_path, fixtures_path}' admin-mockups/design_files/auth-flow.fidelity.json
jq '.acceptance | {story_path, fixtures_path}' admin-mockups/design_files/onboarding.fidelity.json
jq '.acceptance | {story_path, fixtures_path}' admin-mockups/design_files/notifications.fidelity.json
jq '.acceptance | {story_path, fixtures_path}' admin-mockups/design_files/public.fidelity.json
jq '.acceptance | {story_path, fixtures_path}' admin-mockups/design_files/settings.fidelity.json
jq '.acceptance | {story_path, fixtures_path}' admin-mockups/design_files/sp5-profile-settings.fidelity.json
```

Expected: each shows populated paths.

- [ ] **Step 3: Run pnpm lint:fidelity**

```bash
cd apps/web && pnpm lint:fidelity 2>&1 | tail -3
```

Expected: PASS (153+ files all valid).

- [ ] **Step 4: Commit**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add admin-mockups/design_files/auth-flow.fidelity.json admin-mockups/design_files/onboarding.fidelity.json admin-mockups/design_files/notifications.fidelity.json admin-mockups/design_files/public.fidelity.json admin-mockups/design_files/settings.fidelity.json admin-mockups/design_files/sp5-profile-settings.fidelity.json
git commit -m "chore(fidelity): #${ISSUE} link auth cluster story + fixture paths

DS-17 Phase C-1 Task 15: populate 6 fidelity.json story_path + fixtures_path
fields (previously empty per Phase B template).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 16: Generate designer review queue

**Files:**
- Create: `audits/c1-auth-cluster.json` (input to generator)
- Create: `docs/for-developers/frontend/c1-auth-review-queue.md`

- [ ] **Step 0a (NEW — Code-reviewer Finding 4): Verify story paths exist BEFORE constructing cluster JSON**

```bash
# Derive actual committed story paths from git
git diff --name-only main-dev..HEAD -- 'apps/web/src/**/*.stories.tsx' > /tmp/auth-story-paths.txt
cat /tmp/auth-story-paths.txt
```

Expected: 6 paths listed. Use these EXACT paths to construct the `stories` array in Step 1 below. Do NOT hardcode guessed paths.

For each committed story, also verify its fixture pair exists:

```bash
while read story_path; do
  stem=$(basename "$story_path" .stories.tsx)
  fixture_path="apps/web/src/__tests__/fixtures/mockup-pilots/auth/${stem}.ts"
  [ -f "$fixture_path" ] || echo "MISSING fixture for $stem: expected $fixture_path"
done < /tmp/auth-story-paths.txt
```

Expected: no MISSING warnings. If any → return to Tasks 6-11.

- [ ] **Step 1: Build cluster annotated audit JSON**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
jq -n --argjson cluster "$(jq '.byCluster.auth' audits/2026-06-10-mockup-design-intent-audit.json)" '{
  clusterId: "auth",
  classifications: $cluster,
  stories: [
    { mockup_stem: "auth-flow", story_path: "apps/web/src/app/(public)/(auth)/auth-flow.stories.tsx", fixtures_path: "apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts", frame_count: 6 },
    { mockup_stem: "onboarding", story_path: "apps/web/src/app/(authenticated)/onboarding/onboarding.stories.tsx", fixtures_path: "apps/web/src/__tests__/fixtures/mockup-pilots/auth/onboarding.ts", frame_count: 5 },
    { mockup_stem: "notifications", story_path: "apps/web/src/app/(authenticated)/notifications/notifications.stories.tsx", fixtures_path: "apps/web/src/__tests__/fixtures/mockup-pilots/auth/notifications.ts", frame_count: 5 },
    { mockup_stem: "public", story_path: "apps/web/src/app/(public)/public.stories.tsx", fixtures_path: "apps/web/src/__tests__/fixtures/mockup-pilots/auth/public.ts", frame_count: 5 },
    { mockup_stem: "settings", story_path: "apps/web/src/components/features/settings/settings.stories.tsx", fixtures_path: "apps/web/src/__tests__/fixtures/mockup-pilots/auth/settings.ts", frame_count: 7 },
    { mockup_stem: "sp5-profile-settings", story_path: "apps/web/src/app/(authenticated)/profile/sp5-profile-settings.stories.tsx", fixtures_path: "apps/web/src/__tests__/fixtures/mockup-pilots/auth/sp5-profile-settings.ts", frame_count: 8 }
  ]
}' > audits/c1-auth-cluster.json
```

NOTE: actual story_path values may differ. Adjust based on Tasks 6-11 final paths.

- [ ] **Step 2: Run generator**

```bash
cd apps/web && pnpm audit-mockups:cluster-queue --audit ../../audits/c1-auth-cluster.json --out ../../docs/for-developers/frontend/ 2>&1 | tail -5
```

Expected output:
```
Designer queue written: c1-auth-review-queue.md
  - Shipped stories: 6
  - Obsolete deferred: 0
  - Pair disagreements: 0
```

- [ ] **Step 3: Verify queue**

```bash
cat docs/for-developers/frontend/c1-auth-review-queue.md
```

Expected: 6 stories listed in "Shipped stories" section.

- [ ] **Step 4: Commit**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add audits/c1-auth-cluster.json docs/for-developers/frontend/c1-auth-review-queue.md
git commit -m "docs(audit-mockups): #${ISSUE} auth cluster designer review queue

DS-17 Phase C-1 Task 16: emit designer review queue listing 6 shipped stories.
0 obsolete deferred (all auth mockup classified 'current' in Phase B).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 17: Extend diagnostic snapshot spec

**Files:**
- Modify: `apps/web/e2e/storybook/diagnostic.snapshot.spec.ts`

- [ ] **Step 1: Read current diagnostic spec**

```bash
cat apps/web/e2e/storybook/diagnostic.snapshot.spec.ts
```

Expected: 4-story array (Button + 3 pilots from Phase 4 prelude).

- [ ] **Step 2: Add 1 auth cluster sample slug**

Use Edit on `apps/web/e2e/storybook/diagnostic.snapshot.spec.ts`. Find:

```ts
const STORIES = [
  { name: 'Button (primitive control)', slug: 'ui-button--default' },
  { name: 'Library Frame09 (pilot)', slug: 'pages-sp4-library-mockup-matrix--frame-09-all-grid-rail' },
  { name: 'Library Frame13 (pilot empty)', slug: 'pages-sp4-library-mockup-matrix--frame-13-empty-first-run' },
  { name: 'GameDetail Frame07 (pilot)', slug: 'pages-sp4-gamedetail-mockup-matrix--frame-07-desktop-own-info' },
];
```

Replace with (add auth-flow sample):

```ts
const STORIES = [
  { name: 'Button (primitive control)', slug: 'ui-button--default' },
  { name: 'Library Frame09 (pilot)', slug: 'pages-sp4-library-mockup-matrix--frame-09-all-grid-rail' },
  { name: 'Library Frame13 (pilot empty)', slug: 'pages-sp4-library-mockup-matrix--frame-13-empty-first-run' },
  { name: 'GameDetail Frame07 (pilot)', slug: 'pages-sp4-gamedetail-mockup-matrix--frame-07-desktop-own-info' },
  { name: 'Auth-Flow Frame01 (DS-17 Phase C-1 auth)', slug: 'pages-auth-auth-flow--frame-01-login' },
];
```

- [ ] **Step 3: Run diagnostic**

```bash
cd apps/web && pnpm exec playwright test --config playwright.storybook.config.ts diagnostic.snapshot.spec.ts 2>&1 | grep -E "passed|failed"
```

Expected: 5 passed (or 4 pass + 1 fail if auth-flow story has provider issue — fix the story first).

- [ ] **Step 4: Commit**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add apps/web/e2e/storybook/diagnostic.snapshot.spec.ts
git commit -m "test(diagnostic): #${ISSUE} extend STORIES with auth cluster sample

DS-17 Phase C-1 Task 17: diagnostic regression guard now covers auth cluster.
Catches provider chain regressions (Intl + QueryClient + Next.js navigation)
for the new auth story family.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 18: Cleanup scaffolds

**Files:**
- Delete: `apps/web/scripts/audit-mockups/scaffolds/auth/` (entire dir)

- [ ] **Step 1: Verify all scaffolds consumed (BOTH story + fixture, Code-reviewer Finding 2)**

```bash
find apps/web/scripts/audit-mockups/scaffolds/auth -type f | wc -l
```

Expected: 24 files (6 × 4 from Task 5).

Cross-check that each mockup-stem dir has BOTH a committed `.stories.tsx` AND fixture. **CRITICAL**: if any story is missing, DO NOT delete scaffolds — the AI's axis discovery + MSW gap analysis work is permanently destroyed.

```bash
missing=0
for d in apps/web/scripts/audit-mockups/scaffolds/auth/*/; do
  stem=$(basename "$d")
  echo "=== $stem ==="
  story_count=$(find apps/web/src -name "${stem}.stories.tsx" -type f 2>&1 | wc -l)
  fixture_count=$(find apps/web/src/__tests__/fixtures/mockup-pilots/auth -name "${stem}.ts" -type f 2>&1 | wc -l)
  echo "  story: $story_count, fixture: $fixture_count (expected 1 each)"
  if [ "$story_count" -lt 1 ] || [ "$fixture_count" -lt 1 ]; then
    echo "  ⚠ MISSING — re-run Task 6-11 for stem '$stem' before cleanup"
    missing=1
  fi
done
if [ "$missing" -eq 1 ]; then
  echo "ABORT: at least one stem is missing story or fixture. Do NOT delete scaffolds."
  exit 1
fi
echo "✓ All 6 stems have both story + fixture committed. Safe to delete scaffolds."
```

Expected: ✓ message; no MISSING warnings. If ABORT triggered, return to Tasks 6-11 for the missing stems.

- [ ] **Step 2: Delete scaffolds dir**

```bash
rm -rf apps/web/scripts/audit-mockups/scaffolds/auth/
```

- [ ] **Step 3: Commit cleanup**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
git add -A apps/web/scripts/audit-mockups/scaffolds/
git commit -m "chore(stories): #${ISSUE} delete auth cluster scaffolds (post-consume)

DS-17 Phase C-1 Task 18: scaffolds dir deleted post Phase 2 iteration.
Git history retains via Task 5 commit for rollback.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 19: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Full verify suite**

```bash
cd apps/web && pnpm typecheck 2>&1 | tail -3
cd apps/web && pnpm lint 2>&1 | tail -5
cd apps/web && pnpm lint:fidelity 2>&1 | tail -3
cd apps/web && pnpm vitest run scripts/audit-mockups/__tests__/ scripts/mockup-annotations/__tests__/ 2>&1 | tail -5
cd apps/web && pnpm test:storybook:snapshots 2>&1 | tail -5
```

Expected: all PASS. snapshot tests should pass ~50/50 (4 diagnostic + 12 Phase 2.5 + ~36 auth cluster + 1 new diagnostic sample = ~53).

If any fails: inspect, fix inline, re-run.

- [ ] **Step 2: Push branch**

```bash
git push -u origin feature/issue-2063-ds-17-phase-c-pilot-migration 2>&1 | tail -3
```

Expected: branch pushed, no rejection.

---

### Task 20: PR + admin-squash merge + cleanup

**Files:** none (git ops)

- [ ] **Step 1: Create PR**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
gh pr create --base main-dev --head feature/issue-2063-ds-17-phase-c-pilot-migration \
  --title "feat(stories): #${ISSUE} DS-17 Phase C-1 auth cluster — 6 mockup → 6 stories" \
  --body "$(cat <<EOF
## Goal

DS-17 Phase C-1 sub-issue 1 of 3: migrate 6 auth cluster mockup to Storybook stories
following argTypes matrix pattern (DEC-P3-3) shipped Phase 2.5.

## What's in this PR

- 6 stories under \`apps/web/src/<route-path>/<component>.stories.tsx\`
- 6 fixtures under \`apps/web/src/__tests__/fixtures/mockup-pilots/auth/\`
- 1 cluster snapshot spec \`apps/web/e2e/storybook/auth.snapshot.spec.ts\`
- ~36 baseline PNGs captured (auth-flow 6 + onboarding 5 + notifications 5 + public 5 + settings 7 + sp5-profile-settings 8)
- 6 fidelity.json updates (story_path + fixtures_path populated)
- 1 designer queue \`docs/for-developers/frontend/c1-auth-review-queue.md\`
- 1 new TDD script \`generate-cluster-review-queue.mjs\` (3 tests)
- Diagnostic spec extended with auth cluster sample slug
- Scaffolds dir cleaned up post-consume

## Refs

- Sub-issue: closes #${ISSUE}
- Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
- Phase A: #2120 PR #2124 dba7898c1
- Phase B: #2127 PR #2128 66e924233
- Spec: docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md
- Plan: docs/superpowers/plans/2026-06-11-ds-17-phase-c-pilot-migration-plan.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 2: Capture PR number**

```bash
gh pr list --search "DS-17 Phase C-1 auth" --json number --jq '.[0].number' > /tmp/phase-c1-auth-pr.txt
cat /tmp/phase-c1-auth-pr.txt
```

- [ ] **Step 3: Admin-squash merge**

```bash
ISSUE=$(cat /tmp/phase-c1-auth-issue.txt)
PR=$(cat /tmp/phase-c1-auth-pr.txt)
gh pr merge $PR --squash --admin --delete-branch \
  --subject "feat(stories): #${ISSUE} DS-17 Phase C-1 auth cluster — 6 mockup → 6 stories (#${PR})" 2>&1 | tail -5
```

- [ ] **Step 4: Verify merge**

```bash
PR=$(cat /tmp/phase-c1-auth-pr.txt)
gh pr view $PR --json state,mergedAt,mergeCommit --jq '{state, mergedAt, sha: .mergeCommit.oid[0:9]}'
```

Expected: state=MERGED.

- [ ] **Step 5: Cleanup + sync main-dev**

```bash
git stash push -m "transient" -- .claude/scheduled_tasks.lock 2>&1 || true
git checkout main-dev
git pull --ff-only 2>&1 | tail -3
git branch -D feature/issue-2063-ds-17-phase-c-pilot-migration 2>&1 || true
git stash drop 0 2>&1 || true
git log --oneline -3
```

Expected: main-dev fast-forwarded to auth merge commit.

---

### Task 21: Umbrella body update + trigger next cluster

**Files:** GitHub issue #2063 body via `gh issue edit`

- [ ] **Step 1: Fetch umbrella body**

```bash
gh issue view 2063 --json body --jq .body > umbrella-2063.md
```

- [ ] **Step 2: Add Phase C-1 auth row**

Use Edit on `umbrella-2063.md`. Find:

```markdown
- [x] **DS-17 Phase B mockup audit** (#2127):
```

Add immediately AFTER (do not replace; insert after the Phase B paragraph):

```markdown

- [x] **DS-17 Phase C-1 auth cluster** (#${AUTH_ISSUE}): 6 mockup → 6 Storybook stories (auth-flow + onboarding + notifications + public + settings + sp5-profile-settings) → PR #${AUTH_PR} merged \`${AUTH_SHA}\` (admin-squash P145 35a volta). ~36 baseline PNGs captured. Designer queue published. Diagnostic spec extended. New TDD script `generate-cluster-review-queue.mjs`. Next: DS-17-10 sp3 cluster (16 mockup).
```

Substitute `${AUTH_ISSUE}`, `${AUTH_PR}`, `${AUTH_SHA}` with actual values from Tasks 1, 20.

- [ ] **Step 3: Apply**

```bash
gh issue edit 2063 --body-file umbrella-2063.md 2>&1 | head -3
rm -f umbrella-2063.md
```

- [ ] **Step 4: Verify**

```bash
gh issue view 2063 --json body --jq .body | grep -A1 "Phase C-1 auth cluster" | head -3
```

Expected: new row visible.

---

## Sub-issue 2: DS-17-10 sp3 (16 mockup → ~14 stories, 2 forward-refactor)

**Code-reviewer Finding 5 — CRITICAL note on Task 13 (snapshot spec)**: for sp3 the FRAMES array MUST be derived from actually-committed stories (per Task 13 Step 1a slug-derivation pattern). DO NOT pre-write the FRAMES array; build it inline AFTER all sp3 stories are committed (after Tasks 6-11 sp3 equivalents). Run:

```bash
git diff --name-only main-dev..HEAD -- 'apps/web/src/**/*.stories.tsx' | xargs grep -E "title:|^export const Frame" | head -80
```

Then construct FRAMES with one entry per Frame export. Estimated ~45-50 entries (8 unique stems × ~6 frames avg).

Apply Tasks 1-21 with these substitutions:
- Cluster: `sp3`
- Sub-issue title: `[DS-17 Phase C-1] DS-17-10 sp3 cluster migration — 14 mockup to Storybook stories (2 forward-refactor)`
- Mockup files (HTML canonical, 8 unique stems):
  - `sp3-shared-games.html` → /shared-games
  - `sp3-shared-game-detail.html` → /shared-games/[id]
  - `sp3-library-public.html` → forward-refactor (2 of 2 forward-refactor in cluster) — STILL ship story but flag in queue
  - `sp3-legal.html` → /legal + 3 sub-routes
  - `sp3-join.html` → /join + /sessions/join
  - `sp3-how-it-works.html` → /how-it-works
  - `sp3-faq-enhanced.html` → /faq + /games/[id]/faqs
  - `sp3-accept-invite.html` → /accept-invite + /invites/[token]
- 2 forward-refactor mockups (`sp3-library-public.{html,jsx}` per Phase B): designer queue lists them under "Forward-refactor flagged for designer arbitration" section
- Snapshot spec: `apps/web/e2e/storybook/sp3.snapshot.spec.ts`
- Designer queue: `docs/for-developers/frontend/c1-sp3-review-queue.md`
- Diagnostic STORIES extension: add 1 sp3 sample (e.g., `pages-sp3-shared-games--frame-01-grid`)
- Estimated baselines: ~45-50 PNGs

Branch + budget timestamp + sub-issue creation as Task 1. Then Tasks 2-21 with sp3 substitutions throughout.

---

## Sub-issue 3: DS-17-11 sp6-7-nano (18 mockup → 18 stories)

**Code-reviewer Finding 5 — same note as sub-issue 2**: FRAMES array MUST be derived from actual committed stories (Task 13 Step 1a slug-derivation). DO NOT pre-write. Estimated ~50-55 entries (10 unique stems × ~5 frames avg).

Apply Tasks 1-21 with these substitutions:
- Cluster: `sp6-7-nano`
- Sub-issue title: `[DS-17 Phase C-1] DS-17-11 sp6-7-nano cluster migration — 18 mockup to Storybook stories`
- Mockup files (10 unique stems):
  - sp7-game-night-{create,detail-rsvp,live,summary,transition}.{html,jsx} — 5 pairs (10 files)
  - sp7-game-night-join-public.jsx — 1 unique (JSX-only)
  - sp6-libro-game-{index,photo-upload,resume-state,quota-credits}.{html,jsx mixed} — 6+1 unique
- Snapshot spec: `apps/web/e2e/storybook/sp6-7-nano.snapshot.spec.ts`
- Designer queue: `docs/for-developers/frontend/c1-sp6-7-nano-review-queue.md`
- Diagnostic STORIES extension: add 1 sp6-7-nano sample (e.g., `pages-sp6-7-nano-sp7-game-night-create--frame-01-quando`)
- Estimated baselines: ~50-55 PNGs

Same Task 1-21 template.

---

## Phase C-1 closure (after all 3 sub-issues merged)

### Task 22: Phase C-1 closure verification

- [ ] **Step 1: Verify all 3 cluster sub-issues closed**

```bash
gh issue list --search "DS-17-9 auth OR DS-17-10 sp3 OR DS-17-11 sp6-7-nano" --state all --json number,title,state --jq '.'
```

Expected: 3 issues, all CLOSED.

- [ ] **Step 2: Verify cluster snapshot tests all pass**

```bash
cd apps/web && pnpm test:storybook:snapshots 2>&1 | tail -3
```

Expected: all PASS (~140+ stories).

- [ ] **Step 3: Verify Phase C-2 sub-issue exists or trigger creation**

Open follow-up sub-issue for Phase C-2 covering sp4-core (106) + sp4-sessions (50):

```bash
gh issue create --title "[DS-17 Phase C-2] sp4-core + sp4-sessions cluster migration — 156 mockup deferred from Phase C-1" --body "$(cat <<'EOF'
## Goal

Phase C-2 follow-up to Phase C-1 (3 pilot cluster shipped 2026-06-11). Migrate remaining 156 non-obsolete mockups to Storybook stories.

## Scope

- sp4-core (106 mockup, 102 current + 4 forward-refactor)
- sp4-sessions (50 mockup, all current after 2 obsoleti skip)

Apply Phase C-1 lifecycle pattern (4-phase per cluster: AI pre-flight → human iteration → cluster integration → admin-squash merge).

## Phase C-1 validation (prerequisite)

- [x] DS-17-9 auth merged
- [x] DS-17-10 sp3 merged
- [x] DS-17-11 sp6-7-nano merged
- [x] Pattern validated across 3 clusters

## Refs

- Phase C-1 plan: docs/superpowers/plans/2026-06-11-ds-17-phase-c-pilot-migration-plan.md
- Phase C-1 spec: docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md
- Umbrella: #2063

🤖 Generated with Claude Code
EOF
)" 2>&1 | tail -3
```

- [ ] **Step 4: Update umbrella with Phase C-1 closure**

Fetch + update umbrella body to add Phase C-1 closure row after the 3 cluster rows:

```bash
gh issue view 2063 --json body --jq .body > umbrella-2063.md
```

Use Edit on `umbrella-2063.md` to add after the sp6-7-nano cluster row:

```markdown

- [x] **DS-17 Phase C-1 closure** (2026-06-XX): 3 sub-issue merged sequentially (DS-17-9 auth + DS-17-10 sp3 + DS-17-11 sp6-7-nano). 46 mockup → ~38 stories shipped (pair dedup). Pattern validated for hybrid AI scaffold + human verify lifecycle. Phase C-2 sub-issue #${C2_ISSUE} opened for sp4-core (106) + sp4-sessions (50). CI snapshot gate still `continue-on-error: true` — flip deferred to Phase C-2 completion.
```

Apply:

```bash
gh issue edit 2063 --body-file umbrella-2063.md 2>&1 | head -3
rm -f umbrella-2063.md
```

---

## Self-review checklist

(Ran inline after writing — fixes applied where issues found.)

**1. Spec coverage:**
- Architecture (3 sub-issue sequential) → Tasks 1-21 (auth), then template for sp3 + sp6-7-nano ✓
- 4-phase lifecycle → Task 1 (sub-issue), Task 5 (AI pre-flight), Tasks 6-11 (human iteration), Tasks 13-18 (cluster integration), Tasks 19-20 (PR + merge), Task 21 (umbrella) ✓
- Component 1 (scaffold generator) → Task 5 AI dispatch ✓
- Component 2 (story file pattern) → Tasks 6-11 refine ✓
- Component 3 (fixture file pattern) → Tasks 6-11 refine ✓
- Component 4 (snapshot spec pattern) → Task 13 + 14 ✓
- Component 5 (designer queue generator) → Task 4 TDD + Task 16 invoke ✓
- Component 6 (human iteration checklist) → Tasks 6-11 step-by-step ✓
- Data flow (3 phases) → matches Tasks 5 + 6-11 + 13-18 ✓
- Error handling (per-agent, per-mockup, per-cluster, per-PR) → Tasks 5/6/15/17 each have decision gates ✓
- Testing (unit, integration, smoke, regression) → Task 4 (unit TDD), Task 14 (integration snapshot), Task 6 Step 5 (smoke), Task 17 (regression) ✓
- Acceptance criteria → Task 19 verification + Task 22 Phase C-1 closure ✓
- Out of scope (Phase C-2, CI flip) → Task 22 creates Phase C-2; CI flip explicitly NOT in plan ✓

**2. Placeholder scan:**
- `#${ISSUE}` / `#${AUTH_ISSUE}` / `${AUTH_PR}` / `${AUTH_SHA}` — intentional executor fills from Task 1 + 20
- `<NNNN>` — sub-issue number placeholder (executor fills)
- `<mockup-stem>` / `<cluster>` / `<route-path>` — template syntax, intentional
- No "TODO" or "TBD" in narrative sections

**3. Type consistency:**
- `MockupClassification` schema referenced consistently from Phase B audit JSON ✓
- `generateClusterReviewQueue` signature: `(opts: { auditPath: string, outDir: string }) => void` — consistent in Task 4 test + impl ✓
- Frame naming convention `FrameNN_ShortName: Story` consistent in Tasks 6-11 + snapshot slug generation Task 13 ✓
- Fixture export naming `MOCK_<CLUSTER>_<NAME>_<STATE>` consistent ✓
- File path conventions (story.draft.tsx, fixture.draft.ts, axis-discovery.md, msw-gap-analysis.md) consistent ✓

**4. Decision gates explicit:**
- Task 5 Step 3: if <6 scaffolds, re-dispatch
- Task 6 Step 5: if story doesn't render, fix before snapshot
- Task 14 Step 4: smoke test gate verification
- Task 15 Step 3: lint:fidelity decision gate
- Task 19: full verify suite decision gate
- Task 22: Phase C-2 trigger gate

All issues fixed inline.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-11-ds-17-phase-c-pilot-migration-plan.md`. Two execution options:

**1. Subagent-Driven** — dispatch a fresh subagent per task. Note: Tasks 5, 6-11 already use Agent dispatch internally; wrapping each Task in an outer subagent would double delegation overhead.

**2. Inline Execution (recommended for this plan)** — execute tasks in this session using executing-plans, batch with checkpoints. The cluster scaffold dispatch (Task 5) is itself an Agent invocation; orchestration in main conversation keeps state coherent across the 21-task auth lifecycle.

Proceeding with inline execution + code-reviewer agent review of plan first per user request pattern ("scrivi piano, review piano, implementa piano").
