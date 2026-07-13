# Design — Issue #2858 (C1): componente card canonico + decision-table + `MeepleCardGame` adapter

**Data:** 2026-07-13
**Issue:** [#2858](https://github.com/meepleAi-app/meepleai-monorepo/issues/2858) (ST4) — umbrella [#2863](https://github.com/meepleAi-app/meepleai-monorepo/issues/2863) "MeepleCard family debt teardown"
**Audit di riferimento:** `docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md` (§2.1, §3 barriera CRITICAL "nessun componente canonico", §6 ST4)
**Branch:** `feature/issue-2858-canonical-card-decision-table` da `main-dev`
**Dipendenze:** nessuna. **Sblocca:** C4 (#2861 AST body-gate), C5 (#2862 token migration).

---

## 1. Problema

L'audit ha rilevato la barriera più grave della MeepleCard family: **nessun componente card canonico**. Coesistono tre "famiglie" card e ≥5 renderer per la sola entità `game`. Quando un mockup mostra "una card", il default per lo sviluppatore diventa copiare l'HTML in un nuovo renderer standalone → divergenza per pagina.

La verifica sul codice attuale (`main-dev`, post Phase A/B/C2) mostra però che le tre famiglie **non sono la stessa cosa**:

- **`ui/data-display/meeple-card/` → `MeepleCard`** è già di fatto il canonico per il **display** in liste/griglie: 5 varianti (grid/list/compact/featured/hero), ~18 parts atomici, e ~10 adapter DTO→`MeepleCardProps` che lo compongono correttamente via `lib/card-mappers/*` (`MeepleGameCard`, `MeepleGameCatalogCard`, `MeepleLibraryGameCard`, `MeepleAgentCard`, `MeepleKbCard`, `MeepleSessionCard`, `MeepleEventCard`, `MeepleChatCard`, `MeeplePlayerCard`, `MeepleUserLibraryCard`).
- **`ui/data-display/extra-meeple-card/` → `ExtraMeepleCard`** è il canonico per il **dettaglio**: card 600×900 a tab per drawer/pagina, ~29 consumer via cascade-store, genuinamente indipendente (solo import type-only da meeple-card). Non è collassabile in una grid card: è uno scopo diverso.
- **`ui/shared-games/meeple-card-game.tsx` → `MeepleCardGame`** è l'**unico vero renderer standalone rogue**: NON compone `MeepleCard`, reimplementa stelle/cover/badge inline con `hsl(var(--c-*))` legacy. Un solo consumer: `shared-games-grid.tsx:95` → route pubblica `/shared-games`.

## 2. Decisioni (brainstorm 2026-07-13)

1. **Framing → tassonomia canonica a 2 tier.** Formalizzare `MeepleCard` = tier DISPLAY, `ExtraMeepleCard` = tier DETAIL. Non un unico canonico assoluto (assorbire il drawer sarebbe ~29 consumer + sistema 6-tab, oltre lo scope L e ad alto rischio).
2. **`MeepleCardGame` → adapter, "canonico + segnali mappati".** Il body diventa una composizione di `MeepleCard entity="game" variant="grid"`; i segnali distintivi si mappano su props canoniche; si accetta la normalizzazione visiva verso il look canonico su `/shared-games`.
3. **Lint C1 = solo import-boundary (`error`); compose-enforcement → C4** (corretto in fase di plan, 2026-07-13). Il compose-check *basato sul nome* `*Card` è impraticabile: il codebase ha ~100+ componenti `*Card` generici (`StatCard`, `KPICard`, `GlassCard`, `AuthCard`, `PricingCard`, `HubGameCard`, `MeepleInfoCard`, `MeepleWishlistCard`, …) senza rapporto con MeepleCard → floodrebbe la CI o richiederebbe un'allowlist da 100+ voci arbitrarie. Quindi C1 spedisce **solo** la ESLint rule *import-boundary* (ban dei deep-import di `meeple-card/parts/`/`variants/` da fuori la dir canonica). L'enforcement "non reimplementare cover/stelle/badge inline" va **interamente a C4** (#2861 body-inspection, keyed sui primitivi, non sul nome). Split a 2 layer preservato, più corretto.
4. **Decision-table = doc markdown + test di tracciabilità.** Versionabile (Wiegers) e anti-drift (Adzic).
5. **Anchor via `href` first-class su MeepleCard** (deciso in fase di plan, 2026-07-13). Il root di `MeepleCardGame` è un vero `<a href>` (`<Link prefetch>`) richiesto sulla route pubblica `/shared-games` (prefetch, apri-in-nuova-scheda, focus nativo, SEO). Il canonico `GridCard` rende un `<div role=button>` e naviga via `onClick`. Si aggiunge `href?: string` a `MeepleCardProps`: quando presente, la root di `GridCard` rende `<Link href>` invece di `<div>`. È una capability semantica riusabile, non un hack di fedeltà; scope C1 limitato a `GridCard`.

## 3. Tassonomia canonica

| Tier | Componente canonico | Scopo | Come si usa |
|---|---|---|---|
| **DISPLAY** | `MeepleCard` (`ui/data-display/meeple-card/`) | Card in liste/griglie — 5 varianti | adapter DTO→`MeepleCardProps` (mapper `lib/card-mappers/`) |
| **DETAIL** | `ExtraMeepleCard` (`ui/data-display/extra-meeple-card/`) | Drawer/pagina di dettaglio 600×900 a tab | `ExtraMeepleCardDrawer` (cascade-store) |

Regola per lo sviluppatore che porta un mockup: **scegli il tier dal contesto** (lista/griglia → DISPLAY; drawer/dettaglio → DETAIL), **poi l'adapter dalla DTO**; se manca, crea un adapter, **mai** un renderer standalone.

## 4. Conversione `MeepleCardGame` → adapter

**Principio.** L'interfaccia pubblica `MeepleCardGameProps` resta invariata (eccetto `compact`, vedi sotto) così che `page-client.tsx` non cambi. Il *body* diventa una composizione di `MeepleCard entity="game" variant="grid" href={/shared-games/{id}}`. L'adapter risolve l'i18n (`labels`) in stringhe prima di passarle.

**Anchor (`href`).** `MeepleCard` acquisisce `href?: string`; `GridCard` rende la root come `<Link href prefetch>` quando `href` è presente (altrimenti `<div>` come oggi). L'attribution footer resta sibling di `GridCard` (renderizzato da `MeepleCardImpl` fuori dalla root) → nessun anchor annidato. L'adapter passa i props Wikidata a `MeepleCard`, che rende il footer.

**Hook DOM.** Il tile è identificato via `data-testid="shared-games-card"` (forwarded da `GridCard` alla root). `shared-games-grid.test.tsx` passa da `[data-slot="shared-games-card"]` a `[data-testid="shared-games-card"]` (3 selettori). `data-game-id` è rimosso: non è usato esternamente su questa tile (verificato via grep; le altre occorrenze sono componenti diversi).

**Mapping dei segnali (DTO shared-games → `MeepleCardProps`):**

| Segnale attuale (inline) | → | Prop canonica | Esito |
|---|---|---|---|
| `rating` (0–5, ★/☆ inline) | → | `rating={rating} ratingMax={5}` | `Rating` part canonico |
| `coverUrl` + fallback 🎲 | → | `imageUrl` (+ `coverEmoji` se serve) | `Cover` canonico (gradient/shimmer entity) |
| `year` (mono uppercase) | → | `subtitle`/`metadata` chip | meta canonico |
| `toolkitsCount`/`agentsCount`/`kbsCount` (footer `EntityChip`) | → | `connections: ConnectionChipProps[]` (count-only) | `ConnectionChipStrip` footer canonico |
| `newThisWeekCount ≥ 2` (badge `--c-event` rosa) | → | `badge` stringa localizzata ("＋N") | badge neutro canonico |
| `wikidataCover*` | → | pass-through (già supportati per `game`) | `MeepleCardAttributionFooter` |

**Delta di fedeltà dichiarati** (conseguenza voluta della scelta "look canonico"):

1. **Badge "new this week"** perde il rosa `--c-event` → badge neutro canonico. L'informazione ("＋N nuovi") resta; cambia solo il colore. **Confermato accettabile** in brainstorm.
2. **Cover** passa dal fallback 🎲 piatto al `Cover` canonico (gradient entity + shimmer), coerente con le altre game-card.
3. **Prop `compact`.** Verifica sul codice: `page-client.tsx` non passa mai `compact` a `SharedGamesGrid` → in produzione è sempre `false` (knob responsive mai cablato; guida solo altezza cover/padding/emoji-size). Il tier canonico `grid` ha aspect-ratio fisso 7:10, senza equivalente `compact`. **Risoluzione:** rimuovere `compact` da `MeepleCardGameProps`; `shared-games-grid.tsx:95` smette di inoltrarlo alla game-card. `SharedGamesGrid.compact` **resta** (continua a guidare `SkeletonCard compact`).
4. **EntityBadge "Gioco" (nuova superficie).** Il `GridCard` canonico rende un pill EntityBadge top-left assente nel vecchio tile. Rientra nella normalizzazione canonica ed è coerente con `/games` discover (stesso `MeepleCard grid`). Segnalato per designer review su PR.
5. **Aria-label del badge "new this week" (I-2, final review).** Il vecchio tile aveva `aria-label` "N nuovi questa settimana" sul badge; il badge canonico (`CardFooter`) è testo semplice "＋N" senza espansione aria — **coerente con tutti i badge `CardFooter` dell'app** (nessuno ha aria-label). Delta accettato: normalizzazione, non degradazione unica. `newWeekAriaLabel` resta sull'interfaccia (ora morto, come `ratingAriaLabel`).

**Correzione a11y (C-1, final review — root cause).** Instradare il tile pubblico attraverso `GridCard` con `href` metteva elementi interattivi dentro l'`<a>` (nested-interactive, WCAG 4.1.2): `MenuPlaceholder` (`<button>`) e i `ConnectionChip` count-only (`<button>` inerte). Fix root-cause: un `ConnectionChip` **statico** (nessun items/create/onClick/href) ora rende `<span role="img" aria-label>` invece di un `<button>` che non fa nulla — correzione valida app-wide (anti-pattern latente) oltre che abilitante per le anchor-card; `MenuPlaceholder` è omesso sulle card con root anchor (`href`). Guard test in `meeple-card-game.test.tsx`: l'`<a>` non contiene `button`/`a` annidati.

**Test.** `meeple-card-game.test.tsx` passa da asserzioni sul markup inline ad **asserzioni comportamentali** sul render canonico: titolo (`h3`), stelle via ruolo/aria-label, chip di connessione con count (toolkit/agent/kb), badge "new this week", attribution footer Wikidata quando presente. Coerente con l'acceptance-matrix #2859.

## 5. ESLint rule `local/no-standalone-card-renderer` (import-boundary, severità `error`)

**Obiettivo:** rendere strutturalmente impossibile riassemblare una card "rubando" i parts atomici. **Un solo controllo** (import-boundary), preciso e a falsi-positivi ~0.

- **Controllo unico — import-boundary.** Vietato il **value-import** da `**/meeple-card/parts/**` e `**/meeple-card/variants/**` da file **fuori** da `ui/data-display/meeple-card/`. Solo l'export pubblico `MeepleCard` (root della dir) è consumabile. → non puoi comporre una card a mano dai parts interni.

> Il compose-check basato sul **nome** `*Card` è stato **scartato**: il codebase ha ~100+ componenti `*Card` generici (`StatCard`, `KPICard`, `GlassCard`, `AuthCard`, `PricingCard`, `HubGameCard`, `MeepleInfoCard`, …) non legati a MeepleCard. L'enforcement "non reimplementare cover/stelle/badge inline" è **interamente C4** (#2861 body-inspection, keyed sui primitivi renderizzati, non sul nome).

**Esenzioni (perché `error` sia sicuro da subito — verificate via grep):**

- Dir canonica `ui/data-display/meeple-card/` (i suoi interni usano i parts legittimamente).
- File di test (`**/__tests__/**`, `*.test.*`): importano gli interni per testarli.
- **`import type`**: gli import type-only non trascinano logica di rendering (es. `ManaPip`/`ManaPipItem`) → esenti.
- **Allowlist esplicita** per path (pattern `SPREAD_ALLOWLIST` di `call-site-coverage.test.tsx`). Al momento **1 voce**: `src/hooks/queries/useGameManaPips.ts` — importa il value util `getKbPipColor` (colore pip), non un componente card. Nuove eccezioni si aggiungono con motivazione in commit, senza degradare a `warn`.

**Wiring:** `apps/web/eslint-rules/no-standalone-card-renderer.js` + `.test.js` (RuleTester), export in `eslint-rules/index.js`, registrazione + severità `error` in `eslint.config.mjs` (stesso pattern di `no-store-scores-direct` / `no-hardcoded-color-utility`).

## 6. Decision-table + test di tracciabilità

**Doc** → `docs/for-developers/frontend/card-decision-table.md` (accanto a `v2-migration-matrix.md`). Colonne: **Contesto/Route · DTO primario · Entity · Tier · Adapter · Variante tipica**. Righe derivate dalla mappatura verificata, es.:

| Contesto/Route | DTO | Entity | Tier | Adapter | Variante |
|---|---|---|---|---|---|
| `/library?tab=games` | `UserLibraryEntry` | game | DISPLAY | `MeepleUserLibraryCard` | grid |
| `/games?tab=discover` | `SharedGame` | game | DISPLAY | `MeepleGameCatalogCard` | grid |
| `/shared-games` | shared-tile | game | DISPLAY | `MeepleCardGame` (adapter) | grid |
| `/library?tab=agents`, `/agents` | `AgentDto` | agent | DISPLAY | `MeepleAgentCard` | grid |
| `/library?tab=kb` | `PdfDocumentDto` | kb | DISPLAY | `MeepleKbCard` | grid |
| `/library?tab=sessions` | `GameSessionDto` | session | DISPLAY | `MeepleSessionCard` | grid |
| `/library?tab=chat` | `ChatSessionSummaryDto` | chat | DISPLAY | `MeepleChatCard` | grid |
| `/dashboard#Prossimi/#Recenti` | `GameNightSummary` | event | DISPLAY | `MeepleEventCard` | list/compact |
| drawer/dettaglio | `*DetailData` | * | DETAIL | `*ExtraMeepleCard` | — |

(La tabella completa nel doc include tutte le righe degli adapter che compongono `MeepleCard`.)

**Test** → `card-decision-table.test.ts` — coverage **`<MeepleCard>`-usage-based** (precisa, name-independent; nessun compose-check sul nome):

- **Coverage:** ogni file di produzione sotto `src/{app,components}` (esclusi test/dev/showcase e la dir `meeple-card/`) che **rende `<MeepleCard …>`** in JSX è un adapter display-tier e il suo componente **deve** comparire in una riga della tabella. → la mappa non può omettere un adapter reale senza rompere il build (guardia "la mappa non mente", stile test importPath del registry). Definizione name-independent: si conta l'uso di `<MeepleCard>`, non il nome `*Card`.
- **No righe dangling:** ogni adapter citato nella tabella (`` `MeepleXxx` `` / `` `XxxExtraMeepleCard` ``) esiste ed è esportato in `src/components/**` (scan export).
- **Safeguard:** floor sul numero di file scansionati (>50) per evitare pass vacui su glob rotto (come `call-site-coverage.test.tsx`).

## 7. Strategia TDD (ordine)

1. **`href` su MeepleCard/GridCard** (prerequisito conversione): test GridCard href (rosso) → aggiungo `href?` a `types.ts` + branch `<Link>` in `GridCard.tsx` (verde).
2. **`MeepleCardGame` → adapter**: aggiorno `meeple-card-game.test.tsx` al contratto comportamentale + aggiorno i 3 selettori di `shared-games-grid.test.tsx` (rosso) → converto il body ad adapter, rimuovo `compact` da `MeepleCardGameProps`, aggiorno `shared-games-grid.tsx` (verde).
3. **ESLint rule import-boundary**: casi `RuleTester` (rosso: valid = adapter che compone MeepleCard / file esente / `import type` / allowlist; invalid = value deep-import di `parts/`/`variants/`) → implementazione (verde) → registrazione `error` in `eslint.config.mjs` → `pnpm lint` full (verifica 0 violazioni: solo `useGameManaPips` in allowlist).
4. **Decision-table**: `card-decision-table.test.ts` coverage `<MeepleCard>`-usage (rosso) → scrivo `card-decision-table.md` con tutte le righe adapter (verde).
5. Verifica finale: `pnpm test` (mirato meeple-card + shared-games), `pnpm typecheck`, `pnpm lint` (nuova rule attiva), `pnpm build`. **Gate a11y BLOCKING:** il render canonico usa già token AA (`--c-*` 38% + `--c-*-text`), rischio contrasto basso; verifica su `/shared-games`.

**Ordine di merge (importante):** l'import-boundary non riguarda `MeepleCardGame` (che importa il root `MeepleCard`, non i parts), quindi non c'è dipendenza d'ordine stretta; la sequenza sopra tiene comunque la CI verde tra un task e l'altro.

## 8. Scope

**In scope:** conversione `MeepleCardGame`→adapter; decision-table doc + test; ESLint rule + wiring.

**Fuori scope (deferiti, con motivazione):**

- **Rename `extra-meeple-card/`** → il tier è ora documentato dalla decision-table; il rename è ~55 file + ~29 consumer di churn, sproporzionato per una issue L. Follow-up separato.
- **Props orfane** (`customColor`, `coverLabels`) → cleanup, non C1.
- **Migrazione token `--mc-*`→semantici** → è C5 (#2862), dopo C1.
- **Gate AST body-inspection** (inline cover/stelle/badge) → è C4 (#2861), dopo C1.

## 9. Rischi

| Rischio | Mitigazione |
|---|---|
| Import-boundary genera falsi positivi bloccando `error` | Solo 3 hit fuori dalla dir canonica (verificato via grep): 2 in test + 1 in `useGameManaPips`; esenzioni `import type` + test + 1 allowlist coprono tutto. `pnpm lint` full nel task 3 conferma 0 residui |
| Cambio visivo su route pubblica `/shared-games` | Deciso e accettato in brainstorm (look canonico + segnali mappati); delta dichiarati in §4 |
| Regressione a11y sul nuovo render (rating perde `role=img`/aria-label) | Token già AA (axe passa); il rating canonico è quello usato da tutte le altre game-card → coerenza, non nuovo problema. Verifica esplicita su `/shared-games` nel gate BLOCKING |
| `connections` count-only rende chip diverso dall'atteso | ConnectionChipStrip footer con count e senza `items`/handler = chip statico con count, match funzionale del footer attuale |
| Un nuovo renderer di entity-card *senza* nome `*Card` sfugge al lint C1 | È by-design: il compose/reimpl-guard è C4 (body-inspection sui primitivi). C1 copre solo l'import-boundary strutturale |

## 10. Acceptance criteria

- [ ] `MeepleCard` accetta `href?`; `GridCard` rende `<Link href>` quando presente, `<div>` altrimenti (comportamento esistente invariato per i consumer senza `href`).
- [ ] `MeepleCardGame` compone `MeepleCard` (nessun `★`/cover/badge inline); interfaccia pubblica invariata eccetto `compact` rimosso; root è un `<a href="/shared-games/{id}">`.
- [ ] `/shared-games` renderizza via tier canonico; test comportamentali verdi; nessuna regressione a11y.
- [ ] `docs/for-developers/frontend/card-decision-table.md` presente; `card-decision-table.test.ts` verde (coverage `<MeepleCard>`-usage + no-dangling).
- [ ] `local/no-standalone-card-renderer` (import-boundary) attiva a `error`; `RuleTester` verde; `pnpm lint` pulito sul codebase (unica allowlist: `useGameManaPips`).
- [ ] `pnpm test` mirato, `pnpm typecheck`, `pnpm lint`, `pnpm build` verdi.
