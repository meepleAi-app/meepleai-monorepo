# V2 Design Migration — Specification

**Data**: 2026-04-26 (revisione 2026-05-04 — applicazione P0 spec-panel)
**Autore**: Claude (sc:spec-panel multi-expert analysis)
**Scope**: Migrazione frontend `apps/web/` al design system v2 usando i mockup SP3 + SP4 wave 1+2+3+4 come fonte di verità.
**Status**: APPROVED (utente sign-off 2026-04-26 + amendment 2026-05-04 post Wave C.1 fail RCA)

---

## 1. Context & Decisioni utente

### 1.1 Stato attuale (ground truth verificata 2026-04-26)

| Asset | Valore |
|-------|--------|
| v2 primitive shipped | 21 component directories, 62 file `.tsx/.ts` in `apps/web/src/components/ui/v2/` |
| v2 call-site adoption | 142 occorrenze MeepleCard/ExtraMeepleCard in `app/` |
| Schermate già v2 (10/25) | auth, dashboard, games, library, players, sessions, notifications, onboarding, settings |
| Schermate NON v2 (~15) | agents, session-live, session-summary, KB, toolkit, game-nights, discover, admin pages |
| Mockup SP4 disponibili | 8 route (wave 1+2 mergiate in PR #569 + #570) |
| Mockup SP4 wave 3 mancanti | 5 (Player/Toolkit/KB/GameNights/Discover) |
| Mockup SP5 admin mancanti | 19 (brief esiste, non prodotti) |
| Component v2 da implementare (stimato) | ~46 nuovi (vs 23 esistenti) |
| Token system | `tokens.css` produzione: 9 entity colors + 4 semantic aliases ✅ |
| Helper mancante | `entityHsl(entity, alpha?)` non esiste in `lib/color-utils.ts` |
| Visual regression baseline | ❌ Non configurato |
| Bundle size baseline | 12_877_170 bytes (JS-only sum chunks) |

### 1.2 Decisioni architettoniche (utente, 2026-04-26 + amendment 2026-05-04)

| # | Decisione | Implicazione |
|---|-----------|-------------|
| 1 | Procediamo con wave 1+2 (8 route) ora, wave 3+SP5 dopo | Phase 1+2 attive, Phase 3 review-gate prima di wave 3 |
| 2 | **Big-Bang per Tier S/M; coexistence flag opzionale per Tier L** *(amended 2026-05-04 post Wave C.1 fail)* | Route con ≥3 hook indipendenti (Tier L) abilitano feature flag NEXT_PUBLIC_<ROUTE>_V2 per rollout incrementale; rollback ridotto a env var toggle invece di git revert |
| 3 | **No budget** per Chromatic/Percy/Argos | Visual regression via **Playwright `toHaveScreenshot()` nativo** (gratis) |
| 4 | Spec dettagliata prima di aprire issue | Questo documento |
| 5 | **Tier classification obbligatoria** *(added 2026-05-04)* | Ogni route in matrice ha colonna Tier (S/M/L); Tier L richiede Phase 0.5 sub-hook contract pre-implementation |

### 1.3 Implicazioni della decisione "Big-Bang Tier-aware"

⚠️ **Risk acuto** (Nygard): blast radius parziale per Tier S/M (single PR), zero blast radius per Tier L (env flag toggle). Mitigazioni obbligatorie:

1. **Visual regression baseline GROUND-ZERO** prima di qualunque migrazione page-level
2. **Smoke E2E happy path** per ogni page migrata
3. **Bundle delta budget** stretto: max +50 KB per PR Tier S, max +80 KB Tier M, max +120 KB Tier L (incl. legacy coexistence overhead)
4. **Rollback documentato**:
   - Tier S/M: `git revert <sha>` (testato pre-merge)
   - Tier L: env var toggle `NEXT_PUBLIC_<ROUTE>_V2=0` (no deploy required)
5. **Squash merge** sempre (un commit = un revert / un toggle)

App non in produzione → blast radius limitato a dev/staging → rischio accettabile, ma test infra è non-negoziabile.

### 1.4 Spec-panel review log

| Data | Trigger | Amendments |
|------|---------|-----------|
| 2026-04-26 | Initial sign-off | Spec approved, 46 component scope wave 1+2 |
| 2026-04-30 | Wave B.2 spec-panel review | Matrix update −1 (`/agents` not master-detail) |
| 2026-05-03 | Wave 3+4 mockups landed (PR #640) | Scope grew 45 → 80 component |
| 2026-05-04 | **Spec-panel critique post Wave C.1 fail (PR #697)** | **P0 applied**: Tier classification (1.2 #5), coexistence flag Tier L (1.2 #2 amended), Phase 0.5 sub-hook contract (sez. 3.4), test mix ratio 60/30/10 (sez. 4.1), bundle budget re-baseline (sez. 8) |

---

## 2. Goals & Non-Goals

### 2.1 Goals

- ✅ Sostituire 100% delle 8 schermate wave 1+2 con implementazione v2 pixel-perfect rispetto ai mockup
- ✅ Rimuovere il codice legacy delle 8 schermate (no `@deprecated`, no shadow routes)
- ✅ Implementare i ~46 component v2 mancanti emersi dai mockup wave 1+2
- ✅ Stabilire visual regression baseline via Playwright per prevenire drift token
- ✅ Aggiungere helper `entityHsl()` mancante in `lib/color-utils.ts`
- ✅ Mantenere bundle size totale entro +500 KB sull'intera migrazione (delta budget per PR: +50 KB max)
- ✅ Token compliance 100% (zero hex hardcoded — tutti via CSS vars / `entityHsl()`)
- ✅ Accessibility WCAG AA: tutti i `role="dialog"`, `aria-modal`, `prefers-reduced-motion`, `role="tabpanel"` presenti

### 2.2 Non-Goals

- ❌ Wave 3 (Player/Toolkit/KB/GameNights/Discover) — fuori scope, riapre dopo Phase 3 review
- ❌ SP5 admin (19 mockup) — fuori scope
- ❌ Mobile session-live/session-summary — i mockup wave 2 sono desktop-only, mobile è SP5 scope
- ❌ Setup Storybook stories per nuovi component — fuori scope (testing via Playwright + Vitest)
- ❌ Performance optimization oltre il bundle delta budget — separato

---

## 3. Foundation — Phase 0 (PR #0a + #0b + #0c)

Pre-requisito non-negoziabile prima di Phase 1. Tre PR sequenziali, parent branch `frontend-dev`.

### 3.1 PR #0a — Visual Regression Baseline (Playwright + Mockups Claude Design)

**Obiettivo**: catturare snapshot visivi dei **mockup Claude Design** (`admin-mockups/design_files/`) come **design contract**. Ogni page migrata in Phase 1+2 dovrà matchare il proprio snapshot di mockup pixel-perfect (entro threshold).

**Decisione utente (2026-04-26)**: la baseline = mockup, non lo stato legacy. I mockup sono il target, non il punto di partenza.

**Strategia**:
- I mockup sono HTML standalone (React UMD + Babel standalone + `tokens.css` + `components.css` + `data.js`)
- Static file server (`serve` package) espone `admin-mockups/design_files/` su porta dedicata
- Playwright naviga a `http://localhost:PORT/sp4-X.html`, attende fonts + React hydration, cattura snapshot
- Snapshots committati in repo come baseline canoniche
- In Phase 1+2: ogni PR migrazione include test che renderizza l'impl con dati equivalenti a `data.js` e confronta con baseline mockup → diff approvato via review umana

**Deliverables**:
- `apps/web/playwright.config.ts`: nuovo project `mockup-baseline`
  - `testDir: './e2e/visual-mockups'`
  - `use.baseURL: process.env.MOCKUP_BASE_URL ?? 'http://localhost:5174'`
  - `toHaveScreenshot` config:
    - `maxDiffPixelRatio: 0.001` (0.1%)
    - `threshold: 0.2`
    - `animations: 'disabled'`
    - `fullPage: true`
  - Viewports: `375x812` (mobile) + `1440x900` (desktop)
- `apps/web/scripts/serve-mockups.cjs`: helper Node che serve `admin-mockups/design_files/` su 5174 (dev + CI)
- `apps/web/e2e/visual-mockups/baseline.spec.ts`: enumera mockup da una `MOCKUP_REGISTRY` esplicita
  - **Wave 1+2 (8 mockup × 2 viewport = 16 snapshot)**: `sp4-games-index`, `sp4-game-detail`, `sp4-agents-index`, `sp4-agent-detail`, `sp4-library-desktop`, `sp4-sessions-index`, `sp4-session-live`, `sp4-session-summary`
  - **SP3 secondary (5 mockup × 2 = 10)**: `sp3-join`, `sp3-shared-games`, `sp3-shared-game-detail`, `sp3-accept-invite`, `sp3-faq-enhanced`
  - **Core stable (6 mockup × 2 = 12)**: `auth-flow`, `mobile-app`, `public`, `settings`, `notifications`, `onboarding`
  - **Light + dark theme**: per ora solo light (DoD wave 1+2 include dark validation post-impl, snapshot dark = nice-to-have non blocking)
  - Totale: ~38 snapshot iniziali
- `apps/web/e2e/visual-mockups/__snapshots__/`: directory committata
- CI workflow: nuovo job `visual-regression-mockups` in `.github/workflows/web-tests.yml` (o nuovo file)
- `docs/frontend/visual-regression.md`: documenta workflow update snapshot, threshold rationale, comparison Phase 1+2 impl-vs-mockup
- Helper script `apps/web/package.json` scripts: `mockups:serve`, `test:visual:mockups`, `test:visual:mockups:update`

**Definition of Done**:
- [ ] ~38 snapshot mockup wave 1+2 + SP3 + core generati e committati
- [ ] Static server mockup wired in playwright `webServer` config (auto-start in CI)
- [ ] CI verde su `main-dev`
- [ ] Doc `docs/frontend/visual-regression.md` con workflow Phase 1+2
- [ ] Bundle baseline `bundle-size-baseline.json` invariato (12_877_170 — no impatto su bundle prod)

**Effort**: 3-5 giorni · **Owner pattern**: Crispin (testing strategy)

### 3.2 PR #0b — Helper `entityHsl()` + Token Audit

**Obiettivo**: aggiungere il helper mancante per styling entity-colored e verificare sync token CSS vars tra mockup e produzione.

**Deliverables**:
- `apps/web/src/lib/color-utils.ts`:
  ```ts
  // Aggiungi
  export function entityHsl(entity: EntityType, alpha?: number): string {
    const colorVar = `--c-${entity}`; // game, player, session, agent, kb, chat, event, toolkit, tool
    return alpha !== undefined
      ? `hsl(var(${colorVar}) / ${alpha})`
      : `hsl(var(${colorVar}))`;
  }
  ```
- `apps/web/src/lib/color-utils.test.ts`: 9 entity × 2 modi (con/senza alpha) = 18 test
- Audit script `scripts/audit-tokens.ts`: confronta `apps/web/src/styles/design-tokens.css` con `admin-mockups/design_files/tokens.css`. Output diff in `docs/frontend/token-audit-2026-04-26.md`. Fix discrepanze (probabili: `--c-success/warning/danger/info` semantic aliases mancanti)
- Codemod `scripts/codemod-hex-to-entityhsl.ts`: trova hex hardcoded `hsl(38,92%,50%)` style nel codebase v2 esistente e li sostituisce con `entityHsl()` calls (dry-run mode default)

**Definition of Done**:
- [ ] `entityHsl()` exportata, 18 test passanti
- [ ] Audit token completato, discrepanze fixate
- [ ] Codemod applicato (run + commit) su component v2 esistenti
- [ ] CI verde

**Effort**: 1-2 giorni · **Owner pattern**: Fowler (refactoring)

### 3.3 PR #0c — Migration Contract Matrix

**Obiettivo**: tracciabilità esplicita mockup ↔ component ↔ route per i 46 component da implementare.

**Deliverables**:
- `docs/frontend/v2-migration-matrix.md`: tabella markdown con colonne:
  - Mockup file (es. `sp4-game-detail.jsx`)
  - Component name (es. `GameDetailHero`, `GameDetailTabs`, ecc.)
  - Component path target (es. `apps/web/src/components/v2/game-detail/GameDetailHero.tsx`)
  - Route target (es. `/games/[id]`)
  - Status (`pending` / `in-progress` / `done`)
  - PR ref (es. `#601`)
  - Acceptance criteria (es. `viewport 1440 + token compliance + role=tabpanel su tabs body`)
- 46 stub component creati come placeholder:
  - File: `apps/web/src/components/v2/<feature>/<Component>.tsx`
  - Body: `// TODO: implement per admin-mockups/design_files/sp4-X.jsx` + export tipo + return null
  - Permette di importarli mentre la migrazione page-level avanza
- `docs/frontend/v2-migration-matrix.md` referenziato da `CLAUDE.md` sezione Architecture

**Definition of Done**:
- [ ] Matrice 46 entries completa
- [ ] 46 stub committati, typecheck verde
- [ ] CLAUDE.md aggiornato con link alla matrice

**Effort**: 2-3 giorni · **Owner pattern**: Wiegers (requirements traceability)

### 3.4 Phase 0.5 — Sub-hook Contract (per Tier L routes only)

**Aggiunto 2026-05-04** post-RCA Wave C.1 fail (PR #697 closed). Phase 0.5 è **obbligatoria** prima del 5-commit TDD pattern per route classificate Tier L. Per Tier S/M il pattern Wave B (single-shot subagent dispatch) resta sufficiente.

**Tier classification**:

| Tier | Hook count | FSM complexity | Esempi route |
|------|-----------|----------------|--------------|
| **S** | 1 hook | 5-state lineare (`default \| loading \| empty \| filtered-empty \| error`) | `/games?tab=library`, `/agents`, `/library`, `/players` |
| **M** | 2 hook indipendenti | 5-state per hook + composition | `/sessions`, `/players/[id]`, `/toolkits/[id]`, `/kb/[id]` |
| **L** | ≥3 hook indipendenti / cross-resource | Cartesian FSM (≥16 celle teoriche) | `/games/[id]`, `/agents/[id]`, `/sessions/[id]/live`, `/discover`, `/game-nights` |

**Root cause Wave C.1 fail**: il dispatch subagent single-shot per `/games/[id]` non aveva contratti espliciti tra orchestrator e sub-hooks. Sintomi:
1. `/api/v1/agents/undefined` calls (sub-hook fired prima che parent risolvesse `gameId`)
2. State machine stuck (16-cell FSM non mappata, casi non-happy non gestiti)
3. A11y fail (`role="tabpanel"` mancante perché component dispatch non aveva contract)

**Phase 0.5 Deliverables (PR doc-only, parent branch `feature/v2-migrate-<page>` o `main-dev` se separato)**:

1. **Hook dependency graph** — `docs/frontend/contracts/<route>-hooks.md`
   ```markdown
   # /games/[id] Hook Contract

   ## Dependency graph
   useGame(id) ──→ useAgentsByGame(gameId) ──→ AgentsList
                ├→ useFaqsByGame(gameId)    ──→ FaqList
                └→ useKbDocsByGame(gameId)  ──→ KbList

   ## Gating contracts
   - useGame.status === 'pending' → mount global skeleton (no children mount)
   - useGame.status === 'error'   → mount error shell (no children mount)
   - useGame.status === 'success' → children hooks abilitati con gameId valido
   - Children query enabled: `enabled: !!gameId && useGame.isSuccess`
   ```

2. **FSM cell matrix** — esplicita ogni cella rilevante (subset 16 celle cartesiane):
   ```markdown
   | useGame | useAgents | UI Behavior |
   |---------|-----------|-------------|
   | loading | (gated)   | Global skeleton, no tab content |
   | error   | (gated)   | Error shell with retry, no tabs visible |
   | success | loading   | Tab "Agents" mostra inline skeleton |
   | success | error     | Tab "Agents" mostra inline error banner (NO shell error) |
   | success | empty     | Tab "Agents" mostra empty state with CTA |
   | success | success   | Tab "Agents" mostra grid |
   ```

3. **Component contract** — props.shape per ogni component sub-tree:
   ```markdown
   ### AgentsList contract
   Props: { gameId: string, queryEnabled: boolean }
   States: { loading, error, empty, success }
   - queryEnabled false → return null (NO render placeholder)
   - queryEnabled true → fetch + render per state
   ```

4. **Storybook stories** (Tier L only) — 1 story per FSM cell rilevante. Senza Storybook → unit test per cella in `__tests__/<Component>.fsm.test.tsx`.

**Definition of Done Phase 0.5**:
- [ ] Hook dependency graph committato
- [ ] FSM cell matrix esplicita per ogni hook (≥6 celle rilevanti documentate)
- [ ] Component contracts documentati per sub-tree
- [ ] Storybook stories OR unit FSM tests per ogni cella
- [ ] Code review approval su contract PRIMA di dispatch implementation subagent

**Effort**: 1-2 giorni per route Tier L · **Owner pattern**: Nygard (failure mode analysis) + Adzic (executable specs)

**Anti-pattern**: dispatchare implementation subagent senza Phase 0.5 per route Tier L. Wave C.1 PR #697 ha esattamente questo anti-pattern come root cause.

---

## 4. Phase 1 — Wave 1 Migration (5 PR, 4 settimane)

Una PR per page, parent branch `frontend-dev`. Ordine raccomandato (low-risk → high-risk):

| # | PR | Mockup source | Route target | Component nuovi (stima) | Effort |
|---|-----|--------------|-------------|------------------------|--------|
| 1 | `feat(v2): migrate /games index` | `sp4-games-index.jsx` | `/games` | ~6 (GamesHero, FiltersDrawer*, ResultsGrid, ...) | 5-7 gg |
| 2 | `feat(v2): migrate /games/[id]` | `sp4-game-detail.jsx` | `/games/[id]` | ~8 (GameDetailHero, GameDetailTabs, ConnectionBar already shipped, ...) | 5-7 gg |
| 3 | `feat(v2): migrate /agents index` | `sp4-agents-index.jsx` | `/agents` | ~5 | 4-6 gg |
| 4 | `feat(v2): migrate /agents/[id]` | `sp4-agent-detail.jsx` | `/agents/[id]` | ~7 (AgentCharacterSheet primitives) | 6-8 gg |
| 5 | `feat(v2): migrate /library desktop` | `sp4-library-desktop.jsx` | `/library` | ~4 (consolida con mobile esistente) | 4-6 gg |

`*FiltersDrawer` = `AdvancedFiltersDrawer` (riusabile, identificato in mockup wave 1) — implementare come prima cosa nel PR #1, riusato in PR #3.

### 4.1 Pattern PR per ogni page

**Branch**: `feature/v2-migrate-<page>` da `main-dev` *(amended 2026-05-04: Wave B precedent ha mostrato `main-dev` parent stable)*

**Pre-implementation gate (Tier L only)**:
- ✅ Phase 0.5 PR mergiata (sez. 3.4) PRIMA di dispatchare implementation
- ✅ Hook dependency graph reviewed
- ✅ FSM cell matrix approvata

**Body PR include**:
1. Mockup source link (`admin-mockups/design_files/sp4-X.jsx`)
2. **Tier classification** (S/M/L) + link a Phase 0.5 contract se Tier L
3. Component nuovi creati (lista con path)
4. Component legacy rimossi (lista esplicita)
5. Visual regression: link allo snapshot updated
6. Bundle delta: output `pnpm size`
7. **Rollback strategy**:
   - Tier S/M: `git revert <commit-sha>` (testato pre-merge)
   - Tier L: env var toggle `NEXT_PUBLIC_<ROUTE>_V2=0` documentato in PR body
8. Code review nit fix dalla review SP4 wave 1+2 applicati:
   - Hex hardcoded `hsl(38,92%,50%)` → `entityHsl('agent')`
   - `role="tabpanel"` sui body dei tab
   - `prefers-reduced-motion` su `mai-shimmer`/`mai-pulse`/animazioni

**CI gates obbligatori**:
- ✅ Typecheck
- ✅ Lint (incl. `no-hardcoded-hex` rule da introdurre)
- ✅ Vitest unit (target 85%+ su component nuovi)
- ✅ Playwright E2E happy path (login → page → element interaction)
- ✅ Playwright visual regression (snapshot updated + reviewed)
- ✅ A11y axe-core (zero violations su WCAG 2.1 AA)
- ✅ Bundle size delta entro budget Tier-aware (sez. 1.3 punto 3)
- ✅ Smoke E2E real-backend *(workflow_dispatch manual gate, post-merge nightly)*
- ✅ GitGuardian (no secret pattern, no fake-UUID)

**Test mix ratio (added 2026-05-04 post Wave C.1 RCA)**:

Per evitare collasso del test budget durante single-shot subagent dispatch, adottare ratio:

| Tier | Unit | Integration | E2E |
|------|------|-------------|-----|
| **S** | 70% | 20% | 10% |
| **M** | 60% | 30% | 10% |
| **L** | 50% | 35% | 15% |

- **Unit**: component-level FSM cells, hook isolation tests
- **Integration**: orchestrator hook contracts via `renderHook` + `MSW` mocks
- **E2E**: 4–5 stati visuali rilevanti (resto coperti da unit/integration)

Per Tier L l'integration tier è critico: deve esercitare la **cartesian FSM matrix** documentata in Phase 0.5. Esempio per `/games/[id]`:
- `useGame.error` × `useAgents.success` → AgentsList non monta
- `useGame.success` × `useAgents.error` → banner inline (NO shell error)
- `useGame.loading` × tutti gli altri loading → single skeleton (no flash)

**Definition of Done page-level**:
- [ ] Page legacy completamente rimossa (file deleted) **— Tier S/M only**
- [ ] Page legacy gated dietro feature flag (Tier L only) — coexistenza temporanea fino smoke nightly green per 7 giorni
- [ ] Page v2 implementata 1:1 con mockup
- [ ] Tier L: Phase 0.5 contract validated post-implementation (FSM cells coperti)
- [ ] Snapshot visual baseline aggiornato + reviewed
- [ ] E2E happy path passa
- [ ] Test mix ratio rispettato per Tier
- [ ] Bundle delta entro budget Tier-aware
- [ ] Matrice migrazione aggiornata (row status `done`)
- [ ] WCAG AA: focus visibile keyboard, role/aria su dialog/tabpanel, axe-core clean

---

## 5. Phase 2 — Wave 2 Migration (3 PR, 3-4 settimane)

Sessions triade. Più complessa (12+8 component nuovi per session-live e session-summary).

| # | PR | Mockup source | Route target | Component nuovi | Effort |
|---|-----|--------------|-------------|----------------|--------|
| 6 | `feat(v2): migrate /sessions index` | `sp4-sessions-index.jsx` | `/sessions` | ~3 (SessionsHero, SessionFilters, ...) | 4-5 gg |
| 7 | `feat(v2): migrate /sessions/[id]/live` | `sp4-session-live.jsx` + parts | `/sessions/[id]/live` | ~12 (LiveTopBar, TurnIndicator, PlayerRosterLive, LiveScoringPanel, ActionLogTimeline, SessionToolsRail, LiveAgentChat, PauseOverlay, EndgameDialog, ConnectionLostBanner, ...) | 10-14 gg |
| 8 | `feat(v2): migrate /sessions/[id]` | `sp4-session-summary.jsx` + parts | `/sessions/[id]` | ~8 (SessionSummaryHero podio, SessionKpiGrid, ScoringBreakdownTable, SessionDiaryTimeline, PhotosGallery, ChatHighlights, SessionShareCard preview, PlayAgainCta) | 8-10 gg |

### 5.1 Considerazioni speciali wave 2

**PR #7 (session-live) — high complexity**:
- Dark default + light validation (mockup ha entrambi)
- 12 component nuovi → suggerito split in 2 sub-PR: foundation (LiveTopBar + TurnIndicator + PlayerRosterLive + LiveScoringPanel + ActionLogTimeline) + interactions (SessionToolsRail + LiveAgentChat + PauseOverlay + EndgameDialog + ConnectionLostBanner)
- WCAG critical: PauseOverlay/EndgameDialog devono avere `role="dialog"` + `aria-modal="true"` + focus trap (gap noto da code review wave 2)
- Real-time: già esiste infra SSE, riuso

**PR #8 (session-summary)**:
- Confetti CSS-only first-load + `prefers-reduced-motion` (già nel mockup)
- ConnectionBar 1:1 prod (max 6 pip incluso event empty se no GameNightSession parent) — già implementato in `apps/web/src/components/ui/data-display/connection-bar/`, riuso
- ShareCard preview-only (export PNG = backend impl, fuori scope)

---

## 6. Phase 3 — Pause & Review Gate (1 settimana)

Prima di committarsi a wave 3 + SP5, audit obbligatorio:

### 6.1 Deliverables review

- `docs/frontend/v2-migration-phase1-2-retro.md`:
  - Lighthouse pre/post (8 pages)
  - Bundle size totale (vs baseline 12.87 MB)
  - Regression count (issues aperte/chiuse durante migrazione)
  - User feedback raccolto (se applicabile)
  - Velocity actual vs estimated (per calibrare wave 3)
- Decisione GO/NO-GO per Phase 4 (wave 3 + SP5)
- Brief Claude Design SP4 wave 3 (5 mockup mancanti) se GO

---

## 7. Risks & Mitigations

| Risk | Probabilità | Impatto | Mitigazione |
|------|------------|--------|-------------|
| Visual regression baseline incompleta → drift token invisibile | Media | Alto | PR #0a obbligatoria, 36 snapshot iniziali, gate CI bloccante |
| Bundle size esplode (46 component nuovi) | Alta | Medio | Budget per PR (+50 KB), gate CI, code review focus su lazy-load |
| Big-Bang rompe schermate non-wave (regressioni cross-page) | Media | Alto | Visual regression baseline include schermate v2 attuali → rileva drift; lint rule `no-hardcoded-hex` previene drift token |
| Wave 2 session-live troppo complessa per una PR | Alta | Medio | Pre-autorizzazione split in 2 sub-PR (foundation + interactions) |
| Component nuovi divergono dal mockup (ambiguità) | Media | Medio | Contract matrix PR #0c esplicita acceptance criteria; visual regression confronta col mockup |
| Code review nit della wave 1+2 dimenticati durante impl | Alta | Medio | Checklist nel template PR (sezione 4.1) |
| WCAG AA gaps noti (role/aria/reduced-motion) | Alta | Alto | DoD page-level esplicito; lint axe-core in CI raccomandato |
| Mockup wave 3 mancanti → stuck dopo Phase 2 | Bassa | Basso | Phase 3 review include brief wave 3 prima di GO |

---

## 8. Success Metrics

**Re-baselined 2026-05-04** post Wave 3+4 mockup landing (scope grew 46 → 80 component).

| Metrica | Target originale (wave 1+2 only) | Target re-baselined (wave 1+2+3+4) |
|---------|---------------------------------|-------------------------------------|
| Schermate migrate | 8/8 wave 1+2 | 17/17 wave 1+2+3+4 (8 wave 1+2 + 5 wave 3 + 4 wave 4) |
| Component v2 implementati | 46/46 | 80/80 |
| Token compliance | 100% (zero hex hardcoded) | 100% (sustained) |
| Visual regression coverage | 36 snapshot iniziali + +1 per PR | 36 + +1 per PR + sentinel fixture per stati FSM |
| **Bundle delta totale** | < +500 KB (ideal < +300 KB) | **< +800 KB (ideal < +500 KB) — incrementato proporzionalmente da 46 → 80 component** |
| Bundle delta per PR | < +50 KB | Tier S < +50 KB, Tier M < +80 KB, Tier L < +120 KB |
| WCAG AA compliance | 100% wave 1+2 | 100% all waves (axe-core 0 violations) |
| Test coverage component nuovi | ≥ 85% | ≥ 85% (sustained) — con mix ratio Tier-aware sez. 4.1 |
| Code review nit residui | 0 | 0 (sustained) |
| Smoke nightly real-backend pass rate | (not specified) | ≥ 95% rolling 7-day window |
| **Time to complete** | 8-10 settimane (wave 1+2) | 14-18 settimane (wave 1+2+3+4 incl. Phase 0.5 overhead Tier L) |

**Bundle ledger aggiornato 2026-05-04**:
- Baseline 2026-04-26: 12_877_170 bytes
- Post Wave B.3 (2026-05-03): ~13_590_000 bytes (delta +713 KB su 5 component done)
- Estrapolazione 80 component (lineare): ~14_100_000 bytes (totale delta +1.2 MB) — **fuori budget originale**
- Mitigazione: code-splitting + lazy-load Tier L route (target delta ridotto ~30%)
- Re-baseline target: 13_700_000 bytes (delta +823 KB sustainable)

---

## 9. Open Questions

1. **Lint rule `no-hardcoded-hex`**: introdurla in PR #0b o post-Phase 0? Raccomandazione: **PR #0b** (parte foundation).
2. **axe-core in CI**: aggiungere come gate o solo report? Raccomandazione: **gate** per page wave 1+2 (DoD esplicito).
3. **Storybook stories per 46 component nuovi**: fuori scope o opzionale per i più riusabili (`AdvancedFiltersDrawer`, `LiveTopBar`)? Raccomandazione: **opzionale**, no gate.
4. **Codemod hex → entityHsl**: dry-run o auto-apply? Raccomandazione: **auto-apply** in PR #0b (file modificati committati nello stesso PR).
5. **Deprecation `navItems` MeepleCard (deadline 2026-07-15 da PR #552)**: completare cleanup durante Phase 1? Raccomandazione: **sì**, opportunistico (touchpoint games-index PR #1).

---

## 10. Sign-off Required

- [ ] Utente approva strategia Big-Bang + no feature flag (decisione 1.2 #2)
- [ ] Utente approva budget bundle +500 KB totale
- [ ] Utente approva sequenza Phase 0 → 1 → 2 → review gate → wave 3
- [ ] Utente conferma: Playwright `toHaveScreenshot` come visual regression (no Chromatic/Argos/Percy per budget)

---

## Appendix A — Component Inventory (Wave 1+2 nuovi)

Estratto dalla matrice migrazione (PR #0c lo formalizza). Dimensione preliminare; refine in PR #0c.

**Wave 1 (~30 component)**:
- `GamesHero`, `GamesFiltersInline` (4-core sticky), `AdvancedFiltersDrawer`, `GamesResultsGrid`, `GamesSortBar`, `GamesEmptyState`
- `GameDetailHero`, `GameDetailTabsAnimated`, `GameDetailKpiCards`, `GameDetailFaqList`, `GameDetailRulesAccordion`, `GameDetailSessionsRail`, `GameDetailAgentsList`, `GameDetailKbDocList`
- `AgentsHero`, `AgentsSidebarList`, `AgentDetailPanel`, `AgentsFiltersStrip`, `EmptyAgents`
- `AgentCharacterSheet`, `PersonaCard`, `SystemPromptViewer`, `KbDocList`, `ChatHistoryTimeline`, `AgentSettingsForm`, `AgentDangerZone`
- `LibraryHeroDesktop`, `LibraryTabs`, `LibraryHybridGrid`, `BulkSelectionBar`, `RecentActivityRail`

**Wave 2 (~20 component)**:
- `SessionsHero`, `SessionsFilters`, `ConnectionChipStripFooter` (max 3 chip)
- `LiveTopBar`, `TurnIndicator`, `PlayerRosterLive`, `LiveScoringPanel`, `ActionLogTimeline`, `SessionToolsRail`, `LiveAgentChat`, `PauseOverlay`, `EndgameDialog`, `ConnectionLostBanner`
- `SessionSummaryHero`, `SessionKpiGrid`, `ScoringBreakdownTable`, `SessionDiaryTimeline`, `PhotosGallery`, `ChatHighlights`, `SessionShareCard`, `PlayAgainCta`

Totale: ~50 (stima alta vs ~46 iniziale; refine in PR #0c).

---

## Appendix B — Riferimenti

- Code review verdicts wave 1+2: PR #569, PR #570 (entrambi APPROVE_WITH_NITS, 0 critical)
- Pattern branch-by-abstraction: PR #549, #552 (ConnectionChip Step 1.6/2)
- Pattern Big-Bang: PR #396 (card-drawer nav redesign)
- Token system: `admin-mockups/design_files/tokens.css` (mockup) ↔ `apps/web/src/styles/design-tokens.css` (prod)
- Brief Claude Design: `admin-mockups/briefs/_common.md`, `SP4-entity-desktop.md`, `SP5-admin-tools.md`
- Spec-panel analysis: questo documento sezione 1-2

---

**Status**: DRAFT — pending utente sign-off section 10.
**Next step post-approval**: aprire issue epic + 3 sub-issue (PR #0a, #0b, #0c).
