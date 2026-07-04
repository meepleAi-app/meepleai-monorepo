# Slice C2/C4 — Diary reale + Winner resolution sul night-live hub (#2633)

**Data**: 2026-07-04 · **Issue**: #2633 (SI-2, umbrella #2619) · **Predecessori**: Slice B (PR #2656), C1 (PR #2657), WS1 (PR #2665)
**Stato**: DRAFT per `/sc:spec-panel` · **Seam**: `NightLiveViewModel` (`apps/web/src/lib/game-nights/mapNightLive.ts`)

## 1. Contesto e discovery

Slice B ha reso `NightLiveClientView` data-driven dal read model `GET /game-nights/{id}/live`; C1 ha riempito `currentGame`. Restano **stub a `[]`** (mapper righe 194-196) i tre campi diary del seam — `diaryEvents`, `diaryGames`, `diaryPlayers` — e **mai popolato** il `winner` risolto su `PlannedGame` (solo il `winnerId` grezzo passa, LD-2). WS1 ha riabilitato l'avvio live delle partite, quindi ora **esistono dati reali** (almeno `game_started`/`night_started`) da renderizzare.

### Fatti accertati (discovery workflow, 3 reader)

**BE — due read path SEPARATI:**
- `GET /game-nights/{id}/live` → `GameNightLiveDto` (header + `GameNightSessionDto[]` + `isViewerOrganizer` + `plannedLineup`). **Nessun diary.** Winner = solo `Guid? WinnerId` per sessione. **Guardia participant** (organizer OR RSVP).
- `GET /game-nights/{id}/diary` → `GameNightDiaryDto(GameNightId, List<GameNightDiaryEntryDto>)`, dove `GameNightDiaryEntryDto(Id, SessionId, EventType, Description, Payload?, ActorId?, Timestamp)`. Legge cross-BC `SessionEvent WHERE GameNightId==id` ORDER BY Timestamp ASC. `Description` = label italiane emoji generate server-side. `ActorId` = `SessionEvent.CreatedBy` (UserId, non risolto).

**6 gap per C2/C4:**
1. **[SECURITY] Diary senza guardia** — `GetGameNightDiaryQuery(Guid GameNightId)` NON ha `CallerUserId` né participant-check; l'endpoint fa solo `RequireAuthenticatedUser` → **qualsiasi utente autenticato legge il diary di qualsiasi serata**. Il live query invece è participant-guarded. Esporre il diary nell'hub participant-scoped senza parità è una regressione di sicurezza.
2. **Diary non nel live DTO** — per un diary reale il FE deve consumare l'endpoint separato (secondo query/hook); nulla li fonde oggi.
3. **Grouping per-gioco** — le entry diary hanno `SessionId`, non `GameId`. Il `DiaryEvent.gameId` FE deve essere un **game id** (chiave contro `DiaryGameRef.id`). Il live DTO `sessions[]` ha già `sessionId→gameId→gameTitle` → **join FE possibile senza modifica BE**.
4. **Winner display** — solo `winnerId:GUID`; nessun display name. `WinnerId` è **ambiguo**: doc dice UserId, ma `SessionTests` lo setta con `Participant.Id` (guest-capable). Risolvere via RSVP (User-only) **perde i guest** (LD-2).
5. **Actor display** — `ActorId`=UserId non risolto; le avatar-per-attore nel diary richiedono un roster {id→name,initials,color}. Il live DTO non ha roster.
6. **Type mismatch** — live timing `DateTimeOffset?` vs diary `Timestamp DateTime` → normalizzare FE.

**FE — contratti già definiti, non alimentati:**
- Tipi diary (in `CrossGameDiaryTimeline.tsx`, ri-esportati dal barrel): `DiaryEvent{id,time:string,gameId:string|null,kind,icon:string,actors:string[],text}`; `DiaryEventKind='turn'|'score'|'custom'|'end'|'system'`; `DiaryGameRef{id,title,emoji}`; `DiaryPlayerRef{id,initials,color:number}` (color=HSL hue); `DiaryFilter='all'|'turn'|'score'|'custom'` (4 valori, `end`/`system` non filtrabili). `KIND_TO_ENTITY`: turn→session, score→player, custom→chat, end→event, system→toolkit.
- `PlannedGame` porta **sia** `winnerId?:string` **sia** `winner?:PlannedGameWinner{name,initials,color:number}` + `score?:string`. Chip winner rende su `isCompleted && game.winner` (oggi dead code).
- Util FE disponibili: `userHue(userId):number` + `userHsl(userId):string` (match esatto `color:number`+`hsl(h,60%,55%)`); `hashToHue`+`extractInitials` (ma `extractInitials` è tarato su titoli di gioco, **sbagliato per nomi persona** — nessun util initials-persona esiste).
- Mapper è **puro** (clock iniettato `now:Date`, no `Date.now()`/random/module-state). Nessuno schema Zod per il diary.

### Insight architetturale chiave
**C4 (winner display) è accoppiato a SI-3 #2634 (close strip che PRODUCE il winner).** Nessuna partita ha un winner finché il flusso di chiusura non esiste in UI. Renderizzare un winner oggi = dead code garantito. Inoltre #2634 definirà cosa `WinnerId` È (UserId vs Participant.Id), risolvendo il gap #4. → **sequencing naturale: C2 (diary) ora, C4 (winner) dentro/dopo #2634.**

## 2. Decisioni proposte (per il panel)

| # | Decisione | Opzione raccomandata | Alternative |
|---|-----------|---------------------|-------------|
| **D1 Scope** | C2 e C4 insieme? | **C2 (diary) ora; C4 (winner) rinviato a #2634** — winner è dead code senza il close flow, e #2634 disambigua `WinnerId`. | C2+C4 insieme (winner renderizza vuoto fino a #2634) |
| **D2 Diary source** | come alimentare il diary | **2° hook `useGameNightDiary` + mapper puro dedicato `mapDiary(diaryDto, liveSessions, now)`**; il view compone i due (live + diary). Il seam `NightLiveViewModel` NON cambia signature: i 3 campi diary si spostano su un secondo viewmodel `NightDiaryViewModel` iniettato nell'hub. | BE fonde il diary nel live DTO (1 query) — più lavoro BE, rompe la separazione dei due read path esistenti |
| **D3 [SECURITY] guardia diary** | fix in-slice? | **In-slice**: aggiungere `CallerUserId` a `GetGameNightDiaryQuery` + participant-check identico al live handler (403 non-participant). Prerequisito hard per esporre il diary. | Issue di sicurezza separata prima di C2 |
| **D4 Grouping** | SessionId→GameId | **Join FE-side**: `mapDiary` risolve `entry.SessionId → liveSessions.find().gameId` (il live DTO ha già la mappa). Zero modifica BE. | BE aggiunge `GameId`/`GameTitle` a `GameNightDiaryEntryDto` |
| **D5 Profondità diary C2** | actor avatars? | **Minimale**: timeline con `Description` server-side + grouping per-gioco + `DiaryGameRef` (emoji deterministica da gameId via `hashToHue`/placeholder). `DiaryEvent.actors=[]` + `diaryPlayers=[]` (nessuna avatar-per-attore) → **nessun roster BE**. | Full: risolvere `ActorId→{name,initials,color}` (richiede roster BE + endpoint partecipanti incl. guest) |
| **D6 Kind mapping** | EventType BE → DiaryEventKind | Mappa esplicita: `score_update/score→score`; `game_started/game_completed/night_started/night_finalized→end`; `note_added/photo/card_draw/dice_roll/pause_resume→custom`; `player_joined/dispute_resolved/resource_update→system`; default→`system`. `turn` non ha sorgente BE oggi. | — |
| **D7 icon/text** | icona ed etichetta | `text` = `Description` (già italiano emoji); `icon` = derivata dal kind (o estratta dal primo char emoji di `Description`). | — |
| **D8 Corrupted-safe** | entry malformata | Come Slice B (LD-4): schema Zod diary con enum esaustivo + una entry non-parsabile non fa fallire `.parse()` dell'array (skip con log, non throw). | — |

## 3. Scope raccomandato per C2 (questa slice)

**IN**: (a) fix sicurezza guardia diary [D3]; (b) `useGameNightDiary` hook + `GameNightDiaryDtoSchema` Zod; (c) `mapDiary` puro (join per-gioco [D4], kind mapping [D6], corrupted-safe [D8]) → `DiaryEvent[]` + `DiaryGameRef[]`; (d) wire nel view (secondo viewmodel) → `CrossGameDiaryTimeline` off-`[]`. **OUT** (→ #2634/dopo): winner resolution [D1], actor avatars/roster [D5-full], `turn` events, `score` sui planned rows.

## 4. Acceptance criteria (C2)

- **AC1** Non-participant → `GET /diary` risponde 403 (parità col live). Test integration.
- **AC2** Participant → il diary hub mostra le entry reali della serata in ordine cronologico, raggruppate per gioco (gameId risolto dal live sessions).
- **AC3** Entry con `SessionId` non presente in `liveSessions` → `gameId=null` (evento night-level), renderizzata senza crash.
- **AC4** Array diary con 1 entry malformata → le altre renderizzano (no throw), la malformata skippata.
- **AC5** `mapDiary` è puro/deterministico (table-test con `now` iniettato).
- **AC6** Serata senza eventi → empty-state diary definito (no crash, no spinner infinito).
- **AC7** Nessuna regressione su Slice B/C1 (live header/planned/currentGame invariati).

## 5. Test plan

- **FE unit** (Vitest): `mapDiary` table-tests (grouping, kind mapping, corrupted-skip, night-level null gameId, empty); `useGameNightDiary` (success/401→UnauthorizedError/403); schema parse (valido/malformato).
- **BE** (unit + integration Testcontainers): `GetGameNightDiaryQueryHandler` con `CallerUserId` — participant OK / non-participant 403 / organizer OK; parità guardia col live.
- **Regressione**: suite game-nights FE + il cluster BE GameNight.

## 6. Rischi / questioni aperte per l'utente

- **Q-SCOPE** [D1]: C2-only (diary) ora e C4 con #2634, oppure C2+C4 insieme?
- **Q-DEPTH** [D5]: diary minimale (text+grouping, no avatar-attore, zero BE roster) oppure full (actor avatars → richiede roster BE + endpoint partecipanti guest-capable)?
- **Q-SEC** [D3]: fix guardia diary in-slice (raccomandato) o come issue prerequisito separata?

---

## 7. Spec-Panel Verdict (4 esperti: Fowler / Nygard / Wiegers / Crispin)

> Il panel è unanime che il *framing* è corretto (C2 ora, mapper puro, corrupted-safe, Zod-at-the-wire) ma che il draft poggia su una **lettura incompleta del BE** che spedirebbe un'implementazione 500-ante o insicura. **3/4 reviewer hanno indipendentemente trovato lo stesso blocker #1 che il draft non aveva visto.**

### 🔴 Blocker #1 — Route collision (500 latente in prod, VERIFICATO in codice)
`GET /api/v1/game-nights/{id}/diary` è registrato **due volte** su `v1Api`: `GameNightEndpoints.cs:244` (GameManagement, envelope + `Description` italiana, **unguarded**) **e** `SessionFlowEndpoints.cs:246` (SessionTracking, `SessionEventDto[]` raw, **organizer-guarded**). I nomi param (`id`/`gameNightId`) sono irrilevanti per il matching ASP.NET → **`AmbiguousMatchException` a ogni GET**. Il diary GameNight **non ha mai funzionato**. Va risolto a UN endpoint canonico **prima** di qualsiasi guardia/FE, con un route-uniqueness startup test.

### 🔴 Blocker #2 — Hook `useGameNightDiary` già in prod
`apps/web/src/lib/domain-hooks/useGameNightDiary.ts` esiste (Zustand+SSE, `gameNightSessionClient.getDiary` → la rotta collisa, `JSON.parse(e.payload)` non protetto riga 39 → una entry malformata fa sparire l'INTERO diary). Va riconciliato/ritirato — **non** un secondo hook con lo stesso nome.

### Decisioni lockate (aggiorna §2)
| # | Verdetto | Forma lockata |
|---|----------|---------------|
| D1 | ✅ AGREE | C2 ora, C4 con #2634. **+** landare ORA un test pending/xfail che fissa la semantica di `WinnerId` (UserId vs `Participant.Id`) così #2634 non stranda i guest. |
| D2 | ❌ OVERTURNED | I 3 campi diary sono GIÀ su `NightLiveViewModel` (righe 50-52): impossibile "non cambiare signature" **e** spostarli. **Locked: RIMUOVERE `diaryEvents/diaryGames/diaryPlayers` da `NightLiveViewModel`**, `mapNightLive` resta live-only, **comporre live+diary in `NightLiveClientView`** (composition root) sulle prop 3-array esistenti dell'hub. Riconciliare l'hook già shipped. |
| D3 | ⚠️ REFINED | Non "aggiungi guardia": esiste già un twin organizer-only su rotta collisa. **Locked**: (1) risolvi la collisione a UN endpoint canonico; (2) applica UNA guardia participant-parity (organizer OR RSVP, come il live) con parità 404-missing/403-non-participant. |
| D4 | ✅ AGREE+refine | Join FE-side. **+** gate del grouping su live-sessions risolte (o accetta+documenta null transitorio); `Guid.Empty`/night-level/session-non-in-live → `gameId=null`; **invalidare la query diary insieme alla live** su session-start. |
| D5 | ✅ AGREE | Minimale, no roster. **Fix**: `DiaryGameRef.emoji` è string ma `hashToHue`→number: serve palette emoji deterministica/placeholder. Roster guest-capable risolto UNA volta in #2634 (sblocca D5-full + C4 insieme). |
| D6 | ❌ OVERTURNED (BLOCKING) | Le chiavi sono indovinate dallo switch `GenerateDescription` (display-side), non dagli **emitter** (write-side): reali `score_updated`/`turn_advanced`/`dice_rolled`/`session_paused` vs draft `score_update`/`dice_roll`/`pause_resume`; `game_started`/`night_started` **mai emessi**. **Locked**: enumerare la mappa dagli **emitter**; `turn_advanced→turn`; default→system nel mapper; AC che asserisce ogni tipo reale mappa al kind giusto (non default). |
| D7 | ❌ OVERTURNED | icona SOLO dalla mappa kind FE (mai `Description[0]` → surrogate pair → mojibake). Regola double-emoji esplicita. Se vince il `SessionEventDto` (senza `Description`), il FE deve generare label+i18n → budget reale. |
| D8 | ⚠️ REFINED | Per-row `safeParse` skip-with-log (mai `.parse()` array-level). **Overturn enum esaustivo su `eventType`**: resta `z.string()` aperto al wire; enum chiuso solo sul `DiaryEventKind` derivato FE (default→system). AC: evento con `eventType` non mappato **renderizza** (kind=system), non droppato. |

### Nuovi must-fix (oltre i 2 blocker): 
guard-scope divergence (organizer-only→participant non deve regredire consumer recap); race a due query (gate su live-resolved); **pagination truncation** (`ORDER BY Timestamp ASC` + `Take(200)` → una serata lunga perde gli eventi PIÙ RECENTI — decidere cap **e direzione DESC** in-slice); timezone skew (diary `DateTime` Kind Unspecified vs live `DateTimeOffset` → assume-UTC/append `Z`, table-test non-UTC); AC1 deve guidare la rotta HTTP reale (non handler-level).

### Q aperte — raccomandazioni panel
- **Q-SCOPE** → **C2-only ora** (unanime). + decidere cap/direzione in-slice.
- **Q-DEPTH** → **Minimale** (full bloccato sul roster guest = lavoro di #2634).
- **Q-SEC** → **In-slice, non-negoziabile, primo commit — riformulato come bug-fix**: risolvi la collisione + allarga la guardia, testato via pipeline HTTP reale.

### Disaccordo esperti
- **D3/Q-SEC**: Fowler la vedeva come semplice "aggiungi guardia"; Nygard/Wiegers/Crispin (maggioranza) la riformulano come **bug-fix prerequisito** (collisione+guardia), più grande. Risoluzione: maggioranza.
- **D6 vocabolario**: conflitto factuale display-switch vs emitter → **write-side/emitter è autoritativo**.

**Ship order**: (1) fix route collision + guardia participant, (2) riconcilia hook/DTO source-of-truth, (3) mapper puro sul vocabolario reale, (4) componi in `NightLiveClientView`.

## 8. Decisioni utente (2026-07-04, lockate)
- **Q-SCOPE/sequencing** → **UNA PR combinata C2** (fix infra collisione+guardia + render nello stesso diff BE+FE).
- **Canonical diary endpoint** → **GameManagement** (`GetGameNightDiaryQuery`, envelope + `Description` italiana server-side): aggiungi guardia participant, **ritira il twin `SessionFlowEndpoints.cs:246`**. FE usa `Description` come `text` (i18n non per-locale accettato per ora; il catalogo label FE è deferred).
- **Q-DEPTH** → **minimale** (no actor-avatar, `actors=[]`, `diaryPlayers=[]`; roster guest-capable = lavoro #2634).
- Adottate come default tutte le decisioni lockate del §7 (D2 rimozione stub VM + compose in view; D6 kind-map dagli emitter; D8 `eventType` open al wire).
