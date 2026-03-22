# Documentation Review Needed

Files requiring manual review decision: **Keep** or **Delete**

## 1. HTML Mockups (19 files, ~500KB)

**Decision needed**: Keep as reference or delete (functionality implemented)?

### RAG Strategy Diagrams (7 files, 184KB)
- `docs/api/rag/diagrams/rag-flow-current.html` (28K)
- `docs/api/rag/diagrams/strategy-flow-CONSENSUS.html` (24K)
- `docs/api/rag/diagrams/strategy-flow-FAST.html` (20K)
- `docs/api/rag/diagrams/strategy-flow-PRECISE.html` (24K)
- `docs/api/rag/diagrams/strategy-flows-comparison.html` (20K)
- `docs/api/rag/diagrams/strategy-flows-meeple-style.html` (40K)
- `docs/api/rag/diagrams/strategy-matrix-view.html` (28K)

**Recommendation**: DELETE if strategies are stable and documented in code/markdown

### Frontend Mockups (5 files, 200KB)
- `docs/frontend/mocks/layout-desktop-mock.html` (20K)
- `docs/frontend/mocks/layout-mobile-mock.html` (24K)
- `docs/frontend/mockups/chat-card-integrated.html` (40K)
- `docs/frontend/mockups/chat-card-mockup.html` (56K)
- `docs/components/ui/meeple-card-mockup.html` (60K)

**Recommendation**: DELETE - layouts implemented, keep only design-system docs

### Roadmap HTML (3 files, 144KB)
- `docs/chat-agent-roadmap.html` (36K)
- `docs/DEVELOPMENT-ROADMAP.html` (28K)
- `docs/development-workflow.html` (80K)

**Recommendation**: DELETE if content duplicated in `docs/roadmap/*.md`

### Playground Debug (1 file, 36KB)
- `docs/playground-debug-roadmap.html` (36K)

**Recommendation**: DELETE (debug artifact)

---

## 2. Template Files (7 files)

**Decision needed**: Which templates are actively used?

- `docs/templates/api-reference-template.md`
- `docs/templates/architecture-template.md`
- `docs/templates/bounded-context-template.md`
- `docs/templates/development-guide-template.md`
- `docs/templates/TEAM-INSTRUCTIONS.md`
- `docs/templates/testing-guide-template.md`
- `docs/templates/troubleshooting-template.md`

**Recommendation**:
- KEEP: `bounded-context-template.md` (used for new contexts)
- DELETE: Rest (not actively used based on recent doc patterns)

---

## 3. Large PNG Diagrams (42 files, 6MB+)

**Decision needed**: Convert to SVG or keep PNG?

### Bounded Contexts Diagrams (37 PNG files, 5.5MB)
All in `docs/bounded-contexts/diagrams/*/`

**Options**:
- **Keep both**: PNG for quick preview + SVG for editing (current state)
- **SVG only**: Delete all PNG, keep SVG (saves ~4MB)
- **Keep all**: Reference value for architecture

**Recommendation**: **Keep both** - PNGs useful for quick GitHub preview

### Archive Screenshots (5 PNG files, 5.4MB)
- `docs/archive/epic-4068/*.png` (5 large screenshots)

**Recommendation**: **DELETE entire archive/epic-4068/** - Epic completed, no historical value

---

## 4. Misplaced Files

### claudedocs/ in docs/ (25 files)

**Issue**: `docs/claudedocs/` should be root-level `claudedocs/`

**Action needed**:
```bash
mv docs/claudedocs/ ./claudedocs/
# OR delete if duplicates root claudedocs/
```

**Check**: Does root `claudedocs/` already exist with same content?

---

## 5. Obsolete Folders to Remove

### Small/Empty Folders (candidates for deletion)
- `docs/blog/` (1 file - Epic announcement, delete?)
- `docs/marketing/` (1 file - Epic announcement, delete?)
- `docs/video/` (0 files after Epic cleanup - DELETE)
- `docs/pdca/` (30 files - what is this? Keep or consolidate?)
- `docs/evaluation-reports/` (? files - keep or archive?)

---

## 6. Remaining Epic-Specific Docs (not Epic #4068)

**Epic #3490, #4136, #4604, etc.**:
- `docs/roadmap/epic-3490-*.md` (3 files)
- `docs/frontend/epics/` (4 files - various Epics)
- `docs/epics/` (2 files)
- `docs/claudedocs/epic-4136-implementation-plan.md`

**Decision**: Archive completed Epics or keep active ones?

---

## Auto-Cleanup Recommendations (for your approval)

```bash
# Safe deletions (high confidence):
git rm -r docs/archive/epic-4068/        # Epic completed, 6.5MB
git rm docs/*roadmap*.html               # 3 HTML roadmaps (duplicates of .md)
git rm docs/playground-debug-roadmap.html
git rm -r docs/blog docs/marketing docs/video  # Obsolete Epic content

# Template cleanup:
git rm docs/templates/*-template.md      # Keep only bounded-context-template.md

# Mockup cleanup:
git rm docs/api/rag/diagrams/*.html      # 7 RAG strategy HTMLs
git rm docs/frontend/mocks/*.html docs/frontend/mockups/*.html  # 4 layout mockups
```

**Estimated savings**: ~8MB, -50 files

---

## Final Proposed Structure (17 → 12 folders)

```
docs/
├── api/              ✅ Keep (108 files)
├── architecture/     ✅ Keep (43 files) - merged from 3 sources
├── bounded-contexts/ ✅ Keep (144 files) - core domain docs
├── deployment/       ✅ Keep (39 files) - merged from 2 sources
├── development/      ✅ Keep (46 files)
├── frontend/         ✅ Keep (33 files) - merged from 3 sources
├── migrations/       ✅ Keep (1 file) - merged from 2 sources
├── security/         ✅ Keep (6 files) - merged from 2 sources
├── testing/          ✅ Keep (43 files) - merged from 2 sources
├── user-guides/      ✅ Keep (30 files) - merged from 3 sources
├── templates/        🟡 Review (keep 1, delete 6)
├── archive/          🔴 Delete epic-4068/ (6.5MB)
├── roadmap/          🟡 Review Epics
├── epics/            🟡 Review + consolidate with roadmap/
├── evaluation-reports/ 🟡 Review content
├── pdca/             🟡 Review (30 files - what is this?)
└── claudedocs/       🔴 Move to root-level
```

**Next Action**: Should I proceed with auto-cleanup recommendations above?
