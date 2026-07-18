# #3022 — Catan SUMMARY flavor: design (rev. 2 — scope VP reali / BE bridge)

- **Issue**: [#3022](https://github.com/meepleAi-app/meepleai-monorepo/issues/3022) — `[#2377 G6a-2] feat(session-live): Catan flavor SUMMARY view (deferred from #2787)`
- **Parent**: #2377 (G6 umbrella) · **Epic**: #2354 · **Enabler**: #3025 (per-game live game-state)
- **Branch**: `feature/issue-3022-catan-summary-flavor` (parent `main-dev`)
- **Data**: 2026-07-18 (rev. 2)

> **Rev. 2 changelog**: la rev. 1 assumeva "presentational FE-only con punteggi reali già sul summary DTO". Una review avversariale del piano l'ha smentita su due punti critici (§2). Scelta utente: **VP reali via BE bridge**. Questa revisione ridisegna il backend di conseguenza.

## 1. Contesto

La vista LIVE di Catan è in produzione (#2787). La SUMMARY (recap su `/sessions/[id]`) fu rinviata in #3022. Scope confermato: **presentational core con punteggi reali** — Hero vincitore + final standings (VP reali per-player + colori) + KPI durata. Fuori scope: board snapshot, dice-chart, trade-bars, breakdown 5-categorie, badge longest-road/largest-army (richiedono `gameState` end-game non persistito). Il mockup `sp4-session-catan-summary.*` è `design_intent: "deferred"` → guida di layout, non pixel-gate.

## 2. I due difetti critici che hanno ridisegnato il backend (verificati nel codice)

1. **`GET /api/v1/sessions/{id}` non popola `scoreData`/`scoringType`.** Il `GetGameSessionByIdQueryHandler.MapToDto` costruisce il DTO senza score; l'enrichment esiste **solo** sul path history (`GetSessionHistoryQueryHandler.cs:51-61` via `IHistorySessionScoreProvider`). → sul summary `scoreData` è sempre `null`.
2. **Identity mismatch dei player.** `scoreData.scores[].playerId` sono `SessionPlayerEntity.Id` (aggregato **LiveGameSession**, tabella `session_players`), mentre `GameSessionDto.Players` (`SessionPlayerDto`) espone solo `playerName/order/color` senza quell'id. → il join VP↔giocatore non ha chiave comune.

**Bridge risolto** (verificato end-to-end): i `playerId` in `scoreData` = `SessionPlayerEntity.Id`, che porta **anche** `DisplayName` e `Color`. Percorso: `GameSession.Id ← LiveGameSession.CorrelatedGameSessionId`; `LiveGameSession.Id = SessionPlayers.LiveGameSessionId`. Gli stessi `SessionPlayers` vivono sullo stesso `LiveGameSession` che il score-provider già attraversa → allineamento esatto, **nessun name-matching**.

## 3. Architettura

### 3.1 Backend — read-model esteso (una sola chiamata provider)

Nuovo metodo sul provider esistente (`IHistorySessionScoreProvider`), che lascia `GetScoresAsync` (history list) **invariato**:

```csharp
Task<SessionScoreboard?> GetScoreboardAsync(Guid gameSessionId, CancellationToken ct);

internal readonly record struct SessionScoreboard(
    string ScoringType,
    string ScoreData,
    IReadOnlyList<ScorePlayerReadModel> Players);

internal readonly record struct ScorePlayerReadModel(Guid Id, string DisplayName, string Color);
```

Implementazione (mirror del join esistente `HistorySessionScoreProvider.cs:35-49`, + proiezione dei `SessionPlayers`):
1. `LiveGameSessions.Where(CorrelatedGameSessionId == gameSessionId)` join `SessionTrackingSessions on TrackingSessionId == track.Id` → `(live.Id, track.ScoringType, track.ScoreData, updatedAt)`, prendi il più recente. Se nessuno → `null`.
2. `SessionPlayers.Where(LiveGameSessionId == live.Id).Select(p => new ScorePlayerReadModel(p.Id, p.DisplayName, p.Color))`.
3. `new SessionScoreboard(scoringType, scoreData, players)`.

### 3.2 Backend — DTO + handler

- `GameSessionDto` (record) += (tutti nullable, popolati **solo** su GET singola sessione):
  - `string? GameSlug`, `string? GameName` (via `IGameCoreDataProvider` + `Slugifier.Slugify`).
  - `IReadOnlyList<ScorePlayerDto>? ScorePlayers` con `internal record ScorePlayerDto(Guid Id, string DisplayName, string? Color)`.
  - `ScoringType`/`ScoreData` **esistono già** (default null): ora popolati dal scoreboard.
- `GetGameSessionByIdQueryHandler` inietta `IGameCoreDataProvider` + `IHistorySessionScoreProvider`. Dopo aver caricato la session: risolve slug/name; chiama `GetScoreboardAsync(session.Id, ct)`; se presente, popola `ScoringType`/`ScoreData`/`ScorePlayers`.
- `GameSessionMapper.ToDto` (altri endpoint) **invariato**: i nuovi campi restano null.

### 3.3 Frontend — flavor

- Zod `GameSessionDtoSchema` += `gameSlug`/`gameName` (nullable optional) + `scorePlayers: z.array(ScorePlayerDtoSchema).nullable().optional()`, con `ScorePlayerDtoSchema = { id: string, displayName: string, color: string|null }`.
- Dispatcher summary **gemello** isolato (`SummaryFlavorRenderer`, props `{ session: GameSessionDto }`) — non tocca il `FlavorRenderer` live (tipizzato su `LiveSessionDto` + `useLiveSessionStore`).
- `CatanSummaryFlavor` usa **`session.scorePlayers`** (id+displayName+color, allineati a scoreData) come master-list:
  - `buildCatanSummaryStandings(scoringType, scoreData, scorePlayers)` → `mapScoreDataToEndgameSummary(scoringType, parsed, scorePlayers.map(p => ({id, name: displayName})))` + zip `color` per indice.
  - **Winner hero**: preferisce `session.winnerName` (BE-autoritativo, coerente col resto del summary); fallback alla row `isWinner`; **nessun auto-crowning** quando né `winnerName` né una `isWinner` esistono.
  - **Durata**: `session.durationMinutes`.

### 3.4 Data flow

```
GET /sessions/{id}
  handler → GetScoreboardAsync(session.Id)  ─┐
          → IGameCoreDataProvider(gameId)    ─┤→ GameSessionDto {
                                               │    scoringType, scoreData,        (from scoreboard)
                                               │    scorePlayers[{id,displayName,color}],
                                               │    gameSlug, gameName, winnerName, durationMinutes, players }
FE SessionSummaryView (status==='Completed' && hasSummaryFlavor(gameSlug))
  → SummaryFlavorRenderer → CatanSummaryFlavor(session)
      buildCatanSummaryStandings(scoringType, scoreData, scorePlayers)
        → mapScoreDataToEndgameSummary (join playerId↔scorePlayers[].id) → FinalScoreEntry[]
        → zip color per indice → sort winner-first, score DESC
      hero: winnerName ?? row.isWinner ?? (none)   standings: rows con color
```

## 4. Error / null handling

| Condizione | Comportamento |
|---|---|
| `gameSlug` null / non-Catan | `SummaryFlavorRenderer` → `null` → layout generico invariato |
| `status !== 'Completed'` (incl. `Abandoned`) | flavor **non montato** (no winner-hero su partite non concluse) |
| `scoreData`/`scoringType`/`scorePlayers` null (nessuna live correlata) | adapter → `[]` → empty state gentile del flavor |
| `scoreData` malformed JSON | `try/catch` + `console.warn` → trattato come null |
| standings presenti ma nessun `isWinner` e `winnerName` null | standings senza hero-winner (nessun auto-crown) |
| `player.color` null | `PlayerDot` colore neutro (`CATAN_NEUTRAL_HSL`) |

## 5. i18n

Sottoalbero `pages.sessionSummary.flavor.catan.{winnerTemplate,vpUnit,durationTemplate,standingsTitle,empty}` in `it.json` **e** `en.json`, con **guard di parità**. Template con placeholder usano l'interpolazione ICU nativa di `t(key, { name })` (non `.replace` manuale).

## 6. Testing (TDD, RED-first)

- **BE unit `GetScoreboardAsync`** (provider): test integration (Testcontainers/InMemory) — seed `LiveGameSession`(CorrelatedGameSessionId)+`SessionTrackingSession`(scoreData)+`SessionPlayers` → asserisce `SessionScoreboard` con score + players allineati (id == scoreData playerId). Real pipeline, non fixture DTO.
- **BE unit handler** (mock provider + core-data): popola scoringType/scoreData/scorePlayers/gameSlug/gameName; scoreboard null → campi null; gioco assente → slug/name null. Aggiornare i 6 test esistenti al nuovo ctor. + test `GameSessionMapper.ToDto` lascia i nuovi campi null (guard altri path).
- **FE `buildCatanSummaryStandings`**: Points reale (join per id) → standings ordinate + color; scorePlayers vuoto → `[]`; scoreData null → `[]`; JSON malformato → `[]` + warn; tie a punteggio max → ordine deterministico; scoringType sconosciuto → `[]`.
- **FE `CatanSummaryFlavor`**: hero da winnerName; nessun isWinner + winnerName null → nessun auto-crown; empty su standings vuote; color null → dot neutro.
- **FE `SummaryFlavorRenderer`**: dispatch catan→component, unknown/null→null.
- **FE wiring `SessionSummaryView`**: `gameSlug='catan'` + `status='Completed'` → montato; non-Catan → non montato; `status!='Completed'` → non montato; nessuna regressione. Harness IntlProvider con `onError={() => {}}` (precedente `FlavorRenderer.test.tsx:21`).

## 7. Fuori scope MVP (deferred)

Board snapshot, `DiceChart`, `TradeBars`, robber-move counter, longest-production-run, biggest-hand, breakdown 5-categorie, badge longest-road/largest-army. Tutti richiedono `gameState` end-game persistito, assente sul summary path.

## 8. File toccati (checklist)

**Backend**
- `IHistorySessionScoreProvider.cs` — +`GetScoreboardAsync`, +`SessionScoreboard`, +`ScorePlayerReadModel`
- `HistorySessionScoreProvider.cs` — impl `GetScoreboardAsync`
- `GameSessionDto.cs` — +`GameSlug?`, +`GameName?`, +`ScorePlayers?`, +`record ScorePlayerDto`
- `GetGameSessionByIdQueryHandler.cs` — +2 dipendenze, popolamento
- (test) provider integration + handler unit + mapper guard

**Frontend**
- `lib/api/schemas/games.schemas.ts` — Zod `gameSlug`/`gameName`/`scorePlayers`
- `components/features/session-live/SummaryFlavorRenderer.tsx` (nuovo)
- `components/features/session-live/flavors/catan/catan-summary-standings.ts` (nuovo)
- `components/features/session-live/flavors/catan/CatanSummaryFlavor.tsx` (nuovo)
- `app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx` — wire (status-gated)
- `locales/it.json`, `locales/en.json` + guard i18n
- test FE (builder, flavor, renderer, wiring, guard)
