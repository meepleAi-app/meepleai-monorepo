# Mockup Demo Navigation — Design

**Date:** 2026-05-20
**Author:** brainstormed with user
**Status:** draft (pending user review)
**Scope:** Phase 1 — make all 69 mockup screens in `admin-mockups/design_files/` (HTML files; 52 of them backed by sibling React JSX = 121 files to potentially modify) navigable as an interactive demo, starting from a dashboard entry point. Phase 2 (porting to the real Next.js app) is explicitly out of scope and will have its own spec.

---

## 1. Goal

Transform the static HTML/JSX mockups in `admin-mockups/design_files/` into a self-navigating demo. The user opens a single entry file, lands on `sp4-dashboard.html`, and can click through the full product surface (69 mockup screens) following plausible user journeys — without a build step, without new runtime dependencies, openable via `file://` or any static web server.

A secondary goal is to produce a `nav-map.md` that documents every cross-mockup destination — this artifact becomes input for Phase 2 (mapping mockup links onto real Next.js routes).

## 2. Non-goals

- Hover states, animations, or transition effects between mockups.
- State persistence (e.g., "remember selected game" across mockups).
- Mock data realism (existing data stays).
- Simulated form submissions that route to a "success" mockup.
- Interactive modals — mockups that depict a modal keep it visible as-designed.
- Porting nav decisions to the real Next.js app. That is Phase 2.

## 3. Architecture

### 3.1 No build, no dependencies

Mockups remain plain HTML + JSX-loaded-via-Babel-standalone. No new runtime libraries, no router, no bundler. Files must work via `file://` and via static web server.

### 3.2 Navigation primitive

Two file types receive different patches:

**HTML static (17 files — HTML mockups without a sibling JSX):** add `href="target.html"` to existing `<a>` or wrap `<button>` in `<a>` preserving CSS classes. Where the element is a non-anchor (e.g., `<div class="card">`), add `onclick="window.location.href='target.html'"` to keep CSS untouched.

**JSX (52 files; 52 HTML mockups are backed by a sibling `.jsx` loaded via Babel standalone):** modify clickable elements in the JSX to add `onClick={() => { window.location.href = 'target.html'; }}`. Do NOT change the JSX tag (preserves CSS). If an `onClick` already exists, wrap (do not replace): `onClick={(e) => { existingHandler(e); window.location.href = 'target.html'; }}`.

JSX-controlled components that mutate state on click get a `setTimeout(() => window.location.href = ..., 0)` so React has a tick to settle before navigation.

### 3.3 Entry point

New file `admin-mockups/design_files/index.html` (~20 lines): meta-refresh + JS redirect to `sp4-dashboard.html`. Opening `index.html` lands the user in the dashboard.

### 3.4 Canonical navigation map

Sidebar / topbar / bottom-nav appear in many mockups with minor variants. To prevent inconsistency, ONE canonical destination per nav voice is defined and applied uniformly everywhere that voice appears.

Authenticated-app sidebar mapping:

| Sidebar item | Destination |
|---|---|
| Dashboard / Home | `sp4-dashboard.html` |
| Discover | `sp4-discover.html` |
| Library | `sp4-library-desktop.html` |
| Games | `sp4-games-index.html` |
| Players | `sp4-players-index.html` |
| Sessions | `sp4-sessions-index.html` |
| Agents | `sp4-agents-index.html` |
| Game Nights | `sp4-game-nights-index.html` |
| Knowledge Base | `sp4-kb-hub.html` |
| Toolkits | `sp4-hub-toolkits.html` |
| Notifications | `notifications.html` |
| Settings | `settings.html` |
| Profile (avatar in topbar) | `settings.html` |
| Logout | `sp3-join.html` |

Public topbar (sp3-*, public, hub mockups): logo → `index.html`; "Login / Join" → `sp3-join.html`; menu items → `sp3-how-it-works.html`, `sp3-faq-enhanced.html`, `sp3-legal.html`.

Gameplay topbar (librogame, sp6): back arrow → `sp4-game-detail.html`; pause/menu → `librogame-runthrough-resume-picker.html`; chat icon → `sp4-game-chat-tab.html`.

Event topbar (sp7): back arrow → `sp4-game-nights-index.html`.

### 3.5 Cluster taxonomy

Mockups are grouped into clusters; cluster determines which global nav skeleton is active:

| Cluster | Prefix | Count | Active nav |
|---|---|---|---|
| Design system docs | `00-`…`05-` | 6 | none |
| Public / marketing | `sp3-*` (8) + `public/notifications/onboarding/settings/auth-flow/mobile-app.html` (5) | 13 | public topbar |
| App authenticated (core, non-hub) | `sp4-*` excluding hub | 22 | sidebar + topbar |
| Hub composti | `sp4-hub-*` | 3 | sidebar + topbar |
| Gameplay librogame | `librogame-runthrough-*` (13) + `sp6-libro-game-*` (3) + `librogame-game-night-storyboard` (1) | 17 | gameplay topbar |
| Game night | `sp7-game-night-*` | 5 | event topbar |
| Nav primitives showcase | `nanolith-nav-*` | 3 | n/a |

Sum: 6 + 13 + 22 + 3 + 17 + 5 + 3 = 69 HTML mockups.

### 3.6 Intra-cluster mapping rules

Applied in order; first match wins.

**Hub cards** (`00-hub.html`, `sp4-hub-*`): each card → identically-named mockup. `00-hub.html` already has 29 hrefs wired — preserve.

**Index → Detail:**

| Index | Detail destination |
|---|---|
| `sp4-games-index` | `sp4-game-detail` |
| `sp4-players-index` | `sp4-player-detail` |
| `sp4-sessions-index` | `sp4-session-live` (active row) or `sp4-session-summary` (ended row) — one per state |
| `sp4-agents-index` | `sp4-agent-detail` |
| `sp4-game-nights-index` | `sp7-game-night-detail-rsvp` (future) / `sp7-game-night-live` (active) / `sp7-game-night-summary` (past) — one per state |
| `sp4-kb-hub` | `sp4-kb-detail` (entry doc); "Globale" link → `sp4-kb-globale` |
| `sp4-hub-toolkits` | `sp4-toolkit-detail` |
| `sp4-discover` | `sp4-game-detail` |

**Detail → contextual actions:**

| Button text / role | Destination |
|---|---|
| "Play" / "Avvia libro game" | `librogame-runthrough-game-onboarding` |
| "Setup" / "New session" | `librogame-runthrough-setup-wizard` |
| "Chat" / "AI Tutor" tab | `sp4-game-chat-tab` |
| "Citation" / footnote link | `sp4-citation-pdf-viewer` |
| "Add to library" | `sp4-library-desktop` |
| "Upload PDF" / "Aggiungi gioco" | `sp4-upload-wizard-extended` or `sp4-add-game-bgg-step` / `sp4-add-game-pdf-dedup` |
| "Crea Game Night" | `sp7-game-night-create` |
| "Glossary" | `sp6-libro-game-glossary-editor` (or `librogame-runthrough-glossary-editor` if in gameplay context) |
| "Edit" / "Modifica" | no-op tracked (out-of-scope policy) |

**Gameplay flow:**

```
game-detail → game-onboarding → setup-chat / setup-wizard → play-session
    ├── encounter-cheatsheet (battle CTA)
    ├── translate-viewer ("Traduci paragrafo" CTA)
    ├── glossary-editor (term tap)
    ├── quota-credits (credits CTA top-right)
    └── session-end → resume-picker
```

Error states reachable from every gameplay mockup via a "Show error demo" footer link, added to all gameplay mockups.

**Game night flow:**

```
game-nights-index → create → detail-rsvp → live → transition → summary
                                  └── join-public (shared link path)
```

**Auth / public flow:**

`sp3-join` → `auth-flow` → `onboarding` → `sp4-dashboard`. Footer legali everywhere → `sp3-legal`. FAQ → `sp3-faq-enhanced`. How it works → `sp3-how-it-works`.

**Cross-cluster "back to home":** logo is always clickable. Context-aware destination: authenticated → `sp4-dashboard`; public → `index.html`; gameplay → current game detail; event → `sp4-game-nights-index`.

### 3.7 Out-of-scope link policy

When a clickable element has no plausible destination even after rule chain + heuristic:

```javascript
onClick={(e) => { e.preventDefault(); console.info('demo: no target for X'); }}
```

Tracks the gap (searchable via `console.info` filter) without breaking the UI.

### 3.8 Heuristic for ambiguous cases

When a clickable element doesn't match a rule:

1. Match button text vs mockup filenames (case-insensitive fuzzy).
2. Match ≥70%: link automatically.
3. Match <70%: record in `nav-map.md` "TODO/Ambiguous" section for user review.
4. Zero plausible matches: apply out-of-scope policy.

## 4. Execution

Three phases with git checkpoints between each. Reversible.

### Phase A — Discovery & mapping

Python script `scripts/mockup-demo/build_map.py` (stdlib only):

1. Walks `admin-mockups/design_files/` for `.html` and `.jsx`.
2. Extracts clickable elements: `<a>`, `<button>`, elements with `onClick=`, elements with classes matching `*-card`, `*-row`, `*-nav-item`, `*-tab`.
3. Applies the rule chain (canonical nav → hub cards → index→detail → detail actions → flow gameplay/event → fuzzy heuristic).
4. Emits `docs/superpowers/specs/nav-map.md` with columns: `File | Selector | Button text | Destination | Confidence | Rationale`.

**User reviews and edits `nav-map.md`.** TODO rows ask the user to pick a destination.

### Phase B — Application

Same script with `--apply` flag (`apply_map.py`):

1. Reads the user-approved `nav-map.md`.
2. For each row, locates the element in the file via stored selector and applies the patch (HTML or JSX strategy per §3.2).
3. Creates `index.html` redirect.
4. Marks every patched element with a `/* DEMO-NAV */` comment for idempotence.

### Phase C — Validation

Python script `validate.py`:

1. For every `.html`, extract all `href` / `onClick` targets → verify destination file exists.
2. BFS reachability from `index.html`: count how many of the ~110 mockups are reachable.
3. Emit `nav-validation-report.html` with reachability graph + orphan list.
4. Manual spot-check: 10 representative paths clicked in Chrome via MCP browser tool.

### 4.1 Technical decisions

- **No deps:** Python 3.11+ stdlib only (`html.parser`, `re`, `pathlib`, `json`).
- **Scripts location:** `scripts/mockup-demo/{build_map.py, apply_map.py, validate.py}`.
- **JSX parsing:** regex-based with guards. Pattern: locate `<TagName ... className="..."` plus `onClick=` or known clickable patterns (`<button`, `<a`, `<div className="*card*">`, `<li className="*nav*">`). AST parsing for JSX is too brittle for this scope; the script's limits are documented in its docstring.
- **Idempotence:** re-running `apply_map.py` produces an empty diff (guarded by `/* DEMO-NAV */` marker).
- **Backups:** none committed. Git checkpoints between phases provide rollback.

### 4.2 Risks and mitigations

| Risk | Mitigation |
|---|---|
| Regex JSX parser breaks complex markup | `/* DEMO-NAV */` marker + dry-run + mandatory diff review before commit |
| Conflict with existing `onClick` that runs real logic | Wrap, not replace — preserves original handler |
| React-controlled mockup mutates state and blocks redirect | `setTimeout(() => ..., 0)` gives React a tick |
| `nav-map.md` becomes too long (1000+ rows) | Sections collapsible per cluster; deduplicated canonical-nav table separate from per-mockup tables |
| Path resolution broken via `file://` | All targets are relative same-directory paths; no `../` traversal |

## 5. Acceptance criteria

Phase 1 is complete when:

1. Opening `admin-mockups/design_files/index.html` redirects to `sp4-dashboard.html`.
2. From dashboard, every sidebar/topbar voice (14 items in canonical mapping) navigates to the correct destination.
3. From every `*-index.html`, at least one row/card is clickable → detail.
4. Gameplay loop (game-detail → onboarding → setup → play → end) is end-to-end navigable with no dead ends.
5. Auth flow (sp3-join → auth-flow → onboarding → dashboard) is end-to-end navigable.
6. Validation report: ≥95% of the 60 user-facing mockups (69 total minus the 6 design-system docs and 3 nav primitives showcases, which are intentionally excluded from the user journey) are reachable from `index.html`. Orphans documented with reason.
7. Spot-check: 10 paths clicked in browser with no console errors except the expected `console.info` out-of-scope traces.

## 6. Testing

**Automated** (Python scripts):
- `validate.py` checks every href/onClick target resolves to an existing file.
- BFS reachability from `index.html`.
- Idempotence check: `apply_map.py` run twice produces empty diff.

**Manual** (browser via MCP):
- Open `index.html` in Chrome via MCP tool, walk 10 representative paths (1 per cluster + gameplay end-to-end + game-night end-to-end).
- Verify no JS console errors (other than expected `console.info`).
- Verify no visual regression (CSS preserved).

## 7. Deliverables

1. `admin-mockups/design_files/index.html` — new entry redirect.
2. Up to 121 patched mockup files (69 HTML + 52 JSX) with nav wired.
3. `docs/superpowers/specs/nav-map.md` — approved mapping table.
4. `scripts/mockup-demo/{build_map.py, apply_map.py, validate.py}`.
5. `docs/superpowers/specs/nav-validation-report.html` — validation output (not committed in final form; produced on demand).
6. `admin-mockups/README.md` updated with "how to open the demo" instructions.

## 8. Estimate

- Phase A (discovery + nav-map.md generation): ~2h script work + user review window.
- Phase B (application): ~1h script run + ~1h edge-case fixes.
- Phase C (validation + spot-check): ~1h.
- **Total: ~5–6h of execution**, gated by user review of `nav-map.md` between A and B.

## 9. Phase 2 preview (out of scope for this spec)

Once the demo is approved visually, `nav-map.md` becomes the input to Phase 2:

- Each `(mockup → mockup destination)` row translates to a Next.js `<Link href="/route">` in the real app.
- Cluster mockup → Next.js route group: SP4 → `(authenticated)`, SP3 → `(public)`, librogame → `(authenticated)/gamebook` or a new route group, etc.
- Phase 2 will have its own spec + plan.
