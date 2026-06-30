# Claude Design demo — SP6 Libro-Game gap report (COMPLETE, 5/5 turns)

> ✅ **COMPLETE** — all 15 bundle screens built as interactive routes; Step A walked end-to-end across 5 turns.
>
> - **Run**: 2026-06-30 · project "Test line A" (claude.ai/design) · model Opus 4.8
> - **Bundle**: `claude-design-bundle/sp6-libro-game/` (15 mockups) · tracking issue #1888
> - **Baseline reference**: [`2026-06-04-claude-design-gap-report.md`](./2026-06-04-claude-design-gap-report.md) (38 gaps)
> - **Total gaps**: **49** (in-code `[GAP-*]` markers in `LibroGame Prototype.dc.html`)
> - **Two prototype fixes applied mid-run** (operator-driven): FIX 1 chip-row wrap; FIX 2 GameBook 1..N book-manager generalization (see §Fixes).

## Section 1 — Gap inventory by turn

### Turn 1 — discover & pick (13 gaps)
- `[GAP-ROUTE]` library→detail nav undocumented (grid cards no handler) · med
- `[GAP-ROUTE]` non-libro cards (Gloomhaven/Catan) dead-end; only Eldoria has detail · med
- `[GAP-ENTITY]` **game identity drift**: Eldoria/Side Room vs Nanolith/Side Room vs Eldoria/Side Room Studios vs Eldoria/Mythic Games · **high**
- `[GAP-STATE]` library has no loading/offline state · low
- `[GAP-ROUTE]` empty-state "Sfoglia catalogo" + bottom-nav Chat/Sessioni/Profilo absent (bottom-nav violates #20) · med
- `[GAP-CTA]` detail AI Chat/Toolbox/Toolkit tabs no panels (wired to onboarding/outside bundle) · med
- `[GAP-CTA]` detail error/not-found CTAs → sp4-* outside bundle · low
- `[GAP-ENTITY]` KB figures conflict: detail Rules 118p/142 vs onboarding 248p/272/1842 chunks · med
- `[GAP-ENTITY]` session 0 pip — value #15 flips in Turn 2 (watch) · low
- `[GAP-ROUTE]` onboarding ready "Avvia libro game" no handler → setup-wizard · med
- `[GAP-CTA]` onboarding state-A "Carica PDF" → sp4-upload-wizard-extended (outside) · low
- `[GAP-ENTITY]` agent named "Tutor" (A-C) vs "Arbitro Eldoria" (D + pip) — undefined canonical · med
- `[GAP-CTA]` **"Avvia libro game" overloaded** (detail = open gate; ready = create session) · med

### Turn 2 — create campaign session (10 gaps, running 23)
- `[GAP-ENTITY]` **#15 promotion not surfaced** — no setup mockup references a GameNight or the planned→in-progress transition · **high**
- `[GAP-ENTITY]` draft-vs-live ambiguity (#2/#11/#14): "Inizia sessione" (live) vs "campagna persistente" (draft) · med
- `[GAP-ENTITY]` createdAt (#11) — no "Ora di inizio" shown/derived · low
- `[GAP-CTA]` every wizard CTA → game-onboarding.html (dead-ends) · med
- `[GAP-ENTITY]` guest model — all "guest", no User-linked vs free-guest distinction (#3) · med
- `[GAP-CTA]` setup-chat Storybook/Toolbox tabs no panels · low
- `[GAP-CTA]` citation chips don't jump (no KB-viewer target) · med
- `[GAP-CTA]` low-conf "Rules KB" → sp4-kb-hub; out-of-ctx "Cambia gioco/Cerca agente" nowhere · low
- `[GAP-ENTITY]` chat route .../play/{campaignId}?tab=chat — campaignId fictional, no real session persisted (fixture) · high

### Turn 3 — play (10 gaps, running 33)
- `[GAP-ENTITY]` **#10 max-1-live (HIGH)** — no mockup indicates liveness or guards a 2nd concurrent live; demo added LIVE badge + blocked modal ("puoi averne solo una") · **high**
- `[GAP-ENTITY]` **#14 "Ora di inizio" derived** — no play mockup shows it; demo added read-only "▶ Ora di inizio {startedAt} · derivata" chip · med
- `[GAP-ENTITY]` **cross-mockup identity drift (HIGH for build)** — play-session.html=Eldoria §289, .jsx=Tainted Grail/Avalon §214, translate=Runa di Ardenel §147, encounter=§218; unified under Eldoria · **high**
- `[GAP-CTA]` play Story/Encounter/dice CTAs → game-onboarding.html (demo-nav); rewired · med
- `[GAP-CTA]` translate "Traduci"/reader/nav CTAs → other bundle files; low-conf <0.5 → error-states outside bundle; rewired · med
- `[GAP-CTA]` encounter "Risolvi/Glossario/manuale" CTAs → play-session/glossary-editor files; rewired (Encounter Book = ephemeral, never cached per copyright §9.1) · low

### Turn 4 — close (8 gaps, running 41)
- `[GAP-ENTITY]` **#8 GameNight transition (HIGH)** — no session-end mockup references the GameNight; demo added "Serata da Marco" strip: last live close moves in-progress→completed (Completata/Abbandona terminal; Archivia stays resumable) · **high**
- `[GAP-ENTITY]` close-model reconciliation — mockup has 4 parallel outcome states (paused/victory/defeat/cancelled) vs .jsx 3-option dialog (done/archive/abandon); reconciled to 3-way selector + defeat as dock-only branch · med
- `[GAP-ENTITY]` **resume semantics (#11/#14)** — ▶ Riprendi: new Session w/ fresh startedAt vs draft reactivation undefined; modeled as new live Session (fresh startedAt, campaign createdAt unchanged, GameNight re-promoted) — inferred · **high**
- `[GAP-CTA]` session-end CTAs (Riprendi/Nuova campagna/checkpoint) → other bundle files; rewired · med
- `[GAP-CTA]` resume-picker CTAs → other bundle files; rewired · med
- `[GAP-ENTITY]` empty/first-time + tutorial states still carry "Press Start"/"24 pagine" copy — inconsistent w/ FIX-2; rendered generically · med

### Turn 5 — remaining screens (final, running 49)
- `[GAP-ENTITY]` glossary `GlossaryEntry.contexts[]` requires backend schema migration (mockup says so); single-context today · med
- `[GAP-ENTITY]` glossary source uses "Press Start + Rules" book names → rendered generic (FIX-2) · low
- `[GAP-STATE]` quota scope "10/giorno free tier" but every state shows 50/50 — number conflict · med
- `[GAP-CTA]` quota step/device/theme controls → play-session.html (gallery nav); payment visual-only (no Stripe) · low
- `[GAP-STATE]` error-states header "10 stati" but only 9 (A–I) defined; 10th (J) referenced, never built · med
- `[GAP-CTA]` error recovery CTAs → absolute /library/... or other bundle files; rewired · low
- `[GAP-ENTITY]` **#1/#15 GameNight aggregate never rendered (HIGH)** — storyboard embeds 10 per-session mockups but never draws the owning GameNight; demo added "Serata da Marco · GameNight" 1→N + planned→in-progress (pip 0→1) + →completed · **high**
- `[GAP-ENTITY]` house-rule AgentMemory honoring — mockup shows rules + counts but no in-chat proof the agent obeys; surfaced "🤖 Agente onora" chip + AgentMemory note · med
- `[GAP-ENTITY]` house-rule source data is Tainted Grail + ISS Vanguard; re-anchored to Eldoria (identity drift) · med

## Section 2 — Top priorities (fix first)

1. **Render the GameNight aggregate end-to-end (#1/#15/#8/#10)** — the spine of Step A, absent from every mockup.
2. **Lock one canonical game/campaign identity** across all screens (kills ~6 ENTITY gaps at once).
3. **Define resume semantics (#11/#14)**: new Session + fresh startedAt vs draft reactivation.
4. **Resolve every dead-end CTA** to a real in-bundle route (16 CTA gaps).
5. **Reconcile the GameBook model (1..N)** into onboarding + detail + glossary backends.

## Section 3 — Domain model emerged / confirmed

- **GameNight lifecycle (#1/#15/#8/#10)** is the single biggest gap cluster: no mockup renders the owning GameNight aggregate. The demo design-injected it everywhere (setup confirm, play LIVE badge + max-1-live guard, session-end transition, storyboard 1→N timeline).
- **GameBook is 1..N (0-valid)** not a fixed Press Start + Rules pair — confirmed against the shipped `GameBook` aggregate. FIX 2 generalized the onboarding/detail/chat to the real model (see §Fixes). The mockups regressed to the deleted pre-refactor anti-pattern.
- **Session timestamps** (#11 createdAt / #14 startedAt-derived "Ora di inizio") + **resume semantics** are under-specified in the mockups; the demo modeled them and flagged the inference.
- **Player identity** (#3) User-linked vs free-guest collapsed in the mockups.

## Section 4 — Open tensions

- **Resume = new Session vs draft reactivation?** (#11/#14) — demo inferred "new live Session, fresh startedAt"; needs product ratification.
- **Close model**: 4 parallel outcome states (mockup) vs 3-option dialog (.jsx) — which is canonical?
- **Canonical campaign identity** across the bundle (Eldoria vs Tainted Grail vs Nanolith vs Runa di Ardenel) — pick one.
- **GlossaryEntry.contexts[]** multi-context — needs a backend schema migration before the editor's full UI is real.
- **GameBook role capture**: full multi-select at upload vs RoleClassifier auto-classify + UI confirm (Lore has no classifier yet).

## Section 5 — Demo statistics

- **Turns**: 5/5 (discover&pick · create-session · play · close · remaining). **Routes**: 15 (all bundle screens) + dock state switcher.
- **Total gaps: 49** — by category: **ROUTE 9 · STATE 4 · CTA 16 · ENTITY 19 · TOKEN 0**.
- **By severity**: **HIGH 6** (#10 max-1-live · #8 transition · #1/#15 GameNight unrendered · #11/#14 resume/startedAt · cross-mockup identity drift · GameBook 2-PDF vs 1..N) · **MEDIUM ~17** · **LOW ~26**.
- **TOKEN 0** — `tokens.css` internally consistent; the design system held up. Gaps are in flows/entities, not styling.
- **Diff vs 2026-06-04 baseline (38 gaps): +11 net (49 vs 38)** — additional coverage, **no regressions**, no baseline gap retired silently. New gaps concentrate where the baseline under-counted: GameNight lifecycle trio (#1/#15/#8) not itemized before; GameBook 1..N mismatch (FIX-2) new; interactive build exposed the full dead-end-CTA set a static gallery review misses. Category shift: ENTITY grew most; TOKEN stayed 0.

## Fixes applied to the prototype during the run (operator-driven)

- **FIX 1 — chip-row wrap**: Libreria filter chips `flex-wrap:wrap` (no overflow scroll, no clipping). (First shipped as hidden-scrollbar `.no-sb`, then changed to wrap per design-owner.)
- **FIX 2 — GameBook 1..N generalization** (resolves the multi-book ENTITY gap in the prototype): onboarding replaced the rigid 2-slot "Press Start + Rules" with 1 required "Manuale/Regolamento" slot + dynamic "+ Aggiungi libro" 0..N list (editable name, role multi-select Tutorial/RulesReference/Narrative/Encounter/Lore/Setup, "fisico" toggle); generic counters; graceful 0-books/all-physical states; de-hardcoded across detail KB sidebar, agent scope ("Agente Tutor"), chat citations — single source `onbBooks`. Backed by spec-panel analysis (`docs/superpowers` workflow, 2026-06-30) against the shipped `GameBook` aggregate.

## Next steps (handoff — not yet done)

1. **Export the handoff** from claude.ai/design: topbar canvas → Export → Handoff bundle → `.zip` → extract into `claude-design-handoff/2026-06-30-sp6/` + write its snapshot README + add a row to `claude-design-handoff/README.md`.
2. Open the follow-up issues for the 6 HIGH ENTITY gaps if not already tracked (GameNight aggregate rendering is the umbrella).
3. Append any newly-surfaced invariants to `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md` (appendix).
4. Close #1888 once the handoff + this report are committed.
