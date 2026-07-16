# Issue #3006 + #3007 — /toolkit/history & /library/wishlist: i18n + fidelity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Portare `/toolkit/history` (#3006) e `/library/wishlist` (#3007) a parità dei mockup `sp4-toolkit-history` e `sp4-library-wishlist`, con localizzazione completa (react-intl `it`/`en`), sui dati realmente esposti dal backend.

**Architecture:** Due pagine indipendenti sullo stesso feature branch, un solo PR che chiude entrambe le issue. Ogni pagina è un client component con fetch React-Query esistente; filtri/sort/paginazione sono **client-side** (il backend non li supporta). I campi mockup senza dato reale (Score/Turni/Timeline/Highlights per history; MetaBox categoria/players/durata/BGG per wishlist) sono degradati graziosamente o omessi, documentati come gap-dati backend. Le stringhe entrano in nuovi namespace `pages.toolkitHistory.*` e `pages.library.wishlist.*`.

**Tech Stack:** Next.js 16 (App Router), React 19, TypeScript, Tailwind 4 + shadcn/ui primitives, react-intl v7 (hook `@/hooks/useTranslation`), React Query (TanStack), Zod, Vitest + Testing Library.

## Global Constraints

- **i18n obbligatorio**: ZERO stringhe user-facing hardcoded. Ogni stringa via `t('pages.…')`. Aggiornare SEMPRE **entrambi** `apps/web/src/locales/it.json` e `apps/web/src/locales/en.json` con le stesse chiavi. Chiave mancante in un locale = fail.
- **Token semantici only** (ESLint `local/no-hardcoded-color-utility` = error): `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, entity utilities (`text-entity-game`, ecc.). Vietati `bg-white`/`text-gray-*`/`bg-slate-*`. `text-white` ammesso solo con bg colorato dichiarato nella stessa className.
- **BGG asset ban (freeze #2123)**: nessuna richiesta browser a host BGG/geekdo. Non aggiungere immagini/cover da BGG. (Nessuna delle due pagine deve caricare cover esterne.)
- **Card**: usare `MeepleCard` (`@/components/ui/data-display/meeple-card`), mai `GameCard`/`PlayerCard` deprecati.
- **Data-driven only**: NON inventare campi non presenti nei DTO. Vedi tabelle "gap-dati" in ogni parte.
- **Priority values**: lowercase `high|medium|low` end-to-end (DTO `priority: string`, mockup, i18n key suffix).
- **Test culture**: `dotnet`/vitest culture-independent; formattazione numeri/date via `formatNumber`/`formatDate` di react-intl (locale-aware), non `toLocaleString` hardcoded.
- **Branch**: `feature/issue-3006-3007-history-wishlist-i18n-fidelity` (parent `main-dev`). Commit atomici `feat|fix|test|docs(scope): …`. Un PR → `main-dev`.

---

## File Structure

### Parte A — /toolkit/history (#3006)

- Modify: `apps/web/src/app/(authenticated)/toolkit/history/client.tsx` — orchestratore pagina (fetch, stato filtri, view toggle, render tabella/cards/modal).
- Create: `apps/web/src/app/(authenticated)/toolkit/history/_lib/history-filters.ts` — funzioni pure: filtro (search/game/date/winner), sort, paginazione, derive coop, parse `scoreData`.
- Create: `apps/web/src/app/(authenticated)/toolkit/history/_components/HistoryToolbar.tsx` — search + multiselect gioco + date range + multiselect vincitore + sort + view toggle + summary.
- Create: `apps/web/src/app/(authenticated)/toolkit/history/_components/HistoryTable.tsx` — tabella desktop 8 colonne sortabile.
- Create: `apps/web/src/app/(authenticated)/toolkit/history/_components/HistoryCards.tsx` — card stack (view cards + mobile).
- Create: `apps/web/src/app/(authenticated)/toolkit/history/_components/HistoryPagination.tsx` — prev/numeri/next + page-size selector + meta.
- Create: `apps/web/src/app/(authenticated)/toolkit/history/_components/HistoryDetailModal.tsx` — modal desktop / bottom-sheet mobile via `Drawer`.
- Create: `apps/web/src/lib/utils/csv.ts` — `escapeCSVField`, `downloadFile`, `rowsToCsv` (estratti/promossi da `lib/utils/export.ts` privato).
- Create: `apps/web/src/app/(authenticated)/toolkit/history/__tests__/history-filters.test.ts`
- Create: `apps/web/src/app/(authenticated)/toolkit/history/__tests__/page.test.tsx`
- Modify: `apps/web/src/locales/it.json` + `en.json` — namespace `pages.toolkitHistory.*`.

### Parte B — /library/wishlist (#3007)

- Modify: `apps/web/src/app/(authenticated)/library/wishlist/page.tsx` — orchestratore (fetch, stato filtri, stats, dialog add/edit).
- Create: `apps/web/src/app/(authenticated)/library/wishlist/_lib/wishlist-filters.ts` — funzioni pure: filtro (search/priority), sort (5 opzioni), stats (TOTAL_SPEND, PRIO_COUNTS).
- Create: `apps/web/src/app/(authenticated)/library/wishlist/_components/WishlistToolbar.tsx` — search + chip priorità multiselect + sort popover + summary.
- Create: `apps/web/src/app/(authenticated)/library/wishlist/_components/WishlistStats.tsx` — hero stats (giochi, alta priorità, spesa stimata).
- Modify: `apps/web/src/components/wishlist/MeepleWishlistCard.tsx` — i18n + border-left Alta + badge cliccabile + added-at + edit action.
- Modify: `apps/web/src/components/wishlist/AddToWishlistDialog.tsx` → rinominare concettualmente in dialog add/edit (prop `mode`): combobox gioco, radio-chip priorità, prezzo €, note counter 200, i18n.
- Create: `apps/web/src/app/(authenticated)/library/wishlist/__tests__/wishlist-filters.test.ts`
- Modify/Create: test per `MeepleWishlistCard`, `AddToWishlistDialog`, `page`.
- Modify: `apps/web/src/locales/it.json` + `en.json` — namespace `pages.library.wishlist.*`.

### Parte C — Docs

- Modify: `admin-mockups/MOCKUPS_INDEX.md` — riga `sp4-library-wishlist.html` + Summary counts.

---

## PARTE A — /toolkit/history (#3006)

**Gap-dati backend** (da `GameSessionDto`, `apps/web/src/lib/api/schemas/games.schemas.ts:86-127`): disponibili `id, gameId, status, startedAt, completedAt, playerCount, players[].{playerName,playerOrder,color}, winnerName, notes, durationMinutes, scoringType?, scoreData?`. NON disponibili: `gameName` (solo `gameId` → lookup), `players[].score`, `winScore`, `turns`, timeline, varianza, best, highlights. Colonna Score e stats-modal: tentare parse `scoreData` (JSON string) → se assente/non-parsabile mostrare `—`.

**API** (`apps/web/src/lib/api/clients/sessionsClient.ts:82-119`): `api.sessions.getHistory({ gameId?, startDate?, endDate?, limit?, offset? })` → `{ sessions: GameSessionDto[], total, page, pageSize }` (⚠ `total` = size pagina, non conteggio reale). Strategia: fetch batch ampio (`limit: 500`, offset 0) e fare **tutto client-side** (filtro/sort/paginazione). Se il batch è pieno (500) mostrare nota "primi 500".

**gameName lookup**: usare `useLibrary()` (map `gameId → gameTitle`) come già fa la wishlist page; per sessioni di giochi non in libreria fallback `t('pages.toolkitHistory.table.unknownGame')` = "Gioco sconosciuto". (Alternativa catalogo `api.games` opzionale — YAGNI: iniziare con library map.)

### Task A1: CSV util condiviso

**Files:**
- Create: `apps/web/src/lib/utils/csv.ts`
- Test: `apps/web/src/lib/utils/__tests__/csv.test.ts`

**Interfaces:**
- Produces: `escapeCSVField(field: string | number | null | undefined): string`, `rowsToCsv(headers: string[], rows: (string|number|null)[][]): string`, `downloadFile(content: string, filename: string, mimeType?: string): void`

- [ ] **Step 1: Failing test** — `csv.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { escapeCSVField, rowsToCsv } from '../csv';

describe('escapeCSVField', () => {
  it('quotes fields containing comma, quote or newline', () => {
    expect(escapeCSVField('a,b')).toBe('"a,b"');
    expect(escapeCSVField('he said "hi"')).toBe('"he said ""hi"""');
    expect(escapeCSVField('line1\nline2')).toBe('"line1\nline2"');
  });
  it('passes through plain fields and stringifies numbers/null', () => {
    expect(escapeCSVField('plain')).toBe('plain');
    expect(escapeCSVField(42)).toBe('42');
    expect(escapeCSVField(null)).toBe('');
  });
});

describe('rowsToCsv', () => {
  it('joins headers + rows with CRLF and escapes each cell', () => {
    const csv = rowsToCsv(['A', 'B'], [['x', 'y,z'], [1, null]]);
    expect(csv).toBe('A,B\r\nx,"y,z"\r\n1,');
  });
});
```
- [ ] **Step 2: Run → FAIL** (`pnpm vitest run src/lib/utils/__tests__/csv.test.ts`) — module not found.
- [ ] **Step 3: Implement** `csv.ts` (promuovi il pattern privato di `lib/utils/export.ts`): `escapeCSVField` (quota se contiene `,"\n`, raddoppia `"`, `null/undefined→''`, numeri→String), `rowsToCsv` (map escape, join cella `,`, join riga `\r\n`), `downloadFile` (Blob + `URL.createObjectURL` + anchor `download` + `revokeObjectURL`).
- [ ] **Step 4: Run → PASS**.
- [ ] **Step 5: Commit** `feat(utils): add shared CSV export helpers`.

### Task A2: Funzioni pure filtro/sort/paginazione history

**Files:**
- Create: `apps/web/src/app/(authenticated)/toolkit/history/_lib/history-filters.ts`
- Test: `apps/web/src/app/(authenticated)/toolkit/history/__tests__/history-filters.test.ts`

**Interfaces:**
- Consumes: `GameSessionDto` (`@/lib/api/schemas/games.schemas`).
- Produces:
  - `type HistorySort = 'recent' | 'oldest' | 'longest' | 'score'`
  - `type DateRangePreset = 'all' | 'last30' | 'last90' | 'lastYear' | 'custom'`
  - `interface HistoryFilterState { search: string; gameIds: string[]; winners: string[]; datePreset: DateRangePreset; dateFrom?: string; dateTo?: string; sort: HistorySort }`
  - `interface HistoryRow` (view-model): `{ id; gameId; gameName: string; startedAt: string; durationMinutes: number; playerNames: string[]; playerCount: number; winnerName: string | null; isCoop: boolean; winScore: number | null; notes: string | null }`
  - `parseWinScore(dto: GameSessionDto): number | null`
  - `toHistoryRow(dto, gameNameMap: Map<string,string>, unknownLabel: string): HistoryRow`
  - `filterRows(rows: HistoryRow[], f: HistoryFilterState, now: Date): HistoryRow[]`
  - `sortRows(rows: HistoryRow[], sort: HistorySort): HistoryRow[]`
  - `paginate<T>(items: T[], page: number, pageSize: number): T[]`
  - `countActiveFilters(f: HistoryFilterState): number`

- [ ] **Step 1: Failing tests** — coprire almeno:
```ts
// filterRows: search matcha gameName, winnerName, playerNames (case-insensitive)
// filterRows: gameIds filtra per gioco; winners filtra ('__nowin__' = winnerName null)
// filterRows: datePreset last30 esclude > 30 giorni fa (usare now fisso)
// sortRows: recent = startedAt desc; longest = durationMinutes desc; score = winScore desc (null in fondo)
// paginate: page 2 pageSize 20 ritorna item 20..39
// parseWinScore: scoreData JSON con punteggi → max; assente/invalid → null
// countActiveFilters: search+1 game+date custom → conta correttamente
```
(Scrivere ogni caso con `now = new Date('2026-07-15T12:00:00Z')` fisso; NON usare `Date.now()`.)
- [ ] **Step 2: Run → FAIL**.
- [ ] **Step 3: Implement** `history-filters.ts` con le funzioni pure. `isCoop` derivato: `winnerName == null && players.length > 1` (euristica documentata). `parseWinScore`: `try JSON.parse(scoreData)`; supporta shape `{playerId: number}` o `number[]` → `Math.max`; catch → null.
- [ ] **Step 4: Run → PASS**.
- [ ] **Step 5: Commit** `feat(toolkit-history): pure filter/sort/paginate helpers`.

### Task A3: i18n namespace `pages.toolkitHistory`

**Files:** Modify `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`.

**Interfaces:** Produces le chiavi consumate da A4-A8. Struttura (valori IT verbatim dal mockup; EN = traduzione equivalente):

```
pages.toolkitHistory:
  hero: { breadcrumbToolkit:"Toolkit", breadcrumbHistory:"History", title:"Storico sessioni",
          subtitle:"Tutte le partite finalizzate, filtrabili per gioco, data e vincitore.",
          quickStat:"{sessions} sessioni · {games} giochi · {winners} vincitori",
          exportCsv:"Esporta CSV", exportCsvShort:"CSV" }
  tabs: { stats:"Stats", history:"History", templates:"Templates", play:"Play" }
  filters:
    searchPlaceholder:"Cerca per gioco o vincitore…", searchAriaLabel:"Cerca per gioco o vincitore",
    searching:"Cercando…", clearSearch:"Cancella ricerca",
    games:"Giochi", filterByGame:"Filtra per gioco", all:"Tutti",
    showMore:"Mostra altri {n}", showLess:"Mostra meno",
    date:"Data", dateRange:"Intervallo date",
    dateOptions: { all:"Tutte le date", last30:"Ultimi 30 giorni", last90:"Ultimi 90 giorni",
                   lastYear:"Ultimo anno", custom:"Date custom" },
    dateFrom:"Da", dateTo:"A", dateFromLabel:"Data inizio", dateToLabel:"Data fine", applyRange:"Applica intervallo",
    winners:"Vincitori", filterByWinner:"Filtra per vincitore", winnerWon:"{name} (vinto)", noWinner:"Senza vincitore",
    sort:"Ordina", sortBy:"Ordina per",
    sortOptions: { recent:"Più recente", oldest:"Più antica", longest:"Più lunga", score:"Score più alto" },
    view:"Vista", viewTable:"Vista tabella", viewCards:"Vista cards"
  summary: { activeFilters:"{n, plural, one {# filtro attivo} other {# filtri attivi}}",
             results:"{n, plural, one {# sessione} other {# sessioni}} su {total}", clearAll:"Cancella tutto" }
  table: { date:"Data", game:"Gioco", duration:"Durata", players:"Giocatori", winner:"Vincitore",
           score:"Score", notes:"Note", actions:"Azioni", coop:"Cooperativa", coopShort:"co-op",
           noWinner:"Senza vincitore", noNote:"Nessuna nota", hasNote:"Nota presente",
           viewDetails:"Vedi dettagli", gameStats:"Stats del gioco", unknownGame:"Gioco sconosciuto",
           openInLibrary:"Apri {game} in libreria", winnerAria:"Vincitore: {name}",
           playersAria:"{count} giocatori: {names}" }
  pagination: { prev:"Prec.", next:"Succ.", prevAria:"Pagina precedente", nextAria:"Pagina successiva",
                mobileMeta:"Pag. {page} di {pages} · {total} sess.",
                rangeMeta:"{from}–{to} di {total} sessioni", empty:"0 sessioni",
                perPage:"Per pagina", perPageAria:"Sessioni per pagina", listAria:"Pagine storico sessioni" }
  empty: { title:"Nessuna sessione ancora",
           body:"Le partite finalizzate appariranno qui: filtra per gioco, data o vincitore e apri il dettaglio di ognuna.",
           cta:"Crea prima sessione" }
  filteredEmpty: { title:"Nessuna sessione corrisponde ai filtri",
                   body:"Prova a modificare la ricerca o a rimuovere i filtri di gioco, data o vincitore attivi.",
                   cta:"Rimuovi filtri" }
  loading: { ariaLabel:"Caricamento storico sessioni" }
  error: { message:"Impossibile caricare lo storico — riprova.", retry:"Riprova" }
  modal: { title:"{game} · {date}", sub:"Durata {duration} · {count} giocatori · {time}", close:"Chiudi",
           leaderboard:"Classifica finale", you:" · tu", timeline:"Timeline eventi",
           notes:"Note", notesEmpty:"Nessuna nota per questa sessione.",
           stats:"Statistiche partita", turns:"Turni totali", winnerScore:"Score vincitore",
           variance:"Varianza score", highlights:"Highlights", noData:"—",
           gameStats:"Stats gioco", playAgain:"Gioca di nuovo", editNotes:"Modifica note", delete:"Elimina" }
  cards: { coop:"Co-op" }
  batchNote:"Mostrati i primi {n} risultati."
```

- [ ] **Step 1**: inserire il blocco sotto `pages` in `it.json` (JSON valido, virgole corrette).
- [ ] **Step 2**: inserire lo stesso alberello con valori EN in `en.json` (es. title "Session History", subtitle "All finalized games, filterable by game, date and winner.", ecc.).
- [ ] **Step 3: Verify** — `pnpm vitest run` del test locale parità chiavi se esiste, altrimenti `node -e "JSON.parse(require('fs').readFileSync('apps/web/src/locales/it.json'))"` per validità. (Se esiste un test di parità it/en, deve passare.)
- [ ] **Step 4: Commit** `feat(i18n): add pages.toolkitHistory catalog (it/en)`.

### Task A4: HistoryTable (desktop, 8 colonne sortabili)

**Files:** Create `_components/HistoryTable.tsx`; test in `__tests__/HistoryTable.test.tsx`.

**Interfaces:**
- Consumes: `HistoryRow`, `HistorySort` (A2); `t` (useTranslation); primitives `Table*` (`@/components/ui/data-display/table`), `EntityChip`, `Button`, `Badge`.
- Produces: `<HistoryTable rows={HistoryRow[]} sort={HistorySort} onSortChange={(s)=>void} onOpenDetail={(row)=>void} onOpenGameStats={(gameId)=>void} />`

- [ ] **Step 1: Failing test** — render con 2 righe: verifica header i18n (`t('pages.toolkitHistory.table.game')`), click su header "Durata" chiama `onSortChange('longest')` con toggle asc/desc, riga coop mostra `co-op`, riga senza winner mostra `—`, click riga → `onOpenDetail`. Mock `useTranslation` con passthrough id.
- [ ] **Step 2: Run → FAIL**.
- [ ] **Step 3: Implement** tabella: colonne Data (`formatDate`+`formatTime`+rel), Gioco (`EntityChip entity="game"`), Durata (`{h}h {m}m`), Giocatori (avatar stack iniziali + `+N`, `aria-label` playersAria), Vincitore (coop → coop label; null → `—`; else chip 🏆), Score (`winScore ?? —`; coop → coopShort), Note (bottone 📝, dimmed se null), Azioni (👁 viewDetails + 📊 gameStats). Header sortabili con `aria-sort`. Token semantici + entity utilities.
- [ ] **Step 4: Run → PASS**.
- [ ] **Step 5: Commit** `feat(toolkit-history): HistoryTable desktop`.

### Task A5: HistoryCards (view cards + mobile)

**Files:** Create `_components/HistoryCards.tsx`; test.

**Interfaces:** `<HistoryCards rows={HistoryRow[]} onOpenDetail={(row)=>void} />` — usa `MeepleCard entity="session"` o layout ad-hoc card (chead GameChip+rel, cmid avatar+winner+durata, cfoot score pill + data + note flag).

- [ ] **Step 1: Failing test** — render 1 riga, verifica gameName + winner label + click → onOpenDetail.
- [ ] **Step 2: Run → FAIL**. **Step 3: Implement**. **Step 4: PASS**. **Step 5: Commit** `feat(toolkit-history): HistoryCards stack`.

### Task A6: HistoryPagination

**Files:** Create `_components/HistoryPagination.tsx`; test.

**Interfaces:** `<HistoryPagination page total pageSize onPageChange={(p)=>void} onPageSizeChange={(s)=>void} />` — page-size `[10,20,50,100]`, meta `rangeMeta`, mobile `mobileMeta`, ellissi numeri (riuso logica `CatalogPagination` se utile, ma con i18n `pages.toolkitHistory.pagination.*` e page-size selector aggiunto).

- [ ] **Step 1: Failing test** — total 156, pageSize 20, page 3 → meta "41–60 di 156 sessioni"; click Succ. → onPageChange(4); cambio select → onPageSizeChange(50).
- [ ] **Step 2-4** TDD. **Step 5: Commit** `feat(toolkit-history): HistoryPagination with page-size`.

### Task A7: HistoryToolbar

**Files:** Create `_components/HistoryToolbar.tsx`; test.

**Interfaces:** `<HistoryToolbar state={HistoryFilterState} onChange={(next)=>void} gameOptions={{id,label,count}[]} winnerOptions={{value,label,count}[]} view={'table'|'cards'} onViewChange totalCount resultCount onClearAll onExport />`. Composizione: search (debounce 400ms locale, spinner "Cercando…"), multiselect Giochi (Popover + Checkbox, `showMore` dopo 4), date range (preset radio + custom inputs), multiselect Vincitori (`__nowin__` + `showMore` 5), sort (Select), view toggle (table/cards), summary bar condizionale (`countActiveFilters>0`).

- [ ] **Step 1: Failing test** — digitare nel search chiama onChange con search aggiornato (dopo debounce, usare `vi.useFakeTimers`); toggle chip gioco aggiorna `gameIds`; summary mostra `activeFilters` quando ci sono filtri; Cancella tutto → onClearAll.
- [ ] **Step 2-4** TDD (test focalizzati sui comportamenti chiave, non ogni pixel). **Step 5: Commit** `feat(toolkit-history): HistoryToolbar filters`.

### Task A8: HistoryDetailModal (desktop dialog / mobile bottom-sheet)

**Files:** Create `_components/HistoryDetailModal.tsx`; test.

**Interfaces:** `<HistoryDetailModal row={HistoryRow | null} onClose />` — usa `Drawer` (`@/components/ui/drawer/drawer`) con `side="auto"` (bottom su mobile, right/dialog su desktop) O `Dialog` + `BottomSheet`. Sezioni: header (title/sub i18n), Classifica finale (da `players` + winnerName; score da parse scoreData o `—`), Note (readonly textarea o notesEmpty), Statistiche partita (Turni/Varianza/Highlights → `noData` "—" se assenti, documentato gap-dati). Timeline eventi **omessa** (nessun dato) — non renderizzare la sezione.

- [ ] **Step 1: Failing test** — row con notes → mostra note; row senza notes → notesEmpty; sezione stats mostra "—" per turns. onClose su close button.
- [ ] **Step 2-4** TDD. **Step 5: Commit** `feat(toolkit-history): HistoryDetailModal with graceful data degradation`.

### Task A9: Orchestratore client.tsx + export CSV + page test

**Files:** Modify `client.tsx`; test `__tests__/page.test.tsx`.

**Interfaces:** Consuma A1-A8. Stato: `useState<HistoryFilterState>`, `view`, `page`, `pageSize`, `detailRow`. Fetch `useQuery(['toolkit-history'], () => api.sessions.getHistory({ limit: 500 }))` + `useLibrary()` per gameNameMap. Pipeline: `dtos → toHistoryRow → filterRows → sortRows → paginate`. Header con Export CSV (usa `rowsToCsv`+`downloadFile`, colonne = header tabella i18n). Empty vs filteredEmpty in base a `countActiveFilters`. Loading skeleton, error banner con retry (`refetch`).

- [ ] **Step 1: Failing test** — mock `api.sessions.getHistory` (2 sessioni) + `useLibrary`; render → titolo `Storico sessioni` (via i18n mock), tabella con 2 righe; empty state quando getHistory ritorna `[]`; error state quando query rejecs. Riferimento pattern: `toolkit/stats/__tests__/page.test.tsx`, `sessionsClient.test.ts`.
- [ ] **Step 2: Run → FAIL**.
- [ ] **Step 3: Implement** orchestratore, rimuovendo TUTTE le stringhe EN hardcoded esistenti.
- [ ] **Step 4: Run → PASS** + `pnpm typecheck` + `pnpm lint` puliti su questi file.
- [ ] **Step 5: Commit** `feat(toolkit-history): #3006 full fidelity + i18n page`.

---

## PARTE B — /library/wishlist (#3007)

**Gap-dati backend** (da `WishlistItemDto`, `apps/web/src/lib/api/schemas/wishlist.schemas.ts`): disponibili `id, userId, gameId, gameName?, priority, targetPrice, notes, addedAt, updatedAt, visibility`. NON disponibili: `emoji` cover, `category`, `players`, `duration`, `bgg` rating → **MetaBox del mockup NON implementabile** (omettere la sezione). `TOTAL_SPEND`/`PRIO_COUNTS` calcolabili client-side. Edit end-to-end già supportato (`useUpdateWishlistItem` + `UpdateWishlistItemRequest.{clearTargetPrice,clearNotes}`).

**gameName**: `WishlistItemDto.gameName?` opzionale; fallback lookup via `useLibrary()` (già in page), poi `t('…card.unknownGame')`.

### Task B1: Funzioni pure wishlist filtro/sort/stats

**Files:** Create `wishlist/_lib/wishlist-filters.ts`; test `__tests__/wishlist-filters.test.ts`.

**Interfaces:**
- Consumes: `WishlistItemDto`.
- Produces:
  - `type WishlistSort = 'priority' | 'recent' | 'oldest' | 'alpha' | 'price'`
  - `type Priority = 'high' | 'medium' | 'low'`
  - `const PRIORITY_RANK: Record<Priority, number>` = `{high:0, medium:1, low:2}`
  - `interface WishlistFilterState { search: string; priorities: Priority[]; sort: WishlistSort }`
  - `filterItems(items, f, gameNameMap): WishlistItemDto[]` (search su gameName+notes; priorities multi)
  - `sortItems(items, sort): WishlistItemDto[]`
  - `computeStats(items): { total: number; highCount: number; totalSpend: number; priorityCounts: Record<Priority, number> }`
  - `countActiveFilters(f): number`

- [ ] **Step 1: Failing tests** — search matcha gameName+notes; priorities filtra; sort priority = rank poi addedAt desc; sort price = targetPrice desc (null in fondo); computeStats somma targetPrice saltando null, conta priorità. `now` fisso dove serve.
- [ ] **Step 2: Run → FAIL**. **Step 3: Implement**. **Step 4: PASS**. **Step 5: Commit** `feat(wishlist): pure filter/sort/stats helpers`.

### Task B2: i18n namespace `pages.library.wishlist`

**Files:** Modify `it.json` + `en.json`.

**Interfaces:** Produces chiavi per B3-B7. Struttura (IT verbatim dal mockup):

```
pages.library.wishlist:
  hero: { breadcrumbLibrary:"Libreria", breadcrumbWishlist:"Wishlist",
          title:"Wishlist · Giochi desiderati",
          subtitle:"Tieni traccia dei giochi che vuoi comprare o provare.",
          tabsAria:"Sezioni libreria", tabLibrary:"Libreria", tabWishlist:"Wishlist",
          addCta:"Aggiungi alla wishlist", addCtaShort:"Aggiungi" }
  stats: { games:"{count} giochi", highPriority:"{count} alta priorità", estimatedSpend:"spesa stimata {amount}" }
  filters:
    searchPlaceholder:"Cerca per nome gioco o note…", searchAriaLabel:"Cerca per nome gioco o note",
    searching:"Cercando…", clearSearch:"Cancella ricerca",
    priorityGroupAria:"Filtra per priorità (selezione multipla)", all:"Tutti",
    sort:"Ordina", sortBy:"Ordina per", sortAria:"Ordina wishlist",
    sortOptions: { priority:"Per priorità", recent:"Più recenti", oldest:"Più vecchi", alpha:"Alfabetico", price:"Prezzo target" }
  summary: { activeFilters:"{n, plural, one {# filtro attivo} other {# filtri attivi}}",
             results:"{n, plural, one {# gioco} other {# giochi}} su {total}", clearAll:"Cancella filtri" }
  priority: { high:"Alta", medium:"Media", low:"Bassa",
              filterAria:"Priorità {label} — filtra per questa priorità" }
  card: { target:"Target", noNote:"Nessuna nota", addedAt:"Aggiunto {when}", addedTitle:"Aggiunto il {date}",
          edit:"Modifica", editAria:"Modifica wishlist item {name}", remove:"Rimuovi",
          removeAria:"Rimuovi {name} dalla wishlist", openGameAria:"Apri dettaglio gioco {name}",
          unknownGame:"Gioco senza nome", gridAria:"Wishlist giochi" }
  dialog: { addTitle:"Aggiungi alla wishlist", editTitle:"Modifica wishlist",
            addSub:"Salva un gioco che vuoi comprare o provare", editSub:"{name} · priorità {priority}",
            close:"Chiudi", error:"Errore aggiungendo alla wishlist.", retry:"Riprova",
            gameLabel:"Gioco", gameComboPlaceholder:"Cerca per nome o incolla un game ID…",
            gameComboAria:"Cerca gioco da aggiungere", gameRemoveAria:"Rimuovi gioco selezionato",
            priorityLabel:"Priorità",
            priceLabel:"Target price (€)", pricePlaceholder:"65",
            priceHint:"Prezzo massimo che sei disposto a spendere — opzionale.",
            priceAria:"Prezzo massimo che sei disposto a spendere",
            notesLabel:"Note", notesPlaceholder:"es. acquisto entro Q3, voglio aspettare la nuova edizione, ecc.",
            notesCounter:"{len} / 200",
            cancel:"Annulla", submitting:"Aggiungo…", success:"Aggiunto", add:"Aggiungi", save:"Salva" }
  empty: { noItems: { title:"Nessun gioco nella wishlist",
                      body:"Aggiungi giochi che ti interessano per non perderli di vista — con priorità, prezzo target e note.",
                      cta:"Aggiungi il primo" },
           filtered: { title:"Nessun gioco corrisponde ai filtri",
                       body:"Prova a cambiare priorità o a modificare la ricerca per nome o note.",
                       cta:"Cancella filtri" } }
  loading: { ariaLabel:"Caricamento wishlist" }
  error: { message:"Impossibile caricare la wishlist — riprova.", retry:"Riprova" }
```

- [ ] **Step 1-2**: inserire in `it.json` e `en.json` (EN equivalente: title "Wishlist · Wanted games", ecc.).
- [ ] **Step 3: Verify** JSON valido + parità chiavi.
- [ ] **Step 4: Commit** `feat(i18n): add pages.library.wishlist catalog (it/en)`.

### Task B3: MeepleWishlistCard i18n + fidelity

**Files:** Modify `apps/web/src/components/wishlist/MeepleWishlistCard.tsx`; test `__tests__/MeepleWishlistCard.test.tsx`.

**Interfaces:** `<MeepleWishlistCard item={WishlistItemDto} gameName? onRemove={(id)=>void} onEdit={(item)=>void} onFilterPriority={(p)=>void} />`. Rimuovere `formatPriorityItalian` hardcoded → `t('pages.library.wishlist.priority.'+priority)`. Aggiungere: border-left Alta (`border-l-2 border-l-entity...`/semantic danger), badge priorità cliccabile (onFilterPriority), Target price con `€` (`formatNumber`), added-at relativo (`formatRelativeTime`/`formatDate`), azione Edit (onEdit). NO MetaBox (gap-dati).

- [ ] **Step 1: Failing test** — badge mostra label i18n; click badge → onFilterPriority(priority); click edit → onEdit(item); click remove → onRemove(id); item priority high ha border-left.
- [ ] **Step 2-4** TDD. **Step 5: Commit** `feat(wishlist): MeepleWishlistCard i18n + priority filter + edit`.

### Task B4: AddEditWishlistDialog

**Files:** Modify `apps/web/src/components/wishlist/AddToWishlistDialog.tsx`; test.

**Interfaces:** `<AddToWishlistDialog mode={'add'|'edit'} item?={WishlistItemDto} open? onOpenChange? trigger? prefillGameId? />`. Add usa `useAddToWishlist`; edit usa `useUpdateWishlistItem` (id + `UpdateWishlistItemRequest`, con `clearTargetPrice`/`clearNotes` quando svuotati). Campi: gioco (combobox ricerca su `useLibrary` games; in edit read-only chip), priorità radio-chip (`RadioGroup` stilizzato), prezzo € (`Input type=number`), note textarea `maxLength=200` + counter i18n. Titoli/sub/footer i18n via B2. Toast conferma via `sonner` (`success`).

- [ ] **Step 1: Failing test** — mode add: submit chiama useAddToWishlist con {gameId,priority,targetPrice,notes}; mode edit: precompila da item, submit chiama useUpdateWishlistItem con clearTargetPrice quando prezzo svuotato; counter note aggiorna; canSubmit false senza gioco/priorità.
- [ ] **Step 2-4** TDD (mock hooks). **Step 5: Commit** `feat(wishlist): unified add/edit dialog with combobox + radio-chip + i18n`.

### Task B5: WishlistStats + WishlistToolbar

**Files:** Create `_components/WishlistStats.tsx`, `_components/WishlistToolbar.tsx`; test.

**Interfaces:**
- `<WishlistStats stats={ReturnType<typeof computeStats>} />` — riga `games · highPriority · estimatedSpend` (i18n, `formatNumber` per €).
- `<WishlistToolbar state onChange priorityCounts totalCount resultCount onClearAll />` — search debounce, chip priorità multiselect (`ToggleGroup type="multiple"` o chip ad-hoc con count + colore priorità), sort popover (`Popover` + radio), summary condizionale.

- [ ] **Step 1: Failing test** — Stats mostra spesa formattata; Toolbar toggle chip aggiorna priorities; sort popover cambia sort; Cancella filtri → onClearAll.
- [ ] **Step 2-4** TDD. **Step 5: Commit** `feat(wishlist): WishlistStats + WishlistToolbar`.

### Task B6: Orchestratore page.tsx

**Files:** Modify `apps/web/src/app/(authenticated)/library/wishlist/page.tsx`; test `__tests__/page.test.tsx`.

**Interfaces:** Consuma B1-B5. Stato `useState<WishlistFilterState>`, `dialogState {mode, item}`. Fetch `useWishlist()` + `useLibrary()`. Pipeline `items → filterItems → sortItems`, `computeStats(items)`. Render: hero+breadcrumb+tabs, WishlistStats, addCta (apre dialog add), WishlistToolbar, grid `MeepleWishlistCard` (onEdit → dialog edit, onRemove → useRemoveFromWishlist, onFilterPriority → set priorities), empty.noItems vs empty.filtered (in base a countActiveFilters), loading skeleton, error banner. Rimuovere TUTTE le stringhe EN.

- [ ] **Step 1: Failing test** — mock useWishlist (2 item) + useLibrary; render → title `Wishlist · Giochi desiderati`; stats corretti; empty.noItems quando 0 item; filter riduce le card; error state.
- [ ] **Step 2: Run → FAIL**. **Step 3: Implement**. **Step 4: PASS** + typecheck + lint. **Step 5: Commit** `feat(wishlist): #3007 full fidelity + i18n page`.

---

## PARTE C — Docs + chiusura

### Task C1: MOCKUPS_INDEX.md

**Files:** Modify `admin-mockups/MOCKUPS_INDEX.md`.

- [ ] **Step 1**: aggiungere nella sezione SP4 (ordine alfabetico, dopo `sp4-game-detail-tab-*`) la riga:
```
| `sp4-library-wishlist.html` | page-mock | `/library/wishlist` — personal wishlist (priority Alta/Media/Bassa, target price, notes); filters + sort + add/edit dialog. Issue #1491 |
```
- [ ] **Step 2**: aggiornare la tabella `## Summary`: `page-mock` +1, `Total` +1.
- [ ] **Step 3: Commit** `docs(mockups): index sp4-library-wishlist → /library/wishlist`.

### Task C2: Verifica finale + PR

- [ ] **Step 1**: full test suite mirata: `pnpm vitest run` sui path toccati (toolkit/history, library/wishlist, wishlist components, csv, locales). Baseline pulita (0 nuovi fail vs main-dev).
- [ ] **Step 2**: `pnpm typecheck && pnpm lint && pnpm lint:tokens` puliti; verificare nessuna stringa EN residua (`grep` euristico su `toolkit/history`, `wishlist`).
- [ ] **Step 3**: verifica visiva con dev server **fresco** (NON il container Docker stantìo): `pnpm --dir apps/web dev` su porta alternativa, screenshot `/toolkit/history` e `/library/wishlist` vs mockup. (Oppure attendere rebuild container.)
- [ ] **Step 4**: commento su #3007 documentando che il redirect era già risolto (fix 2026-07-11, `next.config.js:229`) e che questa PR copre il residuo i18n/fidelity + index.
- [ ] **Step 5**: push + PR → `main-dev`, `Closes #3006, Closes #3007`, con changelog dei gap-dati backend documentati (Score/Turni/Timeline/Highlights history; MetaBox wishlist) come possibili follow-up.

---

## Self-Review

**Spec coverage:**
- #3006 i18n → A3, A9 (rimozione stringhe EN). ✅
- #3006 fidelity (tabella/sort/filtri/paginazione/export/mobile/modal) → A2,A4,A5,A6,A7,A8,A9. ✅
- #3007 i18n → B2, B3, B4, B6. ✅
- #3007 fidelity (stats/filtri/sort/card/add-edit dialog/empty) → B1,B3,B4,B5,B6. ✅
- #3007 doc (MOCKUPS_INDEX) → C1. ✅
- Redirect #3007 → già risolto upstream (documentato in C2 step 4). ✅
- Gap-dati documentati → header Parte A/B + C2 step 5. ✅

**Placeholder scan:** i valori i18n sono verbatim; i test-block chiave sono espliciti; per i componenti UI di dettaglio i test sono "focalizzati sui comportamenti chiave" (giustificato dalla mole — non ogni pixel). Nessun "TODO/TBD".

**Type consistency:** `HistoryFilterState`/`HistorySort`/`HistoryRow` usati coerentemente A2→A4-A9; `WishlistFilterState`/`WishlistSort`/`Priority`/`PRIORITY_RANK` coerenti B1→B3-B6. `parseWinScore`, `computeStats`, `countActiveFilters` firmati una volta e riusati.

**Vincolo noto:** "fidelity completa" è limitata dai dati backend (documentato). Non è un placeholder ma una scelta di scope approvata.
