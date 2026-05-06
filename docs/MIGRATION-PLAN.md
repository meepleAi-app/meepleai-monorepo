# Docs Reorganization — Migration Plan

**Status**: DRAFT (proposal) | **Date**: 2026-05-05 | **Owner**: TBD

Companion to [`_audit-2026-05-05.csv`](./_audit-2026-05-05.csv).

---

## Goal

Refactor `docs/` (currently 941 files, 30 MB, 19 top-level dirs) into 3 audience-segmented roots:

```
docs/
├── for-claude/        # AI agent + architect: dense technical reference
├── for-developers/    # Contributor humans: task-oriented guides
└── for-users/         # End users (Wikipedia-style): what the app does
```

Plus an out-of-tree archive: `.docs-archive/` (gitignored from main builds, optional separate repo).

---

## Acceptance Criteria

- [ ] Top-level `docs/` ≤ 5 entries
- [ ] No directory > 50 direct files
- [ ] Each root README answers "who/what/where" in ≤ 20 lines
- [ ] `for-users/` covers 5 Alpha features (auth, games+BGG, PDF upload, RAG chat, library)
- [ ] CI link checker green (zero broken internal links)
- [ ] `CLAUDE.md` updated with new paths
- [ ] `git log --follow` preserves history on every moved file

---

## Phased Migration (5 PRs, NOT a big bang)

### Phase 0 — Prep (1 PR, no moves)
1. Add CI workflow `.github/workflows/docs-linkcheck.yml` using `lychee` or `markdown-link-check`.
2. Baseline run on current `docs/` — capture current broken-link count as floor.
3. Add `.docs-archive/` to `.gitignore` if going out-of-tree (or create `docs-archive` branch).
4. Create the 3 new root dirs with stub READMEs.

### Phase 1 — Archive purge ✅ EXECUTED 2026-05-06
**Lowest risk**: archived stuff has fewest inbound links — confirmed by audit
(zero incoming references from active docs to any moved path).

Outcome: ~290 files moved out of `docs/` into `.docs-archive/` (in-repo).

```bash
# From repo root
git mv docs/archive             .docs-archive/historical
git mv docs/pdca                .docs-archive/pdca
git mv docs/roadmap             .docs-archive/roadmap
git mv docs/research            .docs-archive/research
git mv docs/evaluation-reports  .docs-archive/evaluation-reports
git mv docs/audit               .docs-archive/audit
git mv docs/superpowers/plans   .docs-archive/superpowers/plans
git mv docs/superpowers/research .docs-archive/superpowers/research
git mv docs/superpowers/design-review .docs-archive/superpowers/design-review
git mv docs/for-developers/specs/archived .docs-archive/superpowers/specs-archived
git mv docs/specs/game-night-sprint2 .docs-archive/specs/game-night-sprint2
git mv docs/meepleai-manual-test-plan.pdf .docs-archive/meepleai-manual-test-plan.pdf
rm -rf docs/brand/__pycache__   # bytecode artifact
rm docs/skills-reference.pdf    # duplicate of .md
```

Run `scripts/rewrite-docs-links.sh` (see below) before commit.

### Phase 2 — Aggressive prune ✅ EXECUTED 2026-05-06 (REDESIGNED from "asset extraction")
**Original plan** moved binaries to `assets/`. **Redesigned plan** instead deletes
everything non-essential — user feedback: "non ho interesse a mantenere vecchia
documentazione, voglio ridurre sensibilmente". Asset extraction was "shifting the
noise"; aggressive deletion is the only path to a navigable docs/.

**Outcome**: 670 → ~120 files (~82% reduction), 26 → 13 top-level dirs.

**Deleted categories**:
1. **Code-mirror docs** — `docs/bounded-contexts/*.md` (18 files mirrored 1:1
   in `apps/api/src/Api/BoundedContexts/{X}/README.md`)
2. **Auto-generated diagrams** — all `.png`/`.svg` in `docs/bounded-contexts/diagrams/`
   (regen on demand from `.mmd` source via `.github/workflows/generate-diagrams.yml`)
3. **Auto-generated API ref** — `docs/for-claude/api-reference/{bounded-contexts,endpoints,session-tracking}/`
   (Scalar UI at `/scalar/v1` is the SSOT; only `api/rag/` concept docs survive)
4. **Process artifacts** — `docs/superpowers/{mockups,fixtures}`,
   `docs/for-developers/frontend/{screenshots,ux-mockups,layout-concepts}`
5. **Inactive specs/plans** — kept only 4 specs referenced by ADRs/CLAUDE.md +
   1 in-flight plan (`2026-05-06-sp6-libro-game-migration.md`)
6. **Single-file dead folders** — `pages/`, `rulebooks/`, `contracts/`, `migrations/`,
   `content-strategy/`, `navigation/`, `brand/`, `features/`, `libro-game-assistant/`
7. **Architecture non-canonical** — `docs/for-claude/architecture/{components,ddd,overview}/`
   (sacred ADRs preserved 100%)
8. **Aggressive testing/deployment/development prune** — kept only files referenced
   by CLAUDE.md or essential for production ops

**Verified before deletion**:
- All 7 CLAUDE.md → docs/ links survive
- 18 BC mirror READMEs exist in code
- `generate-diagrams.yml` workflow exists (binary regen)
- `git log --all --diff-filter=D --name-only` recovers any deleted file

**Critical**: rewrite all `![](./diagrams/...)` references in surrounding `.md` files. Script handles this.

### Phase 3 — for-claude/ build ✅ EXECUTED 2026-05-06
Architecture (ADRs + diagrams + cross-cutting), api/rag/ concept docs, SKILLS-REFERENCE.md
consolidated under `docs/for-claude/`. Stale ADR refs (docs/01-architecture/, docs/03-api/,
docs/04-deployment/, docs/plans/, docs/research/) patched to current paths or replaced with
Scalar UI / .docs-archive notes.

```bash
mkdir -p docs/for-claude/{architecture,api-reference,patterns}
git mv docs/architecture                     docs/for-claude/architecture
git mv docs/bounded-contexts                 docs/for-claude/architecture/bounded-contexts
git mv docs/api                              docs/for-claude/api-reference
git mv docs/contracts                        docs/for-claude/api-reference/contracts
git mv docs/for-developers/workflows/agent-architecture   docs/for-claude/patterns/agent-architecture
git mv docs/for-claude/skills-reference.md              docs/for-claude/skills-reference.md
```

### Phase 4 — for-developers/ build ✅ EXECUTED 2026-05-06
development → workflows/, testing → testing/, frontend → frontend/, deployment → deployment/,
operations → operations/, security → security/, templates → templates/, superpowers/specs →
specs/, superpowers/plans → plans/, alpha-zero-scope.md → for-developers/alpha-zero-scope.md.
All CLAUDE.md → docs/ links auto-rewritten and verified OK.

```bash
mkdir -p docs/for-developers/{workflows,testing,frontend,deployment,security}
git mv docs/development                      docs/for-developers/workflows
git mv docs/testing                          docs/for-developers/testing
git mv docs/frontend                         docs/for-developers/frontend
git mv docs/deployment                       docs/for-developers/deployment
git mv docs/operations                       docs/for-developers/deployment/operations
git mv docs/security                         docs/for-developers/security
git mv docs/compliance                       docs/for-developers/security/compliance
git mv docs/migrations                       docs/for-developers/workflows/migrations
git mv docs/templates                        docs/for-developers/workflows/templates
git mv docs/specs                            docs/for-developers/workflows/specs
git mv docs/superpowers/specs                docs/for-developers/workflows/superpowers-specs
git mv docs/superpowers/fixtures             docs/for-developers/testing/fixtures
git mv docs/for-developers/alpha-zero-scope.md              docs/for-developers/alpha-zero-scope.md
rmdir docs/superpowers
```

### Phase 5 — for-users/ wiki ✅ EXECUTED 2026-05-06
5 Alpha feature pages written (rag-chat existed from Phase 0; pdf-upload, games-bgg, library,
auth NEW), faq.md NEW, meepleai-project-brief.md moved to for-users/. Italian-first.
Wikipedia-style template: cosa fa, come si usa, limiti, costi, FAQ, link correlati.

```bash
mkdir -p docs/for-users/{features,roles,tutorials,brand}
git mv docs/user-guides                      docs/for-users/roles
git mv docs/features                         docs/for-users/features
git mv docs/libro-game-assistant             docs/for-users/features/libro
git mv docs/brand                            docs/for-users/brand
git mv docs/content-strategy/*.md            docs/for-users/brand/
git mv docs/navigation/*.md                  docs/for-users/navigation.md
git mv docs/pages                            docs/for-users/pages   # verify content first
git mv docs/for-users/meepleai-project-brief.md        docs/for-users/README.md  # adapted as wiki landing
```

Then **write** missing wiki pages — use the [RAG Chat pilot](./for-users/features/rag-chat.md) as template:
- `for-users/features/auth.md`
- `for-users/features/games-bgg.md`
- `for-users/features/pdf-upload.md`
- `for-users/features/library.md`
- `for-users/faq.md`

---

## Link Rewrite Strategy

### Automated (recommended)
Create `scripts/rewrite-docs-links.sh`:

```bash
#!/usr/bin/env bash
# Rewrite markdown links after a docs/ move.
# Usage: rewrite-docs-links.sh <old-prefix> <new-prefix>
set -euo pipefail
OLD="$1"; NEW="$2"
# Find all .md files (incl. CLAUDE.md and READMEs in code)
git ls-files '*.md' | while read -r f; do
  # In-line links: [text](./old/path) and [text](old/path)
  sed -i "s|]($OLD|]($NEW|g; s|](\\./$OLD|](\\./$NEW|g" "$f"
done
```

Run after **each** `git mv` batch:
```bash
./scripts/rewrite-docs-links.sh "docs/for-claude/architecture/" "docs/for-claude/architecture/"
```

### Manual checks
After all phases, run:
```bash
# Find any surviving old paths
git grep -nE 'docs/(archive|pdca|roadmap|user-guides|architecture|bounded-contexts)/'
```

### CLAUDE.md update points
Specifically update these references in root `CLAUDE.md`:
- L11: `docs/for-developers/workflows/snapshot-seed-workflow.md` → `docs/for-developers/workflows/snapshot-seed-workflow.md`
- L? (every `./docs/...` link — ~30 instances). Grep before/after migration.

---

## Rollback

Each phase = 1 PR. If something breaks (e.g., link checker explodes):
```bash
git revert <merge-sha>
```
No phase moves > 250 files, so blast radius is bounded.

For **safety net** before Phase 1: tag current state.
```bash
git tag pre-docs-reorg-2026-05-05
git push origin pre-docs-reorg-2026-05-05
```

---

## Out of Scope (separate work)

- Rewriting/consolidating duplicate content (deferred — first move, then dedupe)
- Auto-generating diagram PNGs from `.mmd` source (Phase 2 just moves what exists)
- Translating user-facing wiki to English (Italian-first, English later)
- Versioning the wiki (semver, snapshots)

---

## Open Questions

1. **`.docs-archive/` location**: in-repo (gitignored from main? bloat git history) or **separate repo** `meepleai-docs-archive`? Recommendation: separate repo to keep main clone lean — but defer decision to Phase 1.
2. **Asset hosting**: `assets/` in-repo or push to S3/R2 with link rewriting? `assets/` likely OK (~10MB), monitor growth.
3. **Wiki publishing**: serve `docs/for-users/` via Docusaurus / MkDocs / Next.js MDX route in `apps/web/`? Out of scope for this PR but design wiki to be portable.
4. **Bounded-context READMEs in code** (`apps/api/src/Api/BoundedContexts/{Context}/README.md`): keep there, don't move. Cross-link from `docs/for-claude/architecture/bounded-contexts/`.

---

## Estimated effort

| Phase | Files | Effort | Risk |
|-------|-------|--------|------|
| 0 — Prep | 0 | 2h | Low |
| 1 — Archive | ~100 | 3h | Low |
| 2 — Assets | ~250 | 4h | Medium (image links) |
| 3 — for-claude | ~200 | 4h | Medium (CLAUDE.md refs) |
| 4 — for-developers | ~150 | 4h | Medium |
| 5 — for-users | ~40 + writing | 8h+ | Low (mostly new content) |
| **Total** | **~740 moved + 200 new** | **~25h** | — |

Plus ~5h for link rewriting tooling and CI setup.
