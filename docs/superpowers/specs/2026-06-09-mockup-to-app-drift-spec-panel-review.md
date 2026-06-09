# Mockup-to-App Fidelity — Spec-Panel Review

**Data**: 2026-06-09
**Issue umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
**Sub-issue Phase 0**: [#2066](https://github.com/meepleAi-app/meepleai-monorepo/issues/2066)
**Origine**: `/sc:spec-panel` mode critique — 5 esperti (Adzic · Fowler · Wiegers · Crispin · Nygard)
**Trigger**: domanda meta-process utente — *"quando faccio le pagine di mockup hanno una grafica, ma quando le usi per aggiornare la nostra app, ci sono sempre delle piccole differenze. C'è un modo migliore per usare l'output che ho prodotto nei file in `admin-mockups/`?"*

> Questo documento è il **source of truth post-panel** delle decisioni di scope, architettura, effort assunte sull'umbrella #2063. Va letto prima di iniziare qualsiasi PR su sub-issue di Phase 1+. Le 6 DEC del body umbrella sono il riassunto operativo; questo doc è la versione dettagliata con findings per esperto, risk matrix, alternative considerate.

---

## Sezione 1 — Quality Assessment baseline (workflow attuale)

Workflow valutato: "design-to-code via `admin-mockups/design_files/`" come documentato in `admin-mockups/README.md` + `admin-mockups/MOCKUPS_INDEX.md`.

### Scoring multi-dimensionale

| Aspect | Score | Note |
|--------|-------|------|
| Token usage discipline | **7/10** | `tokens.css` canonical OK (DS-15 done), ma bridge legacy `--bg-base`/`--gaming-*`/`--nh-*`/`--e-*` ancora in uso (CLAUDE.md § Token Canonicalization "Deferred decisions") |
| State coverage | **4/10** | Multi-stato non standardizzato — solo `state-matrix.html` come dev-fixture passiva; molti page-mock mostrano solo stato felice |
| Drift detection | **2/10** | Visual gate rimosso 2026-05-20 (false-positive rate); detection solo via audit manuale Playwright MCP ex-post (es. sess. 45 #1974) |
| HTML ↔ JSX twin sync | **5/10** | "Pairing rule" convention OK (MOCKUPS_INDEX.md), ma zero tooling — orfani periodicamente rimossi (es. #2025 cluster 3 JSX Sara) |
| Route → mockup mapping | **5/10** | Mapping textual in MOCKUPS_INDEX.md, no auto-check route → mockup esiste |
| Mockup → codebase traceability | **3/10** | Nessuna annotation nei componenti `apps/web/src/...` verso mockup di provenienza |
| Acceptance criteria misurabili | **2/10** | README dice "Treat them as a contract. Reproduce pixel-perfect; deviate only with intent" — zero threshold quantitativo |
| **Overall workflow maturity** | **4.5/10** | Alto sforzo creativo, basso enforcement automatico |

### Verdetto baseline

Workflow strutturalmente fragile: ogni "porting" richiede inferenza umana dal mockup HTML al codice TSX. Lo sforzo creativo dei mockup (127 file, design system completo, entity-driven navigation pattern) è eccellente, ma il canale di trasmissione al codebase ha bandwidth bassa e nessun arbitro.

---

## Sezione 2 — Decisioni lockate (6 DEC)

### DEC-1 · Single source of truth = Storybook stories

**Driver**: CRIT-3 (Fowler) + CRIT-1 (Adzic).
**Decisione**: scegliere **Opt A — Storybook stories** che USA i componenti reali del codebase, scartare Opt B (HTML canonical + JSX codegen) e Opt C (status quo + gate scoped only).
**Motivazione**: pairing HTML ↔ JSX twin tollera duplicazione strutturale. Codegen Opt B richiede parser HTML→JSX maintained nel tempo e lascia comunque due artefatti. Solo Opt A elimina la classe di problema "mockup ≠ codice" perché mockup E codice convergono nello stesso component tree.
**Impatto**: refactor di 127 file (~13K LOC), introduzione Storybook nel monorepo (~7-9 settimane single FTE). Setup tooling Phase 2.

### DEC-2 · Migration incrementale per-mockup, no big bang

**Driver**: lesson learned da umbrella precedenti (#1023 Stage 1+2+3 incremental cluster, #1895 asse A/B/C/D per-axis).
**Decisione**: ogni mockup migra in PR separata o cluster piccolo (3-5 mockup affini). Big bang vietato.
**Motivazione**: 127 file in 1 PR sarebbe unreviewable e blocking; pattern incrementale ha funzionato su #1023 (3 stage) e #1895 (4 assi). Riduce blast radius.
**Impatto**: Phase 3 splittata in 5 cluster (SP4 core / SP3 / SP6+SP7+nanolith / SP4 sessions / component-mocks). PR media ~3-5 mockup.

### DEC-3 · Dev-fixtures KEEP come HTML standalone

**Driver**: pragmatismo Adzic (fixtures non sono page-mocks).
**Decisione**: `00-hub.html`, `04-design-system.html`, `05-dark-mode.html`, `tokens.css`, `components.css`, `data.js` restano in `admin-mockups/design_files/` post-migration.
**Motivazione**: questi file documentano il **token system** + **theme reference** + **dataset shape** — non sono page-mocks, sono playground/reference. Storybook ha già modo di esporre design system (token addon), ma il playground HTML standalone è utile come ground truth indipendente dal framework. Migration costo > benefit per questi 6 file.
**Impatto**: post-cleanup Phase 4, `admin-mockups/design_files/` contiene solo dev-fixtures + tokens. README aggiornato per puntare a Storybook per page-mocks.

### DEC-4 · Visual gate scoped (non full-page, non globale)

**Driver**: CRIT-7 (Crispin) + lesson learned 2026-05-20 (CLAUDE.md "Visual Gate REMOVED").
**Decisione**: re-introdurre visual diff Playwright + pixelmatch con scope ristretto:
- **10 route critiche** (top-traffic + complex layout: dashboard, library, game-detail, chat, session-live, …)
- **Light theme only** (riduce 50% del flake font/locale)
- **Block-level diff** (selettori specifici, no full-page screenshot)
- **Threshold 5% area** (margine per font hinting + sub-pixel rendering)
- **Dry-run 7gg** prima di diventare blocking gate

**Motivazione**: il gate precedente è stato rimosso per false-positive rate, non per inutilità. Le 4 restrizioni sopra eliminano i tre driver principali del flake (font, locale, mockup-vs-live divergence). Scope a 10 route rende il segnale gestibile e il costo CI marginale.
**Impatto**: Phase 4 DS-17-14. CI deve aggiungere job `visual-gate-scoped` con artifacts diff per debug.

### DEC-5 · DS-17 BLOCKS DS-16 (token bridge removal)

**Driver**: CRIT-9 (Nygard) + CLAUDE.md "Deferred decisions" Tier 1-4.
**Decisione**: DS-17 deve completare Phase 3 (migration sweep) PRIMA che DS-16 possa eseguire codemod di rimozione `token-bridge.css`.
**Motivazione**: Storybook stories migrate useranno nomi token canonici (`--bg`, `--bg-card`, ...). Se DS-16 rimuove bridge prima, le stories migrate funzionano ma i page-mock HTML non ancora migrati si rompono. Sequenza obbligata: stories migrate → tutti i consumer su nomi canonici → DS-16 codemod elimina bridge in safety.
**Impatto**: DS-16 spostata a Phase 5 di DS-17. Issue DS-16 (se aperta) blocked-by #2063.

### DEC-6 · Nessun conflitto con asse-B #1897 (UI shell + DrawerStack)

**Driver**: discovery — primitives già shipped.
**Decisione**: le stories DS-17 consumeranno le primitives v2 esistenti in `apps/web/src/components/ui/v2/` (MainSidebar, Drawer cascade-store, WizardModal, StatePreview, ecc.) senza reimplementarle.
**Motivazione**: asse-B #1897 ha shipped (sess.33 2026-06-05) il pattern UI shell completo. Reimplementare primitives nelle stories sarebbe duplicazione e perdita di sync. Le stories di componenti shell (sidebar, drawer) saranno wrap stories che mostrano variants delle primitives reali.
**Impatto**: Phase 3 sotto-cluster "component-mocks" è semplificato — molti component-mock di asse-B sono già coperti da primitives, le stories saranno render-thin.

---

## Sezione 3 — Findings dettagliati per esperto

### Adzic (lead) — Specification by Example

**🔴 CRIT-1 · Mockup non sono "executable specifications"**
HTML statico = "looks like this", ma non risponde a "how do I know it's correct?". Nessuno scenario Given/When/Then lega mockup→codice. Ogni sviluppatore (o agent AI) inferisce comportamento dalla forma visiva → ambiguità sistemica.

**Recommendation**: trasformare ogni page-mock in living spec con tre artefatti pairing:
1. `sp4-dashboard.html` (visual reference) → diventa `dashboard.stories.tsx` post-DEC-1
2. `sp4-dashboard.spec.md` (contract: tokens, props, stati, edge cases) → embedded in story `parameters`
3. `sp4-dashboard.fixtures.json` (dati che il componente DEVE rendere identico al mockup) → loaded by story

**Impact stimato**: +50% testability, +40% comprehensibility.

**🔴 CRIT-2 · I 5 stati (default/empty/loading/error/sse) sono pattern non enforcement**
Pattern "state matrix" (`state-matrix.html`) esiste ma è dev-fixture passiva. Molti mockup mostrano solo stato felice (es. `sp4-game-detail.html` come 1 stato vs i 4 richiesti dal pattern). Empty/loading/error nascono ex-novo a runtime e divergono.

**Recommendation**: ogni page-mock DEVE pubblicare `n × stati` numerati (`-state-01-default`, `-state-02-empty`, ...). PR di migration blocked senza tutti gli stati documentati. Phase 1 DS-17-3.

**🟡 MAJOR · Fixtures data non condivise tra mockup e app**
`data.js` ha shape leggermente diversa dal backend reale (es. `Game.coverGradient: string` vs `Game.coverUrl: string`). Component prop signatures divergono → quando si fa migration, prop mapping è ad-hoc.

**Recommendation**: Phase 2 DS-17-7 definisce pattern `fixtures.json` che riusa i TypeScript types reali del codebase (`apps/web/src/lib/types/`). Una sola fonte di shape.

---

### Fowler — Architecture & Component Design

**🔴 CRIT-3 · Pairing HTML ↔ JSX twin = duplicated source of truth**
README: *"HTML for browser preview, JSX for codebase clone... two are equivalent"*. **Equivalenti è impossibile da garantire senza tooling**. Stesso bug fix va applicato 2 volte; appena divergono, nessuno sa chi è canonico. Vedi cluster #2025 (3 JSX Sara orfani eliminati perché senza HTML canonical).

**Recommendation**: scegliere single source of truth. **DEC-1 → Opt A Storybook** è la scelta.

**🟡 MAJOR-4 · I componenti del mockup non sono "the real thing"**
`mobile-app.jsx` ha ~870 righe di prototipo che reimplementa `<EntityChip>`, `<Drawer>`, `<ConnectionBar>`, `<BottomBar>`. Il codebase ha già `apps/web/src/components/ui/v2/` (asse-B primitives). Due implementazioni della stessa cosa.

**Recommendation**: estrarre i 4 "non-negotiable" component (EntityChip, EntityPip, Drawer, BottomBar) da `mobile-app.jsx` → consumare le primitives v2 esistenti. Post-migration, `mobile-app.jsx` è solo storia/archive (può essere eliminato dopo Phase 3). Vedi DEC-6.

---

### Wiegers — Requirements Quality

**🔴 CRIT-5 · Mancano "acceptance criteria" misurabili per page-mock**
README: *"Reproduce pixel-perfect; deviate only with intent"*. Cosa significa "pixel-perfect"? Nessun threshold quantitativo (es. ≤3px tolerance, ≤2 colour units delta). Ogni revisore decide cosa è "abbastanza vicino".

**Recommendation**: definition of done per mockup migration tramite template:
```yaml
# apps/web/.storybook/stories/<story>/mockup.fidelity.yml
acceptance:
  visual_diff_max_px: 5
  color_delta_e_max: 3
  tokens_used: exactly tokens.css canonical names (no hardcoded HSL)
  states_covered: [default, empty, loading, error, sse]
  a11y_axe: AA passing
  responsive_breakpoints: [375, 768, 1024, 1440]
```
Phase 1 DS-17-4.

**🟡 MAJOR-6 · Mapping mockup→route in MOCKUPS_INDEX.md non è enforced**
La tabella dice `sp4-game-detail.html → /games/[id], /library/[gameId], /private-games/[id]`. Se aggiungo `/games/[id]/v2`, nessuno sa che dovrebbe usare lo stesso mockup. Mapping è documentazione passiva.

**Recommendation**: ogni componente di route ha frontmatter / annotation:
```tsx
/**
 * @mockup admin-mockups/design_files/sp4-game-detail.html
 * @story stories/sp4/game-detail.stories.tsx
 * @mockup-state state-03-loading
 * @fixtures sp4-game-detail.fixtures.json#loading
 */
export default function GameDetailPage() { ... }
```
Script CI verifica che ogni route ha annotation e il file referenziato esiste. Phase 1 DS-17-1.

---

### Crispin — Testing Strategy

**🔴 CRIT-7 · Visual gate è stato RIMOSSO senza sostituto**
CLAUDE.md: *"Visual Gate REMOVED 2026-05-20 — false-positive rate (locale drift, font flake, mockup-vs-live divergence) outweighed pickup value; replacement = manual designer review on PRs"*. Decisione corretta a suo tempo (gate troppo aggressivo), ma il sostituto è non scalabile → drift inevitabile (sess. 45 ha trovato 30+ findings con audit manuale).

**Recommendation**: re-introduce visual diff focalizzato e robusto. Vedi DEC-4 per scope (10 route critiche, light theme, block-level, threshold 5%, dry-run 7gg). Phase 4 DS-17-14.

**🟡 MAJOR-8 · Mockup non hanno test della "interaction model"**
Drawer physics, connection pips, theme toggle — README descrive comportamento (Section "Interactions & Behavior") ma nessun test verifica "drag handle past 40% threshold closes". Nuove implementation possono divergere silently.

**Recommendation**: ogni interaction non-banale → 1 Playwright E2E spec co-located con story: `dashboard.interactions.spec.ts`. Spec leggibile dal designer (Adzic-style GWT). Phase 3 (per stories che hanno interactions complesse, non tutte).

---

### Nygard — Production Reliability

**🔴 CRIT-9 · Token bridge è anti-corruption layer che ha smesso di essere temporaneo**
CLAUDE.md: *"Legacy v1 names (--bg-base, --gaming-bg-*, --nh-bg-*, --e-*) are still aliased via token-bridge.css... will be removed in DS-16"*. Bridge è "tech debt by design". Finché esiste, mockup e app possono usare nomi diversi che risolvono allo stesso valore → cambiare un token nel mockup non si propaga (silent decoupling).

**Recommendation** (3 step):
1. **Immediate (Phase 1 DS-17-2)**: lint blocks new uses di `--bg-base`, `--gaming-*`, `--nh-*`, `--e-*` nel codebase E nei mockup (estendi `lint:tokens` a `admin-mockups/`).
2. **DS-16 codemod**: migrare consumer rimanenti.
3. **Phase 5 DS-17-17**: rimuovi bridge → un solo nome per token.

Vedi DEC-5 per sequencing (DS-17 BLOCKS DS-16).

**🟡 MAJOR-10 · Mancano "monitoring" sul drift**
Drift scoperto solo quando audit manuale gira (es. sess. 45). Tra audit, drift cresce silently.

**Recommendation**: weekly CI job `weekly-mockup-drift.yml`:
- Renderizza ogni route principale con dataset fixture
- Confronta DOM structure (non pixel) col mockup HTML → report markdown
- Auto-issue se trova mismatch oltre threshold

Phase 4 DS-17-15.

---

## Sezione 4 — Risk matrix

Probability × Impact, scala 1-5 ciascuno. Score = P×I (1-25). Mitigation per ogni rischio ≥9.

| Rischio | P | I | Score | Mitigation |
|---------|---|---|-------|------------|
| **R1** · Storybook migration stalls per scope creep (Phase 3 raddoppia) | 3 | 5 | **15** | Phase 3 incremental SP-by-SP, ogni cluster auto-sufficient (DS-17-9...DS-17-13). Possibile pause umbrella dopo qualsiasi cluster completo |
| **R2** · Visual gate riintrodotto torna a falsi positivi (CRIT-7 ripeted) | 4 | 4 | **16** | DEC-4 scope drastico (10 route, light-only, block-level, 5% threshold). Dry-run 7gg prima di blocking. Manual escape-hatch label `skip-visual-gate` per PR di test/refactor |
| **R3** · Token bridge removal (DS-16) breaks consumer non migrato | 3 | 4 | **12** | DEC-5 sequencing: DS-17 BLOCKS DS-16. lint:tokens esteso a admin-mockups/ Phase 1 (DS-17-2) → blocca nuovi usi legacy in input |
| **R4** · Developer adoption gap (continui ad usare HTML mockup invece di Storybook) | 4 | 3 | **12** | Freeze policy umbrella body + CLAUDE.md "Active Freezes" section. Block PR check su `admin-mockups/design_files/*.html` se non dev-fixture |
| **R5** · Storybook tooling lock-in (Chromatic vendor cost $149/mo) | 2 | 3 | **6** | DEC tooling separata Phase 2 (open question). Playwright snapshot come fallback free. Decisione differita post-pilot 3 mockup |
| **R6** · Effort sottostima (~7-9 settimane → realistic ~12-14 settimane) | 3 | 3 | **9** | Phase split granulare (5 phase × 18 sub-issue). Acceptance criteria umbrella check ogni Phase. Possibile early-close Phase 3 partial se ROI saturato (80% mockup migrati = sufficient) |
| **R7** · Designer non-buy-in (perde controllo HTML standalone) | 2 | 4 | **8** | DEC-3 KEEP dev-fixtures + design-system playground. Designer continua a poter editare playground senza Storybook |

---

## Sezione 5 — Alternative considerate

### Opt A · Storybook stories (**CHOSEN**)

**Pro**:
- Elimina struttura del problema: mockup E codice convergono nel medesimo component tree
- Designer vede l'app vera con dati fixture controllati, non un prototipo parallelo
- Riusa primitives v2 già shipped (asse-B #1897)
- Pattern industry-standard, on-boarding contributor più facile
- Storybook 8.x ha addon Vitest integration, side-by-side con test esistenti

**Contro**:
- Refactor 127 file (~13K LOC mockup HTML/JSX)
- Setup tooling (~1 settimana Phase 2)
- Decisione tooling visual snapshot (Chromatic vs Playwright vs Percy) richiede ulteriore valutazione

**Effort**: ~7-9 settimane single FTE; ~4-5 settimane con 2 dev paralleli su Phase 2-3.

### Opt B · HTML canonical + JSX codegen (rejected)

**Pro**:
- Mantiene HTML preview standalone (designer-friendly out of the box)
- Smaller initial refactor (script HTML→JSX vs reimplementation)

**Contro**:
- Tooling complesso da costruire e maintenire (parser HTML → JSX AST)
- Pairing rule resta strutturalmente: HTML canonical + JSX generato
- Cambiare i componenti reali del codebase richiede ancora rigenerazione + verifica
- Cosa fa il codegen quando HTML usa pattern non esprimibile in JSX (e.g. inline `<style>` o `<script>`)?
- Pattern non-standard, on-boarding contributor difficile
- **Razionale rejection**: tollera ancora la duplicazione strutturale; non risolve il root cause Fowler CRIT-3

**Effort stimato**: ~4-5 settimane setup + maintenance ongoing.

### Opt C · Status quo + visual gate scoped (rejected)

**Pro**:
- Nessuna migration, low effort upfront (~1 settimana)
- Mantiene workflow esistente che il designer conosce

**Contro**:
- **Sintomatico**, non risolve root cause
- Drift continua silently tra audit (vedi sess. 45 30+ findings ex-post)
- Accumulo debito tecnico continuo
- Visual gate gestisce SOLO drift visibile, non drift semantico (componenti reimplementati, fixtures inconsistenti, ecc.)
- **Razionale rejection**: 3 root causes identificate (CRIT-1/3/9) sono strutturali, gate visivo ne mitiga 1/3

**Effort**: ~1 settimana setup gate, +N giorni continui di audit/polish PR ricorrenti.

---

## Sezione 6 — Open questions per Phase 2

Decisioni differite a Phase 2 DS-17-8 (CI job + visual snapshot tooling):

### Q1 · Visual snapshot tool: Chromatic vs Playwright snapshot vs Percy

| Tool | Cost | Pro | Contro |
|------|------|-----|--------|
| **Chromatic** | $149/mo project plan | Best Storybook integration, UI review, baseline management auto | Vendor lock-in, costo ricorrente |
| **Playwright snapshot** | Free | Integra con CI esistente, no vendor, scope custom | No UI review, baseline in git LFS |
| **Percy** | $0 free tier (5k snap/mo) | UI review, multi-browser | Separate da Playwright, free tier limitato |

**Raccomandazione panel**: pilot Playwright snapshot in Phase 2 (DS-17-8) con 3 mockup. Se review UX problematica, valutare Chromatic in Phase 4.

### Q2 · Fixtures format

Opzioni:
- JSON file (`fixtures/dashboard.json`)
- TypeScript const (`fixtures/dashboard.ts`)
- MSW handlers (`mocks/handlers/dashboard.ts`)
- Faker.js generator (deterministic seed)

**Raccomandazione panel**: TypeScript const (riusa types reali via `import type`). MSW handlers solo per stories che testano network failure states.

### Q3 · Story granularity: 1 story = 1 mockup, o 1 story = 1 stato?

Opzioni:
- **A**: 1 story file per mockup, multi-states via `args` toggle
- **B**: 1 story file per stato (es. `dashboard-default.stories`, `dashboard-loading.stories`)

**Raccomandazione panel**: Opt A — 1 story file per mockup, ogni stato è export named (`export const Default`, `export const Loading`, ...). Storybook 8 sidebar mostra nested tree naturalmente.

### Q4 · Path canonico stories

Opzioni:
- **A**: `apps/web/.storybook/stories/sp4/dashboard.stories.tsx` (centralizzato)
- **B**: co-locate `apps/web/src/components/features/dashboard/dashboard.stories.tsx` (locale al componente)

**Raccomandazione panel**: Opt B — co-locate. Discovery più naturale ("vado al componente, vedo le sue stories"). `apps/web/.storybook/` solo per config + global decorators.

---

## Sezione 7 — References + change log

### Reference docs

- **Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) [Umbrella] DS-17 — Mockup-to-App Fidelity
- **Sub-issue Phase 0**: [#2066](https://github.com/meepleAi-app/meepleai-monorepo/issues/2066) [DS-17 Phase 0] Pubblica spec doc + decision lock
- **Antecedente DS**: [#1023](https://github.com/meepleAi-app/meepleai-monorepo/issues/1023) [Umbrella] Design System De-versioning & Mockup-Faithful Convergence (CLOSED 2026-05-18)
- **Dipendenza forward**: DS-16 — Token bridge removal (CLAUDE.md § Token Canonicalization "Deferred decisions")
- **Audit precursore**: [#1974](https://github.com/meepleAi-app/meepleai-monorepo/issues/1974) [Umbrella] SP4 audit reskin manuale (CLOSED 2026-06-08, 20 PR polish shipped sess. 45)
- **Coord con altre umbrella attive**:
  - [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895) Claude Design alignment (asse A/B/C/D) — primitives già shipped in `apps/web/src/components/ui/v2/`
  - [#1897](https://github.com/meepleAi-app/meepleai-monorepo/issues/1897) asse B (UI shell + DrawerStack) — primitives shipped sess.33

### Mockup source of truth attuale

- `admin-mockups/MOCKUPS_INDEX.md` — 127 mockup mapping (67 page-mock + 48 component-mock + 12 dev-fixture)
- `admin-mockups/README.md` — design system spec, entity-driven navigation, 9 entity colors
- `admin-mockups/design_files/tokens.css` — source of truth design tokens (linked in `apps/web/src/styles/design-tokens-canonical.css`)

### CLAUDE.md sezioni rilevanti

- "🔒 Active Freezes" → Token Canonicalization (DS-1...DS-16 history)
- "Card Components" → MeepleCard precedent
- "V2 Migration Components" → asse-B primitives reference
- "Visual Gate REMOVED 2026-05-20" → lesson learned per DEC-4

### Pattern memoria

- `feedback_p181_spec-panel-on-fresh-umbrella` — spec-panel su umbrella esistente
- **NEW P234** spec-panel-umbrella-creation-from-meta-question — questo umbrella (umbrella ex novo da meta-question)

### Change log

| Data | Versione | Autore | Cambiamento |
|------|----------|--------|-------------|
| 2026-06-09 | v1.0 | Claude (sess.46h) | Creazione iniziale post-panel critique (5 esperti). 6 DEC firmate. 10 findings (5 CRIT + 5 MAJOR). 7 rischi mapped. 4 open questions per Phase 2. |

---

🤖 Generated with [Claude Code](https://claude.com/claude-code) — spec-panel critique 2026-06-09 (Adzic + Fowler + Wiegers + Crispin + Nygard)
