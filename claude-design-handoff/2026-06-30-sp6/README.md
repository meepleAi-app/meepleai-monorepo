# Claude Design handoff — SP6 Libro-Game (2026-06-30)

Snapshot del prototipo React esportato da [claude.ai/design](https://claude.ai/design) al termine della demo + gap audit della wave **SP6 Libro-Game** (issue [#1888](https://github.com/meepleAi-app/meepleai-monorepo/issues/1888)).

- **Run**: 2026-06-30 · progetto "Test line A" · modello Opus 4.8 · 5 turni (Step A end-to-end)
- **Gap report companion**: [`docs/for-developers/audits/2026-06-30-claude-design-gap-report-sp6.md`](../../docs/for-developers/audits/2026-06-30-claude-design-gap-report-sp6.md) — **49 gap** (ROUTE 9 · STATE 4 · CTA 16 · ENTITY 19 · TOKEN 0; HIGH 6).
- **Follow-up HIGH**: umbrella [#2619](https://github.com/meepleAi-app/meepleai-monorepo/issues/2619).
- **Bundle seed** (input): `claude-design-bundle/sp6-libro-game/` (gitignored) + prompt committati in [`docs/for-developers/workflows/claude-design-demo-prompts.md`](../../docs/for-developers/workflows/claude-design-demo-prompts.md).

## Contenuto

| File | Cosa |
|---|---|
| `LibroGame Prototype.dc.html` | il prototipo finale — single-page, phone frame 375px, route state machine + demo dock (15 route) |
| `support.js` | runtime/shim dell'export Claude Design (referenziato dal `.dc.html`) |
| `tokens.css` | design tokens del prototipo |
| `thumbnail.webp` | preview |

> La cartella `uploads/` dell'export originale (seed: i 15 mockup + scaffold) **non** è inclusa — è ridondante coi sorgenti in `admin-mockups/design_files/` e il prototipo non la referenzia.

## Come navigare

```powershell
cd claude-design-handoff/2026-06-30-sp6
python -m http.server 8765
# Apri http://localhost:8765/LibroGame%20Prototype.dc.html
```

> È un **Design Component** (`.dc.html`) con `support.js` come runtime, non un HTML statico classico come il baseline 2026-06-04. Se non renderizza standalone, riaprilo dal progetto "Test line A" su claude.ai/design.

## 15 route prototipate

Discover&pick: `library-search` · `game-detail` · `game-onboarding` (prereq). Setup: `setup-wizard` · `setup-chat`. Play: `play-session` (pixel-twin) · `translate-viewer` · `encounter-cheatsheet`. Close: `session-end` (3-way) · `resume-picker` (5 stati). Remaining: `glossary-editor` · `quota-credits` · `error-states` (9 stati) · `game-night-storyboard` · `house-rule` (AgentMemory).

Il **demo dock** (toggle `showDock`) salta tra route e stati; `startScreen` (Tweaks) sceglie la schermata iniziale.

## Hot-spot di test (le invarianti rese visibili dalla demo)

I 6 gap HIGH sono **allineamento UI↔dominio** (il backend `GameNightEvent`/`GameBook` è già shipped; i mockup non lo renderizzavano). Il prototipo li ha design-injected — verificali:

- **#10 max-1-live**: route `Play · sessione` → dock "⚠ Avvia 2ª sessione live" apre il modal di blocco.
- **#14 Ora di inizio derivata**: chip read-only "▶ Ora di inizio … · derivata" in play.
- **#15 promotion** + **#8 transition** + **#1 1→N**: route `GameNight · storyboard` (capstone) + strip "Serata da Marco" in setup/play/close.
- **GameBook 1..N (FIX 2 applicato)**: route `Onboarding · prereq` → book-manager generalizzato (1 "Manuale/Regolamento" + "+ Aggiungi libro" 0..N + role multi-select + toggle fisico), niente più "Press Start + Rules" hardcoded.

## Fix applicati al prototipo durante la run (operator-driven)

- **FIX 1** — chip-row/strip → `flex-wrap:wrap` (Libreria chips, setup-chat suggested-prompts, glossary filter, house-rule connection bar, game-detail tab bar). Nessun label tagliato.
- **FIX 2** — GameBook 1..N book-manager (de-hardcoda Press Start+Rules; spec-panel vs `GameBook` aggregate shipped).
