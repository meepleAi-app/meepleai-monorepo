# Claude Design handoff — SP8 Mobile Parity + Libro-Game Companion (2026-07-15)

Snapshot della demo + gap audit della wave **SP8 mobile** su [claude.ai/design](https://claude.ai/design) (issue [#1890](https://github.com/meepleAi-app/meepleai-monorepo/issues/1890)).

- **Run**: 2026-07-15 · progetto "SP8 Mobile Design Bundle" · 5 turni operatore-guidati (runbook) + socratic pass.
- **Gap report**: [`docs/for-developers/audits/2026-07-15-claude-design-gap-report-mobile.md`](../../docs/for-developers/audits/2026-07-15-claude-design-gap-report-mobile.md) — **25 gap** (surface A library-mobile 16 · surface B companion 9; CRITICO 5 · IMPORTANTE 10 · MINORE 10). 16 runtime-confirmed · 1 superato (A11) · 10 new.
- **Bundle seed** (input): `claude-design-bundle/sp8-mobile/` (gitignored) + system-prompt/runbook committati in [`docs/for-developers/workflows/claude-design-demo-prompts.md`](../../docs/for-developers/workflows/claude-design-demo-prompts.md) § SP8.
- **PR**: [#2983](https://github.com/meepleAi-app/meepleai-monorepo/pull/2983).

## Nota — niente prototipo esportato

A differenza di [2026-06-04](../2026-06-04/) (`MeepleAI Prototype.html`) e [2026-06-30-sp6](../2026-06-30-sp6/) (`LibroGame Prototype.dc.html`), questa run **non ha un prototipo unificato**: Claude Design ha **replayato i sorgenti del bundle così com'erano** (React/Babel, `sp4-library-mobile.jsx` + companion), senza rigenerarli. L'handoff sono quindi i **13 screenshot dei turni** (surface A / library-mobile) + il gap report. La cartella input `sp8-mobile/` dell'export originale **non** è inclusa qui — è ridondante con `admin-mockups/design_files/` (rigenerabile via `scripts/build-claude-design-bundle.sh sp8`).

## Screenshot (surface A — library-mobile, viewport 375px)

| File | Turno | Cosa mostra · gap collegati |
|---|---|---|
| `screenshots/01-default.png` | T1 · step 1 | Default: header "La mia libreria · 47 games", tab **Games 47** / Sessions / Chat / ⋯ (dot), hamburger ☰. Visibili **A-01** (naming "Games") + **A-10** (hamburger) |
| `screenshots/01-default-full.png` · `01-default-recent.png` | T1 · step 1 | Scroll completo + sezione "Recente" (non-sticky) |
| `screenshots/02-overflow.png` | T1 · step 2 | Overflow "⋯" → menu Agents (dot) / KB |
| `screenshots/03-filters.png` · `01-03-filters-clean.png` · `02-03-filters-clean.png` | T1 · step 3 | Filtri bottom-sheet 80vh (grab-handle decorativo → **A-08** no swipe, **A-03** no ESC) |
| `screenshots/01-04-bulk.png` · `02-04-bulk.png` | T1 · step 4 | Bulk-select via long-press (top-bar "N selezionato", checkbox) |
| `screenshots/05-bulkactions.png` · `05-bulkactions-full.png` | T1 · step 5 | FAB "⋮" → bulk actions sheet (Archivia/Tag/Esporta/**Rimuovi** danger → **A-02** toast + no-confirm) |
| `screenshots/01-06-tablet.png` · `02-06-tablet.png` | T1 · reflow | Tablet 768px: 5 tab inline (no overflow), grid 2-col |

> Surface B (companion s05/06/07) è stata replayata testualmente (Turn 2) ma non catturata in screenshot in questa run — i finding B-01..B-09 sono nel gap report con evidenza `file:riga`.

## Come rieseguire

Bundle → `scripts/build-claude-design-bundle.sh sp8`; caricare su claude.ai/design a canvas 375px; system-prompt SP8 (primo messaggio) + i 5 prompt-turno del runbook — tutto in [`claude-design-demo-prompts.md`](../../docs/for-developers/workflows/claude-design-demo-prompts.md) § SP8.
