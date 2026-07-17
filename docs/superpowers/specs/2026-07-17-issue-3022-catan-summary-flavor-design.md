# #3022 — Catan SUMMARY flavor: design

- **Issue**: [#3022](https://github.com/meepleAi-app/meepleai-monorepo/issues/3022) — `[#2377 G6a-2] feat(session-live): Catan flavor SUMMARY view (deferred from #2787)`
- **Parent**: #2377 (G6 umbrella) · **Epic**: #2354 · **Enabler**: #3025 (per-game live game-state)
- **Branch**: `feature/issue-3022-catan-summary-flavor` (parent `main-dev`)
- **Data**: 2026-07-17

## 1. Contesto

La vista LIVE di Catan è già in produzione (#2787 / PR #3021). La vista SUMMARY (recap fine partita su `/sessions/[id]`) fu **deliberatamente rinviata** in #3022 perché porta debito che la LIVE non ha: il suo data-path passa da un DTO diverso (`GameSessionDto`, non `LiveSessionDto`) che non conosce lo slug del gioco né il game-state per-partita.

### Decisioni prese (brainstorming 2026-07-17)

| Decisione | Scelta | Conseguenza |
|---|---|---|
| **Scope MVP** | Presentational core | Hero vincitore + final standings (punteggi reali + colori) + KPI base (durata; turni/round se presenti). **Niente** board snapshot / dice-chart / trade-bars / breakdown 5-categorie / badge longest-road-largest-army. |
| **Fonte `gameSlug`** | Backend | `GameSessionDto` arricchito con `GameSlug`/`GameName` (nullable). |

Il mockup `admin-mockups/design_files/sp4-session-catan-summary.*` è marcato `design_intent: "deferred"` (fidelity.json, tracking #2234 — track Storybook già chiusa): vale come **guida di layout**, non come pixel-gate.

## 2. Fatti verificati dalla discovery

1. **I dati core del summary esistono già nel DTO, ma vengono scartati.** `GameSessionDto` (da `GET /api/v1/sessions/{id}`) espone `scoringType` + `scoreData` (punteggi reali per-player, JSON-encoded, formato #2389/#3080), `durationMinutes`, `players[].color`, `winnerName`. L'adapter `adaptGameSessionToDetails` (`SessionSummaryView.tsx:158-193`) li appiattisce a placeholder (`score = isWinner ? 1 : 0`, `duration = '—'`, `color` ignorato). Il summary flavor **bypassa** l'adapter e legge i campi grezzi.
2. **Manca `gameSlug`/`gameName`** su `GameSessionDto` (solo `GameId` Guid). Serve per il dispatch del flavor.
3. **Manca `gameState`** sul summary DTO: vive **solo** su `LiveSessionDto` (`live-sessions.schemas.ts:139`). → tutti gli elementi che richiedono lo stato per-partita sono fuori scope MVP.
4. **`FlavorRenderer` è cablato sul solo LIVE**: `FlavorView = 'live'`, `FlavorProps.session: LiveSessionDto`, e `CatanLiveFlavor` legge lo stato da `useLiveSessionStore`/SignalR — nulla di ciò esiste in pagina summary. Il summary ha bisogno di **props proprie**.
5. **Adapter riusabile già pronto**: `mapScoreDataToEndgameSummary(scoringType, scoreData, players)` (`lib/session-live/score-data-to-endgame-summary.ts`) → `FinalScoreEntry[]` (`{playerName, score, isWinner}`), gestisce i 4 `ScoreType`, ritorna `[]` su null, e mappa `players.map()` **preservando l'ordine** (output parallelo-per-indice all'input).
6. **Pattern BE per lo slug**: il LIVE deriva `GameSlug = Slugifier.Slugify(GameName)` (`QueryHandlers.cs:44`); `SharedGame` non ha un campo `Slug`. `GameName` = `SharedGame.Title`, risolvibile da `GameId` col pattern esistente `_db.SharedGames.Where(g => g.Id == gameId).Select(g => g.Title)`.

## 3. Architettura

### 3.1 Dispatcher summary gemello (non estensione del renderer live)

**Scelta**: un dispatcher parallelo e isolato, **non** una modifica a `FlavorRenderer`.

- **Motivo**: `FlavorProps.session` è `LiveSessionDto` e i flavor live dipendono da `useLiveSessionStore`/SignalR. Il summary consuma `GameSessionDto` e non ha store live. Rendere `FlavorRenderer` generico sulle props per-view toccherebbe codice condiviso da 7 flavor live → rischio sproporzionato per un MVP.
- **Componenti nuovi** (in `apps/web/src/components/features/session-live/`):
  - `SummaryFlavorRenderer.tsx` — dispatcher lazy gemello di `FlavorRenderer`.
  - `SummaryFlavorProps` — `{ session: GameSessionDto; className?: string }` (props proprie, disaccoppiate dal live).
  - `SUMMARY_FLAVOR_MAP` — `Record<string, LazyExoticComponent<...>>`, inizialmente `{ catan: CatanSummaryFlavorLazy }`.
  - `hasSummaryFlavor(slug: string | null | undefined): boolean` — per il mount condizionale.
- `FlavorRenderer.tsx` e i 7 flavor live **restano invariati**.

### 3.2 Data flow (FE, tutto sui campi grezzi del DTO)

```
GameSessionDto
 ├─ scoreData (JSON string) ──JSON.parse[try/catch → warn → null]──▶ oggetto polimorfico
 ├─ scoringType ──────────────────────────────────────────────────┐
 ├─ players[] (id, name, color) ──▶ AdapterPlayer[] ───────────────┤
 │                                                                  ▼
 │                                       mapScoreDataToEndgameSummary()
 │                                          → FinalScoreEntry[] (parallelo-per-indice a players)
 │                                                                  │
 │              zip color: standings[i].color = players[i].color ◀──┘
 │                                                                  ▼
 │                              standings ordinate (winner-first, poi score DESC)
 ├─ durationMinutes ──────────────────────────────────────────────▶ KPI durata
 └─ winnerName / playerCount ─────────────────────────────────────▶ hero + KPI
```

Lo zip per-indice è sicuro perché l'adapter costruisce l'output con `players.map(...)` senza riordinare (verificato, `score-data-to-endgame-summary.ts:49,67,78,97`).

## 4. Backend

### 4.1 `GameSessionDto`

Aggiungere due campi **nullable** in coda al record (`GameSessionDto.cs`):

```csharp
string? GameSlug = null,   // populated only on GET /api/v1/sessions/{id}
string? GameName = null    // Title from the shared-game catalog; null on list/history paths
```

Nullable perché il DTO è condiviso da molti endpoint (history, active, complete, abandon, pause, resume, end): solo il path a singola sessione li popola.

### 4.2 Popolamento (solo path GET singola sessione)

`GetGameSessionByIdQueryHandler.MapToDto` risolve `GameName`/`GameSlug`:
- Inietta accesso in lettura al catalogo `SharedGames` (via `DbContext` condiviso, come `UploadPdfCommandHandler.cs:141` — pattern già accettato nel codebase, oppure repository catalogo se disponibile — dettaglio del piano).
- `gameName = SharedGames.Where(g => g.Id == session.GameId).Select(g => g.Title).FirstOrDefault()`.
- `gameSlug = gameName is null ? null : Slugifier.Slugify(gameName)` (identico a `QueryHandlers.cs:44`).
- Se il gioco non è nel catalogo → entrambi `null` → il FE cade sul layout generico.

`GameSessionMapper.ToDto` (extension statico usato dagli **altri** endpoint) resta invariato: `GameSlug`/`GameName` restano `null` (nessun accesso al catalogo, nessun cambio di comportamento sugli altri path).

### 4.3 Schema Zod FE

`GameSessionDtoSchema` (`games.schemas.ts`) += `gameSlug: z.string().nullable().optional()`, `gameName: z.string().nullable().optional()`.

## 5. Frontend — `CatanSummaryFlavor`

- **Input**: `SummaryFlavorProps` (`session: GameSessionDto`).
- **Parsing**: `JSON.parse(session.scoreData)` in `try/catch`; su errore `console.warn` + trattamento come `null`.
- **Standings**: `mapScoreDataToEndgameSummary(session.scoringType, parsed, adapterPlayers)` + zip `color` per indice + ordinamento winner-first/score-DESC.
- **Render**:
  - **Hero**: avatar del vincitore (hue derivato da `player.color`), titolo `"{nome} vince!"`, punteggio del vincitore + `durationMinutes`.
  - **Standings strip**: per riga → posizione, `PlayerDot` color-coded, nome, barra di progresso, punteggio. Riuso di palette/atomi del pattern `CatanLiveFlavor`.
- **i18n**: label auto-costruite via `useIntl` (stesso pattern del flavor live).

### 5.1 Wire in `SessionSummaryView`

Additivo e a basso rischio: se `hasSummaryFlavor(session.gameSlug)` → monta `<SummaryFlavorRenderer session={dto} />` come **sezione** in cima al layout summary esistente; altrimenti la pagina resta invariata. L'MVP **non** rimuove il layout generico né introduce una nuova route (`/sessions/[id]/summary` con tab è fuori scope; il posizionamento fine è dettaglio del piano).

## 6. Error / null handling

| Condizione | Comportamento |
|---|---|
| `gameSlug` null / gioco senza flavor summary | `SummaryFlavorRenderer` → `null` → layout summary generico invariato |
| `scoringType`/`scoreData` null | adapter → `[]` → empty state gentile del flavor |
| `scoreData` malformed JSON | `try/catch` + `console.warn` → trattato come null |
| `player.color` null | `PlayerDot` con colore neutro di fallback |
| catalogo non risolve il gioco | `gameName`/`gameSlug` null dal BE → come "gameSlug null" |

## 7. i18n

Nuovo sottoalbero (naming da confermare nel piano, es. `features.sessionLive.catanSummary.*`) in `it.json` **e** `en.json`, coprendo hero/standings/KPI/empty. **Guard di parità** dedicato (stesso pattern di `i18n-gamedetail-keys.test.ts`, #3103).

## 8. Testing (TDD, RED-first)

- **FE unit `CatanSummaryFlavor`**: `scoreData` Points reale → hero mostra il vincitore, standings ordinate con colori; `scoreData` null → empty; JSON malformato → empty + `console.warn`.
- **FE unit `SummaryFlavorRenderer`**: dispatch `catan` → componente montato; slug sconosciuto/null → `null`.
- **FE `SessionSummaryView` wiring**: `gameSlug='catan'` → flavor montato; altro slug → non montato; nessuna regressione sul test esistente.
- **BE unit `GetGameSessionByIdQueryHandler`**: popola `GameSlug`/`GameName` dal catalogo; gioco assente → null. Verifica che `GameSessionMapper.ToDto` (altri path) lasci null.
- **Guard i18n**: nuove chiavi presenti e a parità IT/EN.

## 9. Fuori scope MVP (deferred — richiedono `gameState` end-game persistito)

Board snapshot (`HexBoard` finale), `DiceChart` (istogramma 2D6 storico), `TradeBars`, contatore mosse del ladro, "serie di produzione più lunga", "mano più grande", breakdown per le 5 categorie di scoring, badge `LongestRoad`/`LargestArmy` nelle standings. Nessuno di questi è alimentabile dal summary DTO attuale.

## 10. File toccati (checklist)

**Backend**
- `GameSessionDto.cs` — +`GameSlug?`, +`GameName?`
- `GetGameSessionByIdQueryHandler.cs` — DI catalogo + risoluzione slug/name
- (test) `GetGameSessionByIdQueryHandler` test

**Frontend**
- `components/features/session-live/SummaryFlavorRenderer.tsx` (nuovo: renderer + `SummaryFlavorProps` + `SUMMARY_FLAVOR_MAP` + `hasSummaryFlavor`)
- `components/features/session-live/flavors/catan/CatanSummaryFlavor.tsx` (nuovo, + eventuali sub-componenti hero/standings)
- `lib/api/**/games.schemas.ts` — Zod `gameSlug`/`gameName`
- `app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx` — wire condizionale
- `locales/it.json`, `locales/en.json` + guard i18n
- test FE (flavor, renderer, wiring, guard)
