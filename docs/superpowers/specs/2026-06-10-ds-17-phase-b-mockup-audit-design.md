# DS-17 Phase B — Mockup Audit Design

**Date**: 2026-06-10
**Umbrella**: [#2063 DS-17 Mockup-to-App Fidelity](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
**Previous phase**: [Phase 4 prelude #2120](https://github.com/meepleAi-app/meepleai-monorepo/issues/2120) MERGED PR #2124 `dba7898c1` (2026-06-10)
**Sub-issue**: TBD (created during plan Task 1)

---

## Goal

Classify all 224 mockup files in `admin-mockups/design_files/` (105 HTML + 119 JSX, ~137 unique entries in `MOCKUPS_INDEX.md`) with explicit `design_intent` ∈ `{current, forward-refactor, forward-refactor-obsolete}`, generate a `*.fidelity.json` stub for each, publish a designer review queue, and open tracking issues per obsolete classification. This is **Phase B** of the 5-phase roadmap (A=Phase 4 prelude ✅, B=Audit, C=Migration sweep, D=Drift detection, E=optional Storybook source-of-truth).

Phase B unblocks Phase C (migration sweep) by giving each sub-issue a numeric scope: "DS-17 SP4 core: 13 mockup `current` + 2 `forward-refactor-obsolete` deferred" instead of "discover scope at migration time".

## Context

After Phase 2.5 ship (2026-06-10) and Phase 4 prelude ship (2026-06-10), the umbrella infrastructure is operational:

- `validate-fidelity.mjs` schema enforced via zod (DS-17-4 #2072)
- `mockup-annotations:audit` 100% mappable coverage on 68 routes (DS-17-1 #2069/#2084)
- 12 baseline PNGs captured for the 12 Phase 2.5 pilot stories (Library 9 + GameDetail 3) post-Phase 4 prelude (#2120)
- 3 example fidelity.json files in `docs/for-developers/frontend/templates/examples/`:
  - `sp4-library-desktop.fidelity.json` (`design_intent: "current"`)
  - `sp4-game-detail.fidelity.json` (`design_intent: "current"`)
  - `sp4-dashboard.fidelity.json` (`design_intent: "forward-refactor-obsolete"`, tracking issue #2114)

**Zero fidelity.json files committed in `admin-mockups/design_files/`** — the schema and template exist but no actual mockup has been classified yet. Phase B addresses this gap at scale.

## User decisions

Locked via brainstorming session 2026-06-10:

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Audit scope: all 224 files (HTML + JSX) | Maximum coverage. Each twin classified independently for completeness; pair disagreement surfaces drift. |
| 2 | Method: AI-assisted (subagent fan-out) | Faster than manual (~3gg → ~1gg), more consistent than ad-hoc. |
| 3 | Designer: review queue pre-merge | Safer than autonomous. Tracking issues NOT created until designer signs off. |
| 4 | Delivery: 1 PR big bang + 1 tracking issue per obsolete | Atomic merge. Tracking issues granular (Dashboard #2114 precedent). |
| 5 | Architecture: Opt C sequential cluster-by-cluster | Cross-mockup consistency over speed. 6 sequential agents, each sees previous aggregates. |

## Architecture

### Sequential cluster-by-cluster fan-out

Six pre-defined clusters ordered for cross-mockup consistency (simplest first to baseline confidence; largest cross-reference overlap in the middle; most independent last):

```
1. dev-fixtures      (~14 files)  — design system playground, datasets
2. auth + onboarding (~25 files)  — login, register, oauth, reset, verify, onboarding, notifications, settings
3. sp3-hub-drawer    (~30 files)  — public, join, library, KB views
4. sp4-core          (~70 files)  — dashboard, players, sessions, game-night, library, game-detail
5. sp4-sessions      (~40 files)  — live, toolkit, scores, recap, gamebook
6. sp6-7-nano        (~45 files)  — admin, RAG, observability, generators
```

**Per-cluster execution**:

1. Master orchestrator (in this conversation) builds the prompt for cluster N, including:
   - clusterId + cluster file list (absolute paths)
   - Aggregated structured output of agents 1..N-1 (cross-cluster consistency input)
   - Output JSON schema (zod-validated)
2. Dispatch single `Agent(subagent_type=general-purpose)` synchronously
3. Validate output JSON against zod schema; retry once on failure; escalate on persistent failure
4. Append to `aggregatedResults` Map

All six clusters run sequentially in a **single foreground session** (Workflow tool not used per user opt-in policy).

### Token budget per agent

Each Agent prompt: ~5k tokens cluster file list + ~10k tokens previous aggregates + ~3k tokens schema + boilerplate. Output: ~3k tokens JSON. Total per agent: ~25k tokens. 6 agents = ~150k tokens (well within budget).

## Components

### 1. Cluster Manifest Generator

**File**: `apps/web/scripts/audit-mockups/discover-clusters.mjs` (~50 LOC)

**Responsibility**: Discover 224 files in `admin-mockups/design_files/`, classify per cluster via filename prefix + `MOCKUPS_INDEX.md` cross-ref, emit `manifest.json`.

**Schema** (`manifest.json`):

```ts
type ClusterManifest = {
  generatedAt: string; // ISO date
  totalFiles: number;
  clusters: Array<{
    clusterId: 'dev-fixtures' | 'auth' | 'sp3' | 'sp4-core' | 'sp4-sessions' | 'sp6-7-nano';
    files: Array<{ path: string; type: 'html' | 'jsx'; pairKey?: string }>;
    dependencies: string[]; // depend on previous clusterIds for context
  }>;
};
```

**Cluster mapping rules**:
- `00-hub.html`, `01-screens.html`, `02-desktop-patterns.html`, `03-drawer-variants.html`, `04-design-system.html`, `05-dark-mode.html`, `state-matrix.html`, `components.css`, `data.js`, `mobile-app.jsx`, `tokens.css`, `sp4-play-records-data.js` → `dev-fixtures`
- Filename prefix `auth-`, `onboarding`, `notifications`, `public`, `settings`, `verify`, `reset` → `auth`
- Filename prefix `sp3-`, `hub-`, `join-`, `library-` (without `sp4-` prefix) → `sp3`
- Filename prefix `sp4-` AND name contains `dashboard|player|session|game-night|library|game-detail` (not `live|toolkit|scores|recap|gamebook`) → `sp4-core`
- Filename prefix `sp4-` AND name contains `live|toolkit|scores|recap|gamebook` → `sp4-sessions`
- Filename prefix `sp6-`, `sp7-`, `admin-`, `nano-`, `rag-`, `observability`, `generator` → `sp6-7-nano`
- Fallback: last-resort cluster `sp6-7-nano` with warning logged to manifest

**HTML+JSX pair detection**: same basename (e.g., `sp3-join.html` ↔ `sp3-join.jsx`) → `pairKey: "sp3-join"`. Both files go to same cluster.

### 2. Cluster Auditor Agent (general-purpose subagent)

**Prompt template** (built by master orchestrator):

```
You are auditing N mockup files in cluster <clusterId> for the MeepleAI design system Phase B audit.

For each file path provided, read the file, identify markers, and classify design_intent.

Previous clusters' aggregated outputs (cross-reference):
<json of aggregatedResults[1..N-1]>

Files to audit (cluster <clusterId>):
<list of absolute paths>

For each file emit ONE JSON object matching this schema:
{
  "mockup_path": string,          // relative path from repo root
  "design_intent": "current" | "forward-refactor" | "forward-refactor-obsolete",
  "confidence": number,           // 0.0-1.0
  "reasoning": string,            // 1-3 sentences citing markers found OR component path cross-ref
  "sub_components": string[],     // referenced components (es. ["LibraryHub", "GameCard"])
  "pair_disagreement": boolean,   // true if HTML+JSX twin classification differs
  "suggested_tracking_issue": null | {
    "title": string,
    "body": string
  }
}

Detection rules:
- "forward-refactor-obsolete" requires: explicit marker (REFACTOR-FORWARD, design-forward, Pre-Stage) AND codebase has refactored past mockup design. Suggested_tracking_issue MUST be populated.
- "forward-refactor" requires: explicit marker but codebase has NOT yet refactored. suggested_tracking_issue stays null (no action needed, just a flag).
- "current" is the default if no markers + codebase route matches mockup design.
- Pair disagreement: if both HTML and JSX exist in same cluster, classify both independently; if intents differ, set pair_disagreement=true on both.

Output: a single JSON array of objects, one per file. No prose, no comments, no markdown wrapping — just a JSON array.
```

**Output schema** (per file):

```ts
type MockupClassification = {
  mockup_path: string;
  design_intent: 'current' | 'forward-refactor' | 'forward-refactor-obsolete';
  confidence: number; // 0.0-1.0
  reasoning: string;
  sub_components: string[];
  pair_disagreement: boolean;
  suggested_tracking_issue: null | { title: string; body: string };
};

type ClusterOutput = MockupClassification[];
```

### 3. Master Orchestrator (in main conversation)

**Responsibility**: dispatch agents sequentially, validate output, aggregate, generate deliverables.

**Pseudo-flow** (no script — runs as conversation tool calls):

```
manifest = read(manifest.json)
aggregatedResults = new Map()

for cluster of manifest.clusters:
  prompt = buildPrompt(cluster, aggregatedResults)
  output = Agent(general-purpose, prompt)
  parsed = parseJSON(output)
  validated = zod.parse(ClusterOutputSchema, parsed)
  if validation_fail:
    retry once with stricter prompt
    if still fail: escalate to user
  aggregatedResults.set(cluster.clusterId, validated)

writeJson('audits/YYYY-MM-DD-mockup-design-intent-audit.json', aggregatedResults)
```

### 4. Deliverable Generator

**File**: `apps/web/scripts/audit-mockups/generate-deliverables.mjs` (~150 LOC)

**Reads**: `audits/YYYY-MM-DD-mockup-design-intent-audit.json`

**Emits**:

1. **224 `<mockup>.fidelity.json`** files in `admin-mockups/design_files/`:
   ```json
   {
     "_comment": "Generated by Phase B audit YYYY-MM-DD. See audits/... for source.",
     "mockup": {
       "source": "admin-mockups/design_files/<filename>",
       "states": ["default"]
     },
     "acceptance": {
       "visual_diff_max_px": 5,
       "color_delta_e_max": 3,
       "tokens_used": "canonical_only",
       "legacy_token_names_forbidden": true,
       "states_covered": ["default"],
       "a11y_axe": "AA",
       "a11y_violations_max": 0,
       "responsive_breakpoints": [375, 768, 1024, 1440],
       "designer_approved_by": "",
       "designer_approved_on": "",
       "story_path": "",
       "fixtures_path": "",
       "design_intent": "<classified>",
       "viewports": ["desktop"],
       "obsolete_tracking_issue": ""
     }
   }
   ```

2. **`audits/YYYY-MM-DD-mockup-design-intent-audit.md`** — summary table:
   ```markdown
   # Mockup Design Intent Audit — YYYY-MM-DD

   ## Summary

   | Cluster | Total | current | forward-refactor | forward-refactor-obsolete |
   |---------|-------|---------|------------------|---------------------------|
   | dev-fixtures | 14 | 14 | 0 | 0 |
   | auth | 25 | 22 | 1 | 2 |
   | ... | ... | ... | ... | ... |

   ## Pair disagreements

   | HTML | JSX | HTML intent | JSX intent |
   |------|-----|-------------|------------|
   | sp4-foo.html | sp4-foo.jsx | current | forward-refactor |

   ## Low confidence (< 0.6)

   | File | Intent | Reason |
   |------|--------|--------|
   | ... | ... | ... |
   ```

3. **`docs/for-developers/frontend/mockup-designer-review-queue.md`** — checklist:
   ```markdown
   # Mockup Designer Review Queue — DS-17 Phase B

   **Source**: `audits/YYYY-MM-DD-mockup-design-intent-audit.md`
   **Generated**: YYYY-MM-DD
   **Auditor**: AI subagent fan-out (Phase B #TBD)

   ## How to approve

   Comment on PR #TBD with:
   ```
   DESIGNER APPROVED: YYYY-MM-DD <your-name>
   ```

   Optionally request modifications inline. After approval, tracking issues will be
   created for `forward-refactor-obsolete` entries.

   ## Obsolete candidates (require review)

   - [ ] `sp4-dashboard.html` — REFACTOR-FORWARD: Asse C #1898 supersedes. Suggested tracking: existing #2114.
   - [ ] `sp4-other.html` — design-forward: codebase shipped X already. Suggested tracking: new issue.
   - ...

   ## Pair disagreements (require designer arbitration)

   - [ ] `sp4-foo.{html,jsx}` — HTML=current, JSX=forward-refactor. Pick canonical.

   ## Low confidence (< 0.6, optional review)

   - [ ] ...
   ```

4. **`audits/tracking-issues-drafts.md`** — one section per obsolete:
   ```markdown
   # Tracking Issues Drafts — DS-17 Phase B

   NOT created until designer sign-off. After approval, `create-tracking-issues.mjs`
   reads this file and creates GitHub issues.

   ## Draft 1: <mockup file>

   **Title**: `[DS-17 mockup obsolete] <filename> — design_intent forward-refactor-obsolete vs <route>`
   **Body**:
   <markdown body>

   ## Draft 2: ...
   ```

### 5. Designer Sign-off Gate

**Mechanism**: GitHub PR comment with magic phrase matching regex:

```
^DESIGNER APPROVED: \d{4}-\d{2}-\d{2} [\w\s-]+$
```

Example valid comments:
- `DESIGNER APPROVED: 2026-06-15 alice`
- `DESIGNER APPROVED: 2026-06-15 alice-doe`
- `DESIGNER APPROVED: 2026-06-15 Bob Smith`

Bot (post-merge automation, NOT shipped in this PR) verifies phrase regex. For Phase B initial ship, master conversation manually verifies phrase via `gh pr view --json comments --jq '.comments[].body'` + grep against the regex, after user signals designer has approved.

**Post sign-off**:
1. `apps/web/scripts/audit-mockups/create-tracking-issues.mjs` reads `audits/tracking-issues-drafts.md`
2. For each draft, runs `gh issue create --title "..." --body "..."` and captures the issue number
3. Updates the corresponding `fidelity.json` file: `obsolete_tracking_issue: "#<number>"`
4. Commits + amends PR with final state
5. Master orchestrator triggers admin-squash merge

## Data flow

(See architecture diagram in section above; expanded:)

```
[discover-clusters.mjs]      ← scripts/audit-mockups/
        │
        ▼
[manifest.json]              ← cluster split deterministic
        │
        ▼
┌─── Master Orchestrator ─────────────────────────┐
│  for clusterId in [dev-fixtures, auth, sp3,     │
│                     sp4-core, sp4-sessions,     │
│                     sp6-7-nano]:                │
│    prompt = template(cluster, prevAggregates)   │
│    output = Agent(general-purpose, prompt)      │
│    validated = zod.parse(output)                │
│    aggregatedResults[clusterId] = validated     │
└──────────────────────────────────────────────────┘
        │
        ▼
[audits/YYYY-MM-DD-mockup-design-intent-audit.json]
        │
        ▼
[generate-deliverables.mjs]
    ├─→ 224 fidelity.json
    ├─→ audits/...audit.md
    ├─→ docs/.../mockup-designer-review-queue.md
    └─→ audits/tracking-issues-drafts.md
        │
        ▼
[PR created → designer comment with magic phrase → manual verify]
        │
        ▼
[create-tracking-issues.mjs]
    ├─→ N github issues created
    ├─→ fidelity.json `obsolete_tracking_issue` updated
    └─→ commit + amend PR
        │
        ▼
[admin-squash merge → main-dev]
```

## Error handling

### Per-agent

| Failure mode | Behavior |
|---|---|
| Agent timeout (>10min) | Mark cluster failed, escalate to user, no auto-retry |
| Invalid JSON output | Retry once with stricter prompt ("emit ONLY JSON matching schema") |
| Schema mismatch (zod) | Show diff, retry once, escalate if persists |
| Partial cluster output (<N files) | Retry once, no "good enough" tolerance |

### Per-mockup

| Edge case | Behavior |
|---|---|
| HTML+JSX pair disagreement | Flag in queue, "PAIR DISAGREEMENT" callout, default to HTML (canonical per MOCKUPS_INDEX.md) |
| Low confidence (<0.6) | Mark "MANUAL REVIEW REQUIRED" in queue, no auto-tracking-issue creation |
| No clear markers + no codebase route mapped | Default to `current` with low confidence, flag for designer |
| Multiple obsoletes referencing same tracking issue | Dedup via title hash, single GitHub issue covers all |

### File system

| Failure mode | Behavior |
|---|---|
| Read failure on mockup file | Fail fast, audit must be complete |
| Write failure on fidelity.json | Warn + skip, continue other writes, log to error report |
| MOCKUPS_INDEX.md missing row for discovered file | Warn, still classify, mark `index_row_missing: true` |

### Designer sign-off gate

| Failure mode | Behavior |
|---|---|
| Magic phrase not detected after 5gg | Reminder comment, escalate to user |
| Designer requests modifications | Re-run specific cluster Agent, regenerate deliverables, update PR |
| Designer rejects entire audit | Kill branch, file post-mortem issue |

### Tracking issue creation

| Failure mode | Behavior |
|---|---|
| GitHub API rate limit | Backoff + retry, max 3 attempts per issue |
| Partial failure (3 of 10 created) | Rollback (close created issues), retry from scratch — no half-state |

## Testing strategy

### Unit tests (TDD, vitest)

**1. `discover-clusters.mjs`** (`apps/web/scripts/audit-mockups/__tests__/discover-clusters.test.ts`):
- Given known fixture set → discover() returns manifest with 6 clusters correctly partitioned
- Given new file matching `auth-` prefix → classified in `auth`
- Given ambiguous filename → falls to `sp6-7-nano` last-resort with warning
- Given HTML+JSX pair → same `pairKey`, both in same cluster

**2. zod schema validator** (`apps/web/scripts/audit-mockups/__tests__/schema.test.ts`):
- Valid JSON → parses + validates OK
- Missing `design_intent` → fail with path
- Invalid enum (`"obsolete"` instead of `"forward-refactor-obsolete"`) → fail with enum list
- `confidence > 1.0` → fail (out of range)

**3. `generate-deliverables.mjs`** (`apps/web/scripts/audit-mockups/__tests__/generate-deliverables.test.ts`):
- 224 mockups in aggregated results → 224 fidelity.json files generated
- 12 `forward-refactor-obsolete` → queue markdown has 12 obsolete review items
- HTML+JSX pair disagreement → queue calls it out
- Generated fidelity.json conforms to `validate-fidelity.mjs` schema (smoke test via subprocess)

**4. `create-tracking-issues.mjs`** (mocked GitHub API):
- N obsolete entries + valid API → N issues created, fidelity.json updated
- API rate limit on issue #3 → backoff + retry succeeds
- API failure on issue #5 of 10 → closes #1-4 (rollback), fails clearly

### Integration tests

- E2E: orchestrator dispatches 6 stub Agents (mocked) → verifies aggregated results flow + final deliverables shape. ~1 test, fast (<5s).
- Real-world (post-merge): `pnpm lint:fidelity --all` validates all 224 generated files.

### Manual verification

- Random sample 10 mockups → verify classification matches manual inspection
- Run `validate-fidelity.mjs` on all 224 files → 0 failures
- Verify designer queue markdown renders correctly in GitHub PR view

## Acceptance criteria

- [ ] 224 `<mockup>.fidelity.json` files committed in `admin-mockups/design_files/`
- [ ] `pnpm lint:fidelity --all` passes 224/224
- [ ] `audits/YYYY-MM-DD-mockup-design-intent-audit.json` + `.md` committed
- [ ] `docs/for-developers/frontend/mockup-designer-review-queue.md` published
- [ ] Designer sign-off comment present on PR
- [ ] N GitHub tracking issues created (1 per obsolete), referenced in fidelity.json
- [ ] PR admin-squash merged to main-dev
- [ ] Umbrella #2063 body updated with Phase B row + Phase 3 effort revised by post-audit numeric scope

## Out of scope

- Component-level migration to Storybook (that is Phase C — DS-17-9..13)
- Designer queue automated polling bot (manual `gh pr view` polling for now)
- Mockup deletion of `forward-refactor-obsolete` files (deferred to Phase C — when migrating, decide DELETE or skip)
- Cross-locale mockup variants (current mockups are it-IT only)

## References

- Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
- Phase 2.5 spec: `docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md`
- Phase 4 prelude spec: `docs/superpowers/specs/2026-06-10-ds-17-phase-4-prelude-intl-hardening-design.md`
- Fidelity schema: `apps/web/scripts/mockup-annotations/validate-fidelity.mjs`
- Fidelity examples: `docs/for-developers/frontend/templates/examples/*.fidelity.json`
- Pattern docs: `docs/for-developers/frontend/page-mock-story-pattern.md`
- MOCKUPS_INDEX: `admin-mockups/MOCKUPS_INDEX.md` (137 rows: 75 page-mock + 57 component-mock + 14 dev-fixture)
- Companion: `admin-mockups/README.md` (narrative design handoff)
