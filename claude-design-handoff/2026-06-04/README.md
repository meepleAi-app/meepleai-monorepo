# MeepleAI Prototype — Handoff Bundle 2026-06-04

Exported from claude.ai/design (design ID `038677b3-6790-4c20-92e8-f770043129e0`) after 5 iteration turns + 1 gap report turn + 5 socratic-mode tension resolutions.

This folder is **gitignored** (see repo `.gitignore`). Regenerable by re-exporting from Claude Design.

## Quickstart

```powershell
# From this folder
python -m http.server 8765
# Open http://localhost:8765/MeepleAI%20Prototype.html
```

The prototype is a single-page React app that uses Babel standalone in-browser (no build step). Toggle between light/dark via `data-theme` attribute on `<html>`. Toggle 5-state preview from the topbar (default / empty / loading / error / offline).

## Structure

| Path | Content |
|---|---|
| `MeepleAI Prototype.html` | Entry point. Loads tokens + components + prototype CSS, then 15 JSX modules via `babel-standalone`. |
| `assets/tokens.css` | Design tokens (HSL colors, type, spacing, radius, shadow). Source of truth. |
| `assets/components.css` | Composed primitives (phone frame, nav, cards). |
| `assets/prototype.css` | Prototype-specific layout (sidebar nav, screen containers, GAP badges). |
| `assets/data.js` | Fake dataset — 9 entities (game, player, session, agent, kb, chat, event, toolkit, tool). |
| `js/app.jsx` | Root component, routing state machine, sidebar nav, topbar state toggle. |
| `js/lib.jsx` | Shared helpers, dataset accessors, formatters. |
| `js/data-gn.jsx` | GameNight-specific fixture (the bulk of the demo). |
| `js/drawer.jsx` | Drawer stack pattern (slide-over with swap, ESC backtrack, close-all). |
| `js/screen-*.jsx` | 12 screen modules — one per route prototyped. |

## Routes prototyped (13)

- `/dashboard` (priority B-A-C-D — Prossimi > Recenti > Suggested > Friends)
- `/library` (collection personale)
- `/games/[id]` (game detail with 5-tab layout)
- `/game-nights` (timeline grouped by month)
- `/game-nights/new` (3-step wizard)
- `/game-nights/[id]` (detail with hero + RSVP + sessions)
- `/game-nights/[id]/live` (immersive layout, no sidebar)
- `/sessions` (cross-GameNight timeline grouped by game)
- `/discover` (hero + 4 horizontal-scroll sections)
- `/agents` (grid filterable by category)
- `/agents/[id]` (4-tab detail)
- `/login` + `/register` (modal centered)
- `/onboarding` (3-step wizard fullscreen)

Stub routes (linked but not built): `/games`, `/knowledge-base`, `/toolkit/[id]`, `/sessions/[id]`, `/game-nights/[id]/summary`.

## Related docs

- **Gap report**: [`docs/for-developers/audits/2026-06-04-claude-design-gap-report.md`](../../docs/for-developers/audits/2026-06-04-claude-design-gap-report.md) (38 gap, 5 categories)
- **Domain model**: [`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`](../../docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md) (20 invariants, 5 resolved tensions)
- **Workflow pattern**: see `MEMORY.md` entry `claude-design-demo-workflow.md` for future rerun

## Screenshots

`screenshots/` folder contains progressive snapshots captured during the 5 iteration turns. Useful as a visual change log. Numbered prefix = iteration order, semantic name = what was shown.

## Reproduction

To regenerate this bundle:

1. Rebuild source folder: `claude-design-bundle/` (see `docs/for-developers/workflows/claude-design-demo-prompts.md` if exists, otherwise `cp` script in repo README)
2. Link folder to a new design in claude.ai/design
3. Replay the 5 turns + 1 gap report from the workflow
4. Topbar canvas → Export → Handoff bundle → download zip
5. Extract into a date-versioned subfolder of `claude-design-handoff/`
