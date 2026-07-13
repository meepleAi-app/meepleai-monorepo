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
3. **Lint a 2 layer complementari, C1 = `error`.** C1 spedisce la ESLint rule *guardia dichiarazione/uso*; C4 aggiungerà il gate AST *guardia implementazione* (body-inspection). Zero overlap.
4. **Decision-table = doc markdown + test di tracciabilità.** Versionabile (Wiegers) e anti-drift (Adzic).

## 3. Tassonomia canonica

| Tier | Componente canonico | Scopo | Come si usa |
|---|---|---|---|
| **DISPLAY** | `MeepleCard` (`ui/data-display/meeple-card/`) | Card in liste/griglie — 5 varianti | adapter DTO→`MeepleCardProps` (mapper `lib/card-mappers/`) |
| **DETAIL** | `ExtraMeepleCard` (`ui/data-display/extra-meeple-card/`) | Drawer/pagina di dettaglio 600×900 a tab | `ExtraMeepleCardDrawer` (cascade-store) |

Regola per lo sviluppatore che porta un mockup: **scegli il tier dal contesto** (lista/griglia → DISPLAY; drawer/dettaglio → DETAIL), **poi l'adapter dalla DTO**; se manca, crea un adapter, **mai** un renderer standalone.

## 4. Conversione `MeepleCardGame` → adapter

**Principio.** L'interfaccia pubblica `MeepleCardGameProps` resta invariata (eccetto `compact`, vedi sotto) così che `shared-games-grid.tsx` e `page-client.tsx` non cambino. Il *body* diventa una composizione di `MeepleCard`. L'adapter risolve l'i18n (`labels`) in stringhe prima di passarle.

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

**Test.** `meeple-card-game.test.tsx` passa da asserzioni sul markup inline ad **asserzioni comportamentali** sul render canonico: titolo (`h3`), stelle via ruolo/aria-label, chip di connessione con count (toolkit/agent/kb), badge "new this week", attribution footer Wikidata quando presente. Coerente con l'acceptance-matrix #2859.

## 5. ESLint rule `local/no-standalone-card-renderer` (severità `error`)

**Obiettivo:** rendere strutturalmente difficile ricreare una card "copiando HTML", senza falsi positivi che impediscano il flip a `error`. Due controlli complementari nella stessa rule:

- **Controllo A — import-boundary (preciso).** Vietato importare da `**/meeple-card/parts/**` e `**/meeple-card/variants/**` da file **fuori** da `ui/data-display/meeple-card/`. Solo l'export pubblico `MeepleCard` è consumabile. → non puoi "rubare" i parts per riassemblare una card a mano.
- **Controllo B — compose-check (euristico).** Un componente il cui nome matcha `*Card`, definito sotto `components/` ma **fuori** dalle dir canoniche, deve rendere `<MeepleCard>` (o `<ExtraMeepleCard>`) nel proprio JSX.

**Esenzioni (perché `error` sia sicuro da subito):**

- Dir canoniche: `ui/data-display/meeple-card/` (i suoi `GridCard`/`ListCard`/… sono gli interni del dispatcher) e `ui/data-display/extra-meeple-card/` (tier detail: `GameExtraMeepleCard` ecc. legittimamente non compongono `MeepleCard`).
- Pattern non-card per nome: `*Skeleton*`, `*Empty*`, `*Error*`, `*Footer*`, `*Placeholder*` (stati/wrapper, non renderer di entità).
- **Allowlist esplicita** per path (stesso pattern di `SPREAD_ALLOWLIST` in `call-site-coverage.test.tsx`): eccezioni legittime future si aggiungono con motivazione in commit, senza degradare a `warn`.

**Confine con C4 (nessun overlap).** Questa rule è *dichiarazione/uso* (import + presenza di `<MeepleCard>`). C4 (#2861) farà il *body-inspection* AST (rileva `<Cover>`/glifi-stella/`<StatusBadge>` renderizzati inline anche senza import). Al merge di C1 il violatore reale (`MeepleCardGame`) non esiste più.

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

(La tabella completa nel doc include tutte le righe estratte dalla mappatura.)

**Test** → `card-decision-table.test.ts`:

- **No righe dangling:** ogni adapter citato nella tabella esiste ed è esportato (parse markdown → risolve import path).
- **Coverage:** ogni componente `*Card` adapter in produzione (esclusi gli esenti) compare in una riga della tabella → la mappa non può omettere un adapter senza rompere il build (guardia "la mappa non mente sul filesystem", stile test importPath del registry).

> **Consistenza guardie.** Il coverage test e il compose-check della ESLint rule (§5) devono condividere **la stessa definizione di "adapter `*Card`"** (identiche esenzioni per nome/dir). Per evitare divergenze, l'insieme delle esenzioni vive in un unico modulo condiviso (es. `apps/web/eslint-rules/card-renderer-exemptions.js` o equivalente importabile dal test), consumato da entrambi.

## 7. Strategia TDD (ordine)

1. `card-decision-table.md` + `card-decision-table.test.ts`: scrivo la tabella, il test la valida (rosso→verde).
2. ESLint rule: casi `RuleTester` prima (rosso: valid = adapter che compone / file esente; invalid = `*Card` che non compone + deep-import) → implementazione (verde) → registrazione `error` in `eslint.config.mjs`.
3. `MeepleCardGame`: aggiorno `meeple-card-game.test.tsx` al contratto comportamentale (rosso) → converto il body ad adapter (verde) → rimuovo `compact` da `MeepleCardGameProps` e aggiorno `shared-games-grid.tsx`.
4. Verifica finale: `pnpm test` (mirato meeple-card + shared-games), `pnpm typecheck`, `pnpm lint` (nuova rule attiva), `pnpm build`. **Gate a11y BLOCKING:** il render canonico usa già token AA (`--c-*` 38% + `--c-*-text`), rischio contrasto basso; verifica su `/shared-games`.

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
| Il compose-check (Controllo B) genera falsi positivi bloccando `error` | Esenzioni per nome/dir + allowlist per path; casi coperti da `RuleTester` prima del flip |
| Cambio visivo su route pubblica `/shared-games` | Deciso e accettato in brainstorm (look canonico + segnali mappati); delta dichiarati in §4 |
| Regressione a11y sul nuovo render | Token già AA; verifica esplicita su `/shared-games` nel gate BLOCKING |
| `connections` count-only rende chip senza popover diverso dall'atteso | ConnectionChipStrip footer con count e senza `items`/handler = chip statico con count, match funzionale del footer attuale |

## 10. Acceptance criteria

- [ ] `MeepleCardGame` compone `MeepleCard` (nessun `★`/cover/badge inline); interfaccia pubblica invariata eccetto `compact` rimosso.
- [ ] `/shared-games` renderizza via tier canonico; test comportamentali verdi; nessuna regressione a11y.
- [ ] `docs/for-developers/frontend/card-decision-table.md` presente; `card-decision-table.test.ts` verde (no-dangling + coverage).
- [ ] `local/no-standalone-card-renderer` attiva a `error`; `RuleTester` verde; `pnpm lint` pulito sul codebase.
- [ ] `pnpm test` mirato, `pnpm typecheck`, `pnpm lint`, `pnpm build` verdi.
