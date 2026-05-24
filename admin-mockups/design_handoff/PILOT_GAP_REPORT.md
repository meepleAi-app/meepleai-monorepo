# Pilot Gap Report — `/games/[id]` (sp4-game-detail.jsx ↔ brownfield Wave C.1)

> Generato dallo **Step 7** di `QUICK_START.md` — riadattato come **conformity check** post-scoperta brownfield esistente (Wave C.1, Issue #581).
> Data: 2026-05-24 · Branch: `feature/design-handoff-setup` · Scope: solo lettura (zero modifiche).
> Aggiornamento 2026-05-24 PM: **6/6 decisioni risolte** via read mirati — vedi § 3.5.

## ⚠️ DIVERGENZA AGGIUNTIVA scoperta in § 3.5: il codebase ha EVOLUTO il mockup

Il `GameDetailView` brownfield post-Wave C.1 ha **6 tabs** (`info`, `rules`, `faqs`, `sessions`, `agents`, `documents`) mentre il mockup ne ha **5** (`Info`, `Sessions`, `Chat`, `Stats`, `Documents`). Match:

| Mockup tab | Codebase tab | Status |
|---|---|---|
| Info | `info` | ✅ Match |
| Sessions | `sessions` | ✅ Match |
| Chat | `agents` (semantic) | ⚠️ Naming diverso ma scope correlato — `agents` mostra agent associati al game (vedi `useGameAgents` hook), mentre `Chat` mockup è inline chat con agent (un caso d'uso più specifico) |
| Stats | _(non esiste)_ | ❌ MANCA tab dedicata stats — `GameDetailKpiCards` esiste come componente standalone ma non integrato come tab |
| Documents | `documents` | ✅ Match |
| _(non esiste)_ | `rules` | 🆕 Codebase-only — `GameDetailRulesAccordion` (rule sections expandable) |
| _(non esiste)_ | `faqs` | 🆕 Codebase-only — `GameDetailFaqList` (FAQ correlate al game) |

**Implicazione strategica**: il codebase è andato OLTRE il mockup. Decisione design richiesta:
- (a) **Mantenere codebase 6-tabs** + integrare i 3 gap (specs/houserules/leaderboard) DENTRO tab esistenti (es. `specs` in `info`, `houserules` in `info`, `leaderboard` in nuova `stats` tab o in `info`)
- (b) **Refactor verso mockup 5-tabs** (drop `rules`/`faqs`, rinomina `agents` → `chat`, add `stats`) — _ALTO COSTO + regressione UX_
- (c) **Hybrid**: aggiungere tab `stats` per coprire mockup, lasciare `agents` per chat agent (semantic match), mantenere `rules`/`faqs` come codebase-extension

→ **Raccomandazione**: opzione (c) — minor effort + mantiene evoluzione codebase, aggiunge solo lo `stats` tab nuovo per matchare mockup.



## TL;DR

`/games/[id]` **NON è da implementare ex novo** — è già brownfield v2 mergiato in Wave C.1 (Issue #581). Il "pilot" originale del handoff QUICK_START § 7 è quindi un **conformity check**, non una build.

**Risultato check**: ottimo allineamento, **3 gap reali** + 1 gap variant identificati:

| Status | Count | Componenti |
|---|---|---|
| ✅ Già implementato + esposto via barrel | 8 | `GameDetailHero`, `GameDetailTabsAnimated` (con `tabIdFor`/`panelIdFor` helpers), `GameDetailKpiCards`, `GameDetailFaqList`, `GameDetailRulesAccordion`, `GameDetailSessionsRail`, `GameDetailAgentsList`, `GameDetailKbDocList` |
| ❌ MANCANTE come standalone | 3 | `GameSpecsCard`, `HouseRulesList`, `GameLeaderboard` |
| ⚠️ Variant non implementata | 1 | `CommunityGate` / `InfoTabLocked` (per giochi non in libreria, tabs locked) |
| 🔎 Da verificare | 1 | `ChatTab` (potrebbe usare uno dei `chat-unified` esistenti — non verificato) |

→ **Effort residuo per closure totale**: ~M (3-5h). 4 nuovi componenti + integrazione in `GameDetailView`.

## Convenzioni

| Simbolo | Significato |
|---|---|
| ✅ | Implementato + esposto via barrel `index.ts` |
| 🟢 | Implementato, NON esposto via barrel — uso possibile via import diretto |
| ❌ | Mancante — da creare |
| ⚠️ | Esiste in altro path / variant — riuso possibile con adapter |
| 🔎 | Da verificare apertura file |

## 1. Mapping componenti v2: mockup ↔ codebase

### 1.1. Componenti v2 dichiarati nel header del mockup (`sp4-game-detail.jsx` riga 12-19)

| # | Mockup name | Path proposto handoff (⚠️ obsoleto `ui/v2/`) | Status codebase | Path canonical attuale |
|---|---|---|---|---|
| 1 | `GameHero` | `apps/web/src/components/ui/v2/game-hero/` | ✅ | `apps/web/src/components/features/game-detail/GameDetailHero.tsx` |
| 2 | `GameTabs` (animated underline) | `apps/web/src/components/ui/v2/game-tabs/` | ✅ | `apps/web/src/components/features/game-detail/GameDetailTabsAnimated.tsx` |
| 3 | `GameSpecsCard` | `apps/web/src/components/ui/v2/game-specs-card/` | ❌ MANCANTE | _da creare_ → `apps/web/src/components/features/game-detail/GameDetailSpecsCard.tsx` |
| 4 | `GameStatsKpi` | `apps/web/src/components/ui/v2/game-stats-kpi/` | ✅ | `apps/web/src/components/features/game-detail/GameDetailKpiCards.tsx` |
| 5 | `GameLeaderboard` | `apps/web/src/components/ui/v2/game-leaderboard/` | ❌ MANCANTE | _da creare_ → `apps/web/src/components/features/game-detail/GameDetailLeaderboard.tsx` |
| 6 | `HouseRulesList` | `apps/web/src/components/ui/v2/house-rules-list/` | ❌ MANCANTE | _da creare_ → `apps/web/src/components/features/game-detail/GameDetailHouseRulesList.tsx` |

→ **3 componenti su 6** dichiarati v2 sono mancanti come standalone.

### 1.2. Sotto-componenti non dichiarati nel header ma presenti nel mockup

| Mockup name | Riga mockup | Equivalente codebase | Status |
|---|---|---|---|
| `ConnectionBar` | 90-160 | `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx` (riuso prod) | ✅ |
| `Stars` (rating) | 74-84 | _Inline o utility?_ — da verificare se esiste già `components/ui/stars/` | 🔎 |
| `InfoTab` orchestrator | 477-500 | Probabile inline in `GameDetailView` | 🔎 |
| `CommunityGate` (locked variant) | 596-626 | NON trovato | ❌ MANCANTE |
| `InfoTabLocked` | 502-508 | Wrapper di `CommunityGate` | ❌ MANCANTE |
| `SessionListItem` + `SessionsTab` | 513-594 | ✅ via `GameDetailSessionsRail.tsx` | ✅ |
| `ChatTab` | 631-726 | _Da verificare_ — potrebbe usare uno di `components/chat-unified/*` o `components/chat/panel/*` | 🔎 |
| `KpiCard` | 732-756 | Sub-componente in `GameDetailKpiCards.tsx`? | 🔎 (verificare se inline o estratto) |
| `Leaderboard` | 759-812 | = `GameLeaderboard` v2 ❌ MANCANTE | ❌ |
| `DocItem` + `DocsTab` | 831-888 | ✅ via `GameDetailKbDocList.tsx` | ✅ |
| `TabSkeleton` (loading state) | 893-901 | _Da verificare_ — pattern standard nel codebase (es. `MeepleCardSkeleton`) | 🔎 |
| `SpecsCard` | 381-420 | = `GameSpecsCard` v2 ❌ MANCANTE | ❌ |
| `HouseRulesCard` | 423-475 | = `HouseRulesList` v2 ❌ MANCANTE. Esiste pattern simile in `apps/web/src/components/library/game-table/HouseRulesSection.tsx` + `apps/web/src/components/session/live/HouseRulesDisplay.tsx` | ⚠️ |

## 2. Gap details (componenti da creare)

### 2.1. `GameDetailSpecsCard` ❌

| Aspetto | Valore |
|---|---|
| Path canonical | `apps/web/src/components/features/game-detail/GameDetailSpecsCard.tsx` |
| Mockup source | `sp4-game-detail.jsx:381-420` (function `SpecsCard`) |
| Shape props | `{ game: { players, duration, year, author, publisher, rating, weight }, labels?: { specs, players, duration, age, complexity, year, designer, publisher, ratingBgg } }` |
| Visual | Card `bg-card border` rounded-lg padding 18px con grid `repeat(auto-fill, minmax(140px, 1fr))` di 8 spec items |
| Spec items | Giocatori, Durata, Età (`14+` hardcoded mockup), Complessità (`weight.toFixed(1)/5`), Anno, Designer (= author), Editore (= publisher), Rating BGG |
| Età source | ⚠️ Hardcoded `'14+'` nel mockup — il BE `SharedGame` ha `MinAge : int?`? Verificare entity. Se mancante, omettere o default. |
| Effort | **S** (~30-45 min) |
| Test path | `apps/web/src/components/features/game-detail/__tests__/GameDetailSpecsCard.test.tsx` |
| DS-15 compliance | Use `text-muted-foreground`, `text-foreground`, `bg-card`, `border-border` — no hardcoded slate-*/gray-* |

### 2.2. `GameDetailHouseRulesList` ❌ (con riuso pattern esistente)

| Aspetto | Valore |
|---|---|
| Path canonical | `apps/web/src/components/features/game-detail/GameDetailHouseRulesList.tsx` |
| Mockup source | `sp4-game-detail.jsx:423-475` (function `HouseRulesCard`) |
| Shape props | `{ rules: HouseRule[], onToggle?: (id, enabled) => void, onAdd?: () => void, labels?: { title, addLabel } }` |
| Visual | Card `bg-card border` con header (title + "Aggiungi" button) + lista toggle switches (36×20px pill) + label |
| State management | Mockup usa `useState` interno — nel codebase reale state in `useState` controlled + chiamata API `POST/PATCH /api/v1/games/{id}/house-rules/{id}` via `agentMemoryClient.ts` |
| Pattern riusabile | `components/ui/toggle-switch/` esiste (vedi `eslint.config.mjs:583`). Riusare. |
| Pattern esistente correlato | `components/library/game-table/HouseRulesSection.tsx` (display lista FAQ-style) — diverso, non riusare |
| Dependency BE | `AgentMemory.Domain.Models.HouseRule.cs` (verificato in audit § 10). Shape `{ id, userId, gameId, houseRuleText, source, createdAt, updatedAt, ... }` — da verificare `enabled` campo exists |
| Effort | **S-M** (~45-60 min) |
| Note | Mockup ha `enabled : bool` ma BE potrebbe non averlo (default-on). Da verificare aprendo `HouseRule.cs` |
| Test path | `apps/web/src/components/features/game-detail/__tests__/GameDetailHouseRulesList.test.tsx` |

### 2.3. `GameDetailLeaderboard` ❌

| Aspetto | Valore |
|---|---|
| Path canonical | `apps/web/src/components/features/game-detail/GameDetailLeaderboard.tsx` |
| Mockup source | `sp4-game-detail.jsx:759-812` (function `Leaderboard`) |
| Shape props | `{ entries: LeaderboardEntry[], maxItems?: number = 10, labels?: { title, winsLabel, playsLabel, avgScoreLabel } }`<br>`LeaderboardEntry = { playerId, displayName, initials, color: number, wins, plays, avgScore }` |
| Visual | Card `bg-card border` con header "Classifica giocatori" + lista N giocatori. Ognuno: rank (🏆 per #1, `#2`/`#3`/... mono font), avatar circolare 32×32px con color HSL, name + plays/avg, wins-count grande |
| Data source | Aggregated da `PlayRecord` BE (winner count per game/user). Endpoint atteso: `GET /api/v1/games/{id}/leaderboard?since=` |
| Effort | **S** (~30-45 min) |
| Test path | `apps/web/src/components/features/game-detail/__tests__/GameDetailLeaderboard.test.tsx` |
| Player color source | Mockup usa `p.color : number` hue da `data.js` mock. BE: usa `User.AccentHue : int?` (da verificare) o hash di `User.Id`/`DisplayName` (cf. `Player.color` decision in `SCHEMA_DIFF.md § 2`) |

### 2.4. `GameDetailCommunityGate` ❌ + `InfoTabLocked` variant

| Aspetto | Valore |
|---|---|
| Path canonical | `apps/web/src/components/features/game-detail/GameDetailCommunityGate.tsx` |
| Mockup source | `sp4-game-detail.jsx:596-626` (function `CommunityGate`) + `502-508` (`InfoTabLocked` wrapper) |
| Shape props | `{ icon: string, title: string, description: string, ctaLabel?: string = '+ Aggiungi a libreria', onAdd: () => void }` |
| Visual | Empty-state pattern: dashed border, icon circle 64px, title h3, description, CTA primary game-color |
| Use case | Mostrato dentro tab content quando `variant === 'community'` (gioco non in libreria utente) e l'utente prova a personalizzare. Locked tabs = tabs disabilitate + icon 🔒 |
| Effort | **XS** (~20 min) |
| Integration | `GameDetailView` deve passare `variant: 'own' \| 'community'` ai children. `GameDetailTabsAnimated` deve supportare `locked?: boolean` per tab + render 🔒 disabled state. |

## 3. Decisioni richieste prima di implementare

| # | Decisione | Opzioni |
|---|---|---|
| 1 | `Stars` rating component — esiste o creo? | (a) creare standalone se non esistente · (b) riusare util esistente · (c) inline in `SpecsCard` |
| 2 | `ChatTab` — riusare `chat-unified` o creare tab-specific? | (a) riusare `chat-unified/CitationBlock` + `MessagesList` se compatibile · (b) wrapper feature-specific in `features/game-detail/GameDetailChatTab.tsx` |
| 3 | `GameDetailView` accetta già `variant`? Se no, brownfield refactor richiesto | _Aprire `GameDetailView.tsx` per verificare_ |
| 4 | `HouseRule.enabled : bool` esiste in BE? | _Aprire `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/HouseRule.cs`_ |
| 5 | `User.AccentHue : int?` esiste in BE per Leaderboard color? | _Aprire `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs`_ |
| 6 | `GameLeaderboard` ha endpoint dedicato `/api/v1/games/{id}/leaderboard`? | _Grep su `lib/api/clients/*` e/o BE `Routing/*Endpoints.cs`_ |

## 3.5. Decisioni RISOLTE (post wave read-only 2026-05-24 PM)

| # | Decisione | Risposta | Implicazione |
|---|---|---|---|
| 1 | `Stars` rating | ⚠️ **PARZIALMENTE ESISTE** — `apps/web/src/components/features/toolkit-detail/Stars.tsx` (feature-specific) | **Decisione**: estrarre in `apps/web/src/components/ui/feedback/Stars.tsx` come canonical riutilizzabile + adapter in `toolkit-detail` e `game-detail`. Effort: S (~20 min refactor + import update + test). _Alternativa_: inline in `GameDetailHero` per ridurre scope (effort XS, ma duplica codice) |
| 2 | `ChatTab` strategy | ⚠️ **chat-unified** esiste ricchissimo (8+ componenti: `CitationBadge`, `CitationBlock`, `CitationSheet`, ecc.) | **Decisione**: creare adapter `GameDetailChatTab.tsx` in `features/game-detail/` che riusa `chat-unified` primitives. Il tab "Chat" del mockup è in realtà già coperto **semanticamente** dalla tab `agents` codebase — vedi divergenza tab-list § Header. → **Re-mappare**: rimuovere "Chat" come tab nuovo, considerare riuso `agents` tab. |
| 3 | `GameDetailView.variant` | ❌ **NON ESISTE** — props attuali solo `{ gameId: string \| null }` (Phase 0.5 contract) | **Decisione**: aggiungere `variant?: 'own' \| 'community'` prop con default `'own'` derivato da `useLibraryGameDetail` (se `data` esiste = own, altrimenti community). Refactor `GameDetailView` per supportare i 2 modes + integrazione `GameDetailCommunityGate`. Effort: M (~60 min refactor + test + variant story Storybook) |
| 4 | `HouseRule.enabled` | ❌ **NON ESISTE** — BE shape minimalissimo (3 campi: `Description : string`, `AddedAt : DateTime`, `Source : HouseRuleSource enum`). Niente `Id`, niente `UserId`, niente `GameId`, niente `enabled` | **Decisione critica**: la `HouseRule` BE è un **value object embedded** in `GameMemory`/`PlayerMemory`/`GroupMemory` aggregates (vedi `AgentMemory.Domain.Models/` folder, non `Entities/`). UI mockup `enabled : bool` toggle è **incompatibile** con BE shape — non si "toggle", si **add/remove**. Riprogetto `GameDetailHouseRulesList` con CRUD operations (add/edit description/delete) + segnalare divergenza al designer per riallineamento mockup |
| 5 | `User.AccentHue` | ❌ **NON ESISTE** — `User` entity ha 34+ campi ma niente `AccentHue` / `Color` / `Theme` (Theme esiste come `string` user-pref ma non hue) | **Decisione**: implementare utility `userHue(userId: Guid \| string): number` in `apps/web/src/lib/colors/user-hue.ts` con hash deterministico (es. CRC32 mod 360 o simhash). Effort: XS (~15 min). Trade-off: fade nice-to-have, no migration BE richiesta |
| 6 | `Leaderboard endpoint` | ❌ **NON ESISTE** — grep "leaderboard" su `lib/api/clients/` matcha solo `badgesClient.ts` (badges leaderboard, contesto diverso). Niente endpoint `/api/v1/games/{id}/leaderboard` | **Decisione**: 3 opzioni in order of preference: (a) **Aggiungere BE endpoint** in `GameManagement` o `SharedGameCatalog` BC con CQRS query `GetGameLeaderboardQuery` (effort M ~2h + BE PR) · (b) **Derivare client-side** dai `playRecordsClient.ts` esistenti con aggregazione lato FE (effort S ~30 min ma O(N²) potenziale) · (c) **Skip per ora**, mostrare `<EmptyState>` placeholder "Classifica in arrivo" — XS unblock pilot. **Raccomandazione**: (c) per ora + opener follow-up issue BE per (a) |

## 4. Conformity sub-check (post-decisioni)

Per chiudere il loop di conformity totale:

| Sub-check | Action | Owner |
|---|---|---|
| Visual fidelity 1:1 | Side-by-side screenshot mockup HTML vs route reale + Playwright visual diff con `chromatic` | Designer + dev |
| Tabs 5 (Info/Sessions/Chat/Stats/Documents) — match | Aprire `GameDetailView` per verificare i 5 tabs sono presenti + URL hash navigation `#info` etc. | Dev |
| States (default/loading/empty/error) | Verificare `GameDetailView` ha tutti i 4 stati (mockup li ha) | Dev |
| Variant own/community switch | Verificare `GameDetailView` accetta `variant?` prop + render CTA differenti (`Gioca ora` vs `Aggiungi a libreria`) | Dev |
| API endpoint | `GET /api/v1/games/{id}?include=stats,sessions(limit=5),agents,kb,houseRules` — verificare in `gamesClient.ts` se supporta `?include=` | Dev |
| A11y | Tabs WAI-ARIA tablist + keyboard nav (`tabIdFor`/`panelIdFor` helpers già in `GameDetailTabsAnimated`) | Verifica `pnpm test:a11y:e2e` |
| Tests | Verificare unit tests `__tests__/*.test.tsx` per i 9 sub-componenti esistenti + aggiungere per i 4 nuovi | Dev |

## 5. Effort breakdown finale (RIVISTO post-decisioni § 3.5)

| Task | Effort | Acceptance criteria (SMART) | Stato |
|---|---|---|---|
| **5a** Verifica state `GameDetailView` | XS (15 min) | _Given/When/Then_ docu structure | ✅ **DONE** 2026-05-24 PM |
| **5b** Risolvi 6 decisioni § 3 | S (30 min) | _Given/When/Then_ evidence per ogni | ✅ **DONE** 2026-05-24 PM (vedi § 3.5) |
| **5c** Crea `GameDetailSpecsCard` + test | S (45 min) | Componente + test + Storybook. `pnpm typecheck && pnpm lint && pnpm test` pass | ⏳ next |
| **5d** Crea `GameDetailHouseRulesList` + test + CRUD integration | **L (90 min)** ⚠️ aumentato | BE shape `{ Description, AddedAt, Source }` → UI deve essere add/edit/delete, NON toggle. API call cablato a endpoint `POST/PATCH/DELETE /api/v1/games/{id}/memory/house-rules` (verificare client). Form modal + confirmation modal. | ⏳ next |
| **5e** Crea `GameDetailLeaderboard` placeholder + follow-up issue | **XS (20 min)** ⚠️ degraded | EmptyState `<GameDetailEmptyState>` con messaggio "Classifica in arrivo" + skeleton component pronto per quando BE endpoint arriva. Aprire follow-up issue BE per `GetGameLeaderboardQuery` (effort M ~2h, separato) | ⏳ next |
| **5f** Crea `GameDetailCommunityGate` + variant integration | **M (60 min)** ⚠️ aumentato | Componente + refactor `GameDetailView` per supportare `variant?: 'own' \| 'community'` + integrazione `GameDetailTabsAnimated.locked` con icon 🔒 + tabs disabilitate per community | ⏳ next |
| **5g** Refactor `GameDetailView` con i 4 nuovi + tab `stats` | **M (60 min)** ⚠️ aumentato | Add `stats` tab (mockup-required) + integrazione 4 nuovi componenti + variant switching + Phase 0.5 contract preserved | ⏳ next |
| **5h** Aggiungere 4 nuovi + Stars + userHue al barrel `index.ts` | XS (5 min) | Export lines | ⏳ next |
| **5i** Storybook stories per i 4 nuovi | S (30 min) | 4 `.stories.tsx` con default/empty/loading variants. Chromatic captures | ⏳ next |
| **5j** Run full DoD checklist 9-point | XS (15 min) | Vedi `CODEBASE_AUDIT.md § 14.5` | ⏳ next |
| **5k** 🆕 Stars refactor → `ui/feedback/Stars.tsx` canonical | S (20 min) | Estrarre da `features/toolkit-detail/Stars.tsx` + adapter import in toolkit-detail e game-detail | ⏳ next |
| **5l** 🆕 `userHue(userId)` utility in `lib/colors/user-hue.ts` | XS (15 min) | Hash deterministico (CRC32 mod 360) + unit test (3 fixture userId → expected hue) | ⏳ next |
| **5m** 🆕 New `stats` tab content composition | _included in 5g_ | Specs card + leaderboard placeholder + KPI cards (`GameDetailKpiCards` esistente) | ⏳ next |

**Totale post-decisioni**: **~7-8h** per closure totale del pilot conformity check.

→ **Variation vs stima originale (3-5h)**: +3-4h imputabili a (a) `HouseRule` BE shape minimalissimo (incompatibile con toggle UI mockup, richiede CRUD), (b) `GameDetailView.variant` refactor non previsto, (c) Stars canonical refactor, (d) userHue utility, (e) leaderboard endpoint mancante.

## 6. Stato dei deliverable Step 1-7

| Step | Deliverable | Stato |
|---|---|---|
| 1 | `design_handoff/CODEBASE_AUDIT.md` | ✅ done |
| 2-3 | Step 2-3 sub-task | ✅ APPLICATO 2026-05-24 |
| 4 | Types entity strategy | ✅ SKIP (decisione utente) |
| 5 | `design_handoff/SCHEMA_DIFF.md` | ✅ done (+ spot-check 2 entity) |
| 6 | `design_handoff/COMPONENTS_AUDIT.md` | ✅ done (+ post-review patches) |
| **7** | `design_handoff/PILOT_GAP_REPORT.md` | ✅ **GENERATO (questo file)** |
| 7-impl | Closure dei 4 gap (`SpecsCard`/`HouseRulesList`/`Leaderboard`/`CommunityGate`) | ⏳ next decision |

## 7. Raccomandazione next step (RIVISTA post wave decisioni)

**Stato attuale**: tutte le 6 decisioni risolte. Effort closure rivisto a ~7-8h (vs 3-5h originale).

### Path forward — 3 opzioni

#### Opzione A — Closure in sessione corrente (~7-8h ulteriori)

Eseguire tutti i sub-task 5c → 5m in sequenza. Rischio: sessione molto lunga, focus drift, possibili regressioni multi-componente. Beneficio: pilot completo in 1 PR.

#### Opzione B — Pilot ridotto "MVP foundation" (~3-4h ulteriori)

Implementare in questa sessione SOLO i quick-win + foundation:
- 5k Stars refactor canonical (S, 20 min)
- 5l userHue utility (XS, 15 min)
- 5c GameDetailSpecsCard (S, 45 min) — gap concreto + AC chiari
- 5h barrel update (XS, 5 min)
- 5i Storybook story per SpecsCard (S, 30 min)
- 5j DoD verify (XS, 15 min)

→ **~2-2.5h**. Deliverable: SpecsCard integrato in `/games/[id]` + foundation Stars/userHue per i futuri Leaderboard/Hero. Issue follow-up per 5d/5e/5f/5g.

#### Opzione C — Stop e commit fase audit + decisioni

Tutti i 4 deliverable handoff (CODEBASE_AUDIT, SCHEMA_DIFF, COMPONENTS_AUDIT, PILOT_GAP_REPORT) + Step 2-3 code changes sono pronti. Committiamo, aprime PR draft, ripartiamo Opzione A o B in sessione futura.

→ **Raccomandazione**: **Opzione C** per chiudere "spec + audit + foundation setup" come 1 deliverable atomico. Le 4 issue di gap closure (5c/5d/5e/5f/5g/5k/5l/5m + 5h/5i/5j) possono essere PR di follow-up dedicati con scope ridotto + review più focused.

## 8. Issue follow-up CREATE (2026-05-24)

✅ **Tutte e 9 le issue sono state aperte sul repo `meepleAi-app/meepleai-monorepo`**:

| # | GitHub | Title | Effort | Priority | Status |
|---|---|---|---|---|---|
| 1 | [#1463](https://github.com/meepleAi-app/meepleai-monorepo/issues/1463) | `feat(game-detail): GameDetailSpecsCard component (sp4-game-detail.jsx gap)` | S (45 min) | P1 | open |
| 2 | [#1464](https://github.com/meepleAi-app/meepleai-monorepo/issues/1464) | `feat(game-detail): GameDetailHouseRulesList con CRUD operations (BE shape mismatch)` | L (90 min) | P1 | open |
| 3 | [#1465](https://github.com/meepleAi-app/meepleai-monorepo/issues/1465) | `feat(game-detail): GameDetailCommunityGate + variant routing` | M (60 min) | P2 | open · depends on #1466 |
| 4 | [#1466](https://github.com/meepleAi-app/meepleai-monorepo/issues/1466) | `feat(game-detail): GameDetailView refactor — variant prop + stats tab` | M (60 min) | P2 | open |
| 5 | [#1467](https://github.com/meepleAi-app/meepleai-monorepo/issues/1467) | `feat(api): GetGameLeaderboardQuery + endpoint GET /api/v1/games/{id}/leaderboard` | M (2h BE) | P2 | open |
| 6 | [#1468](https://github.com/meepleAi-app/meepleai-monorepo/issues/1468) | `feat(game-detail): GameDetailLeaderboard component (depends on BE leaderboard endpoint)` | S (45 min) | P2 | open · depends on #1467 + #1470 |
| 7 | [#1469](https://github.com/meepleAi-app/meepleai-monorepo/issues/1469) | `refactor(ui/feedback): canonical Stars component (lift from toolkit-detail)` | S (20 min) | P3 | open |
| 8 | [#1470](https://github.com/meepleAi-app/meepleai-monorepo/issues/1470) | `feat(lib): userHue(userId) deterministic color utility` | XS (15 min) | P3 | open |
| 9 | [#1471](https://github.com/meepleAi-app/meepleai-monorepo/issues/1471) | `feat(game-detail): GameDetailChatTab via chat-unified adapter` | S (30 min) | P3 | open |

**Dependency graph**:
```
#1467 (BE leaderboard endpoint) ──► #1468 (Leaderboard FE)
                                  │
#1470 (userHue utility)  ─────────┘
#1466 (GameDetailView refactor) ──► #1465 (CommunityGate integration)
                                  ├► #1463 (SpecsCard integration in Info tab)
                                  ├► #1464 (HouseRulesList integration in Info tab)
                                  ├► #1468 (Leaderboard integration in stats tab)
                                  └► #1471 (ChatTab integration in agents tab)
#1469 (Stars canonical) ──────────► usable across game-detail
```

**Recommended implementation order**:
1. **Phase 1 — Foundation primitives**: #1470 userHue + #1469 Stars (XS+S, ~35 min parallel)
2. **Phase 2 — BE work**: #1467 leaderboard endpoint (M, ~2h — può essere parallelo a Phase 1)
3. **Phase 3 — FE standalone**: #1463 SpecsCard + #1464 HouseRulesList (S+L, ~135 min)
4. **Phase 4 — Refactor**: #1466 GameDetailView (M, ~60 min) — apre la strada a tutte le integrazioni
5. **Phase 5 — Integration**: #1465 CommunityGate + #1468 Leaderboard FE + #1471 ChatTab (M+S+S, ~135 min)

Cumulativi: ~7-8h distribuiti in 5-7 PR atomici reviewable.

---

**Generato da Claude Code Opus 4.7 in modalità read-only.** Nessuna modifica al codebase. Decisioni § 3.5 risolte 2026-05-24 PM.
