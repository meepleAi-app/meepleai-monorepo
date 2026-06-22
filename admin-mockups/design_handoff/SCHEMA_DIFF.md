# Schema Diff — Mockup `data.js` ↔ Backend EF Core entities

> Generato dallo **Step 5** di `QUICK_START.md` (riadattamento prompt **0.3** di `BACKEND_PROMPTS.md`).
> Data: 2026-05-24 · Scope: solo lettura (zero migrations eseguite).
> Revisione `/sc:spec-panel` applicata: aggiunti § 0 (Confidence level UPFRONT), § 7.5 (GameNightEvent spot-check), § 13.5 (Notification spot-check), errata § 7 (Location vs LocationDescription).

## ⚠️ 0. Confidence level di questo documento — LEGGERE PRIMA DI USARLO

**Risultato spot-check 2026-05-24**: ho aperto **3 file `.cs`** su ~60 entity totali → confidence calibrata per sezione:

| Sezione | Entity BE | Confidence | Reasoning |
|---|---|---|---|
| § 1 — Game | `SharedGame.cs` (aggregate root in `Domain/Aggregates/`) + value objects | 🟢 **HIGH** (post Step-1 review) | **File aperto e verificato spot-check** 2026-05-24 PM — 4 nomi colonna BE corretti (vedi errata § 1 table): YearPublished, ComplexityRating, AverageRating, PlayingTimeMinutes |
| § 2 — Player | `User.cs` (Auth BC) + `RecordPlayer.cs` + `LiveSessionPlayer.cs` | 🔴 **LOW** | 0 file aperti, mapping inferenziale 100% |
| § 3 — Session | 4+ entity con "Session" nel nome | 🔴 **LOW** | 0 file aperti, ambiguità struttura BE non risolta |
| § 4 — Agent | `AgentConfiguration.cs` + family KB BC | 🔴 **LOW** | 0 file aperti |
| § 5 — KB Document | `PdfDocumentEntity.cs` + `VectorDocument.cs` | 🔴 **LOW** | 0 file aperti |
| § 6 — Chat | `ChatThread.cs` + `ChatSession.cs` + `ConversationMemory.cs` | 🔴 **LOW** | 0 file aperti |
| § 7 — Event (GameNight) | `GameNightEvent.cs` (aggregate root) | 🟢 **HIGH** | **File aperto e verificato spot-check** — ✅ **correzioni applicate al diff inferenziale originale** (vedi § 7.5) |
| § 8 — Toolkit | 2 BC paralleli (`GameToolbox` vs `GameToolkit`) | 🔴 **LOW** | 0 file aperti — ambiguità BC non risolta |
| § 9 — Tool | `ToolboxTool.cs` (in `GameToolbox`) | 🔴 **LOW** | 0 file aperti |
| § 10 — HouseRule | `HouseRule.cs` (in `AgentMemory.Domain.Models/`) | 🟡 **LOW-MEDIUM** | Path confermato via Glob (file Models, non Entities — VO probabile), schema interno non letto |
| § 11 — GameNight | → § 7 | 🟢 **HIGH** | Aggregato in § 7 |
| § 12 — RSVP | `GameNightRsvp.cs` + DTO | 🟡 **MEDIUM** | File non aperto, ma collocazione (sotto `GameNightEvent/`) confermata via Glob |
| § 13 — Notification | `Notification.cs` (aggregate root in `UserNotifications.Domain.Aggregates/`) + 4 satellite | 🟢 **HIGH** | **File aperto e verificato spot-check** — ✅ vedi § 13.5 |
| § 14 — UserPreferences | `UserPreferences.cs` (in `SystemConfiguration.Domain.Entities/`) | 🟡 **MEDIUM** | Path confermato via Glob, file non aperto |

**Conseguenze pratiche**:
- ✅ **Usa § 7, § 7.5, § 13, § 13.5 con confidence alta** (file aperti).
- ⚠️ **Per § 1, § 4, § 5, § 8, § 9, verifica field-by-field** aprendo l'entity prima del primo touch BE. Nomi colonna proposti sono **ipotesi**, non specifica autoritativa.
- 🔴 **Per § 2 Player, § 3 Session, § 6 Chat**: presenza di multi-entity (4+ Session, multi-Player, multi-Chat) richiede consult con maintainer per scegliere canonical PRIMA di scrivere migrations o endpoint.

**Disclaimer originale (preservato)**: tutti i caveats originariamente in § 17 sono ora qui in cima. Il documento è **una specifica di lavoro**, non un contratto autoritativo. Per la migrazione real fare `dotnet ef migrations script` in dev.

## Premessa di metodo

Il prompt 0.3 originale assumeva uno stack **Prisma**. Il backend MeepleAI è **.NET 9 / EF Core 9.0.11 / PostgreSQL 16 + pgvector**. L'analogo qui è:

- _A. Source mockup_ → `admin-mockups/design_handoff/data.js` (9 entity types con shape JS) + 5 entity aggiuntive citate nel prompt 0.2 (`HouseRule`, `GameNight`, `RSVP`, `Notification`, `UserPreferences`).
- _B. Source backend_ → `apps/api/src/Api/BoundedContexts/**/Domain/Entities/*.cs` + `**/Domain/Aggregates/*.cs` + `**/Domain/Models/*.cs` + DTO `**/Application/DTOs/*.cs` (60+ entities mappate via EF Core, 18 BCs DDD).

Questo audit fa il diff **shape-level** (nome campo + tipo logico + cardinalità + status discriminator). _Non_ apre le migrations: per la diff field-by-field a livello SQL fare `dotnet ef migrations script` in dev.

Le entity BE sono mappate **multi-aggregato** per ognuna delle 9 entity mockup. Esempio: `Game` mockup ≠ singola entity BE — vive come `SharedGame` + `PrivateGame` + `UserLibraryEntry` + multi value-object (`GameMechanic`, `GamePublisher`, `GameDesigner`, …). Il mockup ha un view unificato che _aggrega_ campi da molteplici source — i diff sotto evidenziano la sorgente verosimile.

## Convenzioni

| Simbolo | Significato |
|---|---|
| ✅ | Campo presente in entrambi (mockup + BE), tipo compatibile, mapping diretto |
| 🔁 | Campo presente in entrambi, **rinominato** (mockup name ≠ BE name) |
| ⚠️ | Campo presente in entrambi, **tipo diverso** o **cardinalità diversa** o **derived** |
| 🆕 | Solo nel **mockup**, manca BE → potenziale migration o solo-UI |
| 🗃️ | Solo nel **BE**, manca dal mockup → ok, BE può continuare a usarlo |
| 🧮 | **Derived/computed** (non da memorizzare; calcolato runtime) |

## 1. `Game` (mockup `g-*`) ↔ `SharedGame` + multi

**BE BC sorgente**: `SharedGameCatalog` + `UserLibrary` (per status `owned`/`wishlist`)

| Mockup field | Tipo mockup | BE field (entity / colonna) | Status |
|---|---|---|---|
| `id` | string `g-azul` | `SharedGame.Id : Guid` | 🔁 (string slug client-side vs Guid BE — _attenzione_: id mockup non sono UUID, sono slug; vedi NOTA-A) |
| `type` | const `'game'` | _none — implicit by entity_ | 🆕 UI discriminator |
| `title` | string `'Azul'` | `SharedGame.Title : string` | ✅ |
| `publisher` | string `'Plan B Games'` | `_publishers : List<GamePublisher>` many-to-many nav. property ✅ **verified** (Step-1 spot-check) | ⚠️ Mockup string singolo (UI display first), BE many-to-many embedded collection |
| `year` | number `2017` | `SharedGame.YearPublished : int` ✅ **verified** (Step-1 spot-check) | 🔁 **ERRATA**: NOT "Year" — corretto `YearPublished` |
| `author` | string `'Michael Kiesling'` | `_designers : List<GameDesigner>` many-to-many nav. property ✅ **verified** | ⚠️ Mockup string singolo, BE many-to-many. UI display = first/joined |
| `players` | string `'2–4'` | `SharedGame.MinPlayers : int` + `SharedGame.MaxPlayers : int` ✅ **verified** | ⚠️ UI format `'min–max'`, BE due colonne. **Derived** |
| `duration` | string `'30–45m'` (range) | `SharedGame.PlayingTimeMinutes : int` ✅ **verified** (single int, **NOT range**) | 🔁 **ERRATA**: NOT "MinDurationMinutes/MaxDurationMinutes" — solo `PlayingTimeMinutes` single. UI deve fakearne il range o cambiare mockup format |
| `weight` | number `1.77` (BGG complexity) | `SharedGame.ComplexityRating : decimal?` ✅ **verified** | 🔁 **ERRATA**: NOT "Complexity" — corretto `ComplexityRating` |
| `rating` | number `7.8` (BGG rating) | `SharedGame.AverageRating : decimal?` ✅ **verified** | 🔁 **ERRATA**: NOT "BggRating" — corretto `AverageRating` (generico, può aggregare community + BGG) |
| `stars` | number `4` (display) | _derived from rating_ | 🧮 UI computed |
| `cover` | string `linear-gradient(...)` | `SharedGame.CoverImageUrl : string?` OR `CoverGradient : string?` | 🆕 / ⚠️ Mockup usa gradient CSS; BE probabile usa imageUrl (BGG-sync). _Display fallback_ |
| `coverEmoji` | string `'🔷'` | _none_ | 🆕 UI-only fallback |
| `status` | discriminated `'owned' \| 'wishlist'` | `UserLibraryEntry.Status` OR `WishlistItem` exists | 🔁 BE separa entity per status (UserLibraryEntry per `owned`, WishlistItem per `wishlist`). Mockup unifica |
| `badge` | string `'In Libreria' \| 'Top 10' \| 'Wishlist' \| ...` | _none_ (derived) | 🧮 UI derived from status + community signals |
| `kbState` | `'indexed' \| 'partial' \| 'initial'` | derived from `VectorDocument` count + indexing status | 🧮 UI derived |
| `totalPlays` | number `23` | `PlayRecord` count per user/game | 🧮 Aggregated query |
| `winRate` | number `0.65` | derived from `PlayRecord.Winner` count | 🧮 Aggregated |
| `avgScore` | number `72` | derived from `ScoreEntry.Score` avg per game | 🧮 Aggregated |

**Diagnostic**:
- 🆕 `coverGradient` UI fallback non persistito (ok).
- 🆕 `coverEmoji` UI fallback (ok).
- 🧮 `stars` / `badge` / `kbState` / `totalPlays` / `winRate` / `avgScore` sono **derivati** — il BE non li memorizza ma li espone via aggregati. Verificare presenza di endpoint `GET /api/v1/games/{id}?include=stats` per evitare round-trip multipli FE.

> **NOTA-A — Id slug mockup**: il mockup usa string slug human-readable (`g-azul`, `p-marco`, `s-azul-live`). Il BE usa `Guid`. Decisione FE: continuare con slug human-readable nei dev/test fixtures OK; in produzione i client devono passare `Guid` reale. Il regola from `README.md:80`: "Usa ID short tipo `gn-saturday-3`, `p-marco`, `kb-wingspan-rules`". → ok mantenere slug in mock data, ma _i contratti API restano Guid_.

---

## 2. `Player` (mockup `p-*`) ↔ `User` + `RecordPlayer` + `LiveSessionPlayer`

**BE BC sorgente**: `Authentication` (User) + `GameManagement` (RecordPlayer, LiveSessionPlayer)

| Mockup field | Tipo mockup | BE field | Status |
|---|---|---|---|
| `id` | string `p-marco` | `User.Id : Guid` | 🔁 slug vs Guid |
| `type` | const `'player'` | implicit | 🆕 UI |
| `title` | string `'Marco R.'` | `User.DisplayName : string` (probabile) | 🔁 title → DisplayName |
| `subtitle` | string `'Membro Gen 2025 · 89 partite'` | derived: `User.CreatedAt` + count partite | 🧮 UI computed |
| `cover` | gradient string | _none_ | 🆕 UI fallback |
| `coverEmoji` | `'👤'` | _none_ | 🆕 UI |
| `initials` | `'MR'` | derived from DisplayName | 🧮 UI computed |
| `color` | number hue `262` | _none_ | 🆕 UI-only (per-player accent hue). Verificare se BE ha `User.AccentHue : int?` (probabilmente no) |
| `status` | `'active' \| 'idle'` | derived from `User.LastSeenAt` recency | 🧮 UI heuristic |
| `badge` | `'Pro' \| 'Veterana' \| 'Rising' \| ...` | derived from `Badge` + `UserBadge` BC | 🧮 / 🗃️ (BE ha `Badge.cs` + `UserBadge.cs` in SharedGameCatalog) |
| `totalWins` | number `47` | aggregated from `PlayRecord` where winner=user | 🧮 |
| `totalSessions` | number `89` | aggregated from `Session` count by participant | 🧮 |
| `winRate` | number `0.528` | derived | 🧮 |
| `fav` | string `'Azul'` | derived: most-played `Game.Title` from PlayRecord aggregation | 🧮 |

**Diagnostic**:
- 🆕 `color` (hue numerico per-player) e `coverEmoji` non persistiti BE. UI computa da hash di `User.Id` o `DisplayName` (verificare logica esistente in `entity-tokens.ts` / `entity-color`).
- Tutti i numeri (totalWins, sessions, winRate, fav) sono **aggregated queries** — BE deve esporre endpoint `GET /api/v1/players/{id}?include=stats` o `GET /api/v1/users/{id}/stats`. Verificare se `dashboardClient.ts` o `sessionStatisticsClient.ts` copre già.

---

## 3. `Session` (mockup `s-*`) ↔ ⚠️ 4 entity BE distinte

**BE BC sorgenti**: 4 entity con "Session" nel nome:
1. `Authentication.Domain.Entities.Session.cs` (auth sessions — cookie/JWT)
2. `SessionTracking.Domain.Entities.Session.cs` (game session aggregate root)
3. `GameManagement.Domain.Entities.GameSession.cs` (mock-friendly alias?)
4. `UserLibrary.Domain.Entities.GameSession.cs` (library-side session reference)
5. `GameManagement.Domain.Entities.LiveGameSession.cs` (in-play live state)

**Mapping primario probabile**: mockup `s-*` ↔ `SessionTracking.Session` (aggregate root con state + events + participants).

| Mockup field | Tipo mockup | BE field (SessionTracking.Session) | Status |
|---|---|---|---|
| `id` | string `s-azul-live` | `Session.Id : Guid` | 🔁 |
| `type` | const `'session'` | implicit | 🆕 |
| `title` | `'Serata Azul'` | `Session.Title : string?` (probabile) | ✅ |
| `subtitle` | `'In corso · Turno 3/5'` | derived | 🧮 |
| `status` | `'inprogress' \| 'completed' \| 'paused' \| 'setup' \| 'archived'` | `Session.Status : SessionStatus enum` | ⚠️ Verify enum values match. Probably also `GameNightSessionStatus.cs` for nights vs `SessionStatus` for stand-alone |
| `state` | `'live' \| 'done' \| 'paused' \| 'setup'` | duplicato di `status` (?) | ⚠️ Mockup ha **doppio campo** state+status — BE probabilmente uno solo. Risolvere |
| `turn` | string `'3/5'` (current/total) | `Session.CurrentTurn : int?` + `MaxTurns : int?` | ⚠️ Mockup format `'N/M'`; BE due colonne separate |
| `code` | `'AZL-7X2K'` (join code) | `Session.JoinCode : string?` OR `SessionInvite.Code : string` | ⚠️ Verify column. Probabile in `SessionInvite` entity (GameManagement BC) |
| `gameId` | `'g-azul'` \| `null` | `Session.GameId : Guid?` (FK SharedGame) | 🔁 (Guid). Null per archived/freestyle |
| `playerIds` | `string[]` `['p-marco', 'p-sara', …]` | `Session.Participants : ICollection<SessionParticipant>` (FK User) | ⚠️ Many-to-many via join entity |
| `toolCount` | number `3` | derived from `ToolkitSessionState` count | 🧮 |
| `duration` | `'45m' \| '1h 42m' \| ...` | derived from `Session.StartedAt` + `EndedAt` (DateTime) | 🧮 |
| `winner` | `'Sara T.'` (display name) | derived from `ScoreEntry.PlayerId` of max score OR `Session.WinnerId : Guid?` | 🧮 / ⚠️ |

**Diagnostic**:
- ⚠️ **CRITICAL**: campo `state` mockup è duplicato di `status` con nomi diversi (`inprogress` vs `live`, `setup` vs `setup`, `completed` vs `done`). FE deve riconciliare a un singolo discriminator. **Decisione design** richiesta: usare BE enum come canonical.
- ⚠️ **Code/JoinCode**: il mockup mostra codice nei badge — BE potrebbe averlo o no. Verificare `SessionInvite.Code` o aggiungere migration se mancante.
- 🧮 `duration`, `toolCount`, `winner` → derived. Endpoint `GET /api/v1/sessions/{id}?include=stats,participants` o equivalente.

---

## 4. `Agent` (mockup `a-*`) ↔ `AgentConfiguration` + `AgentDefinition`

**BE BC sorgente**: `KnowledgeBase`

| Mockup field | Tipo mockup | BE field | Status |
|---|---|---|---|
| `id` | `'a-azul-rules'` | `AgentConfiguration.Id : Guid` | 🔁 |
| `title` | `'Azul Rules Expert'` | `AgentConfiguration.Name : string` | 🔁 |
| `subtitle` | `'RAG · GPT-4o-mini · Attivo'` | derived | 🧮 |
| `status` | `'active' \| 'idle'` | `AgentConfiguration.IsActive : bool` + `LastInvokedAt` recency | 🧮 |
| `badge` | `'Attivo' \| 'Idle'` | mirror of status | 🧮 |
| `strategy` | `'hybrid-rag' \| 'rag-citations' \| 'router'` | `AgentConfiguration.StrategyName : string` | ✅ |
| `model` | `'GPT-4o-mini' \| 'Claude Haiku' \| ...` | `AgentConfiguration.ModelName : string` (or `ModelId`) | 🔁 model → ModelName |
| `gameId` | `'g-azul' \| null` | `AgentConfiguration.GameId : Guid?` (FK SharedGame) | 🔁 |
| `docs` | number `3` | derived from `VectorDocument` count linked to agent KB | 🧮 |
| `invocations` | number `342` | `AgentConfiguration.InvocationCount : long?` OR derived | ⚠️ Verify column. From `types/domain.ts`: `Agent.invocationCount: number` → BE field esiste |
| `avgLatency` | string `'1.2s'` | derived from `AgentTestResult` / metric aggregation | 🧮 |

**Diagnostic**:
- Mockup `strategy` discriminator (3 valori) corrisponde a `StrategyName` BE — verificare allineamento string literal.
- `model` mockup usa display name human ("GPT-4o-mini", "Claude Haiku") — BE potrebbe usare canonical id ("gpt-4o-mini-2024-07-18", "claude-3-haiku-20240307"). Decidere mapping FE.

---

## 5. `KB Document` (mockup `kb-*`) ↔ `VectorDocument` + `PdfDocumentEntity`

**BE BC sorgenti**: `KnowledgeBase` (`VectorDocument`, `Embedding`) + `DocumentProcessing` (PdfDocumentEntity, chunking, OCR)

| Mockup field | Tipo mockup | BE field | Status |
|---|---|---|---|
| `id` | `'kb-azul-ita'` | `PdfDocumentEntity.Id : Guid` (o `VectorDocument.Id`) | 🔁 |
| `title` | `'azul-regole-ita.pdf'` (filename) | `PdfDocumentEntity.FileName : string` | 🔁 |
| `subtitle` | `'2.4 MB · 12 pag · Indicizzato'` | derived | 🧮 |
| `status` | `'indexed' \| 'processing' \| 'failed'` | `PdfDocumentEntity.ProcessingStatus : enum` | ⚠️ Verify enum |
| `pages` | number `12` | `PdfDocumentEntity.PageCount : int?` | 🔁 |
| `size` | `'2.4 MB'` (display) | `PdfDocumentEntity.FileSizeBytes : long` | ⚠️ Display formatting |
| `chunks` | number `47` | derived: `TextChunkEntity` count where pdf_id = id | 🧮 |
| `embedding` | `'e5-base 768d' \| null` | `Embedding.ModelName : string` + `VectorDimension : int` | ⚠️ Mockup string concat |
| `gameId` | `'g-azul'` | `PdfDocumentEntity.SharedGameId : Guid?` (post-Phase 2d) | 🔁 |

**Diagnostic**:
- ⚠️ Status mockup `'failed'` (`kb-gloom-failed`) implica error tracking lato BE — verificare colonna `PdfDocumentEntity.ProcessingError : string?` o tabella audit separata.
- 🆕 Il mockup non mostra `OcrApplied`, `TableExtractionApplied`, `Language` ma BE li memorizza (vedi `DocumentProcessing` BC).

---

## 6. `Chat` (mockup `c-*`) ↔ `ChatThread` (+ `ChatSession`)

**BE BC sorgente**: `KnowledgeBase` (ChatThread, ChatSession, ConversationMemory)

| Mockup field | Tipo mockup | BE field | Status |
|---|---|---|---|
| `id` | `'c-azul-rules'` | `ChatThread.Id : Guid` | 🔁 |
| `title` | `'Come si gioca ad Azul?'` (prima query) | `ChatThread.Title : string?` OR derived from `ChatMessage` first | 🔁 / 🧮 |
| `subtitle` | `'Azul Rules Expert · 12 msg'` | derived | 🧮 |
| `status` | `'active' \| 'archived'` | `ChatThread.IsArchived : bool` | 🔁 |
| `badge` | `'Attiva' \| 'Archiviata'` | mirror | 🧮 |
| `msgCount` | number `12` | derived from `ChatMessage` count | 🧮 |
| `lastAt` | `'5 min fa' \| '2 ore fa' \| 'Ieri' \| ...` | `ChatThread.LastMessageAt : DateTime?` (display formatted) | 🧮 |
| `agentId` | `'a-azul-rules'` | `ChatThread.AgentId : Guid` (FK AgentConfiguration) | 🔁 |
| `gameId` | `'g-azul'` | `ChatThread.GameId : Guid?` (FK SharedGame) | 🔁 |

**Diagnostic**:
- 🗃️ BE ha `ConversationMemory` (state machine) non rappresentato nel mockup — non un gap, BE ricco di più.
- Tutti i discriminator (`status`) sono ok.

---

## 7. `Event` (mockup `e-*`) ↔ `GameNightEvent` + `GameNightInvitation` + `GameNightRsvp`

**BE BC sorgente**: `GameManagement.Domain.Entities.GameNightEvent.*` ✅ ricchissimo

| Mockup field | Tipo mockup | BE field | Status |
|---|---|---|---|
| `id` | `'e-marco-serata'` | `GameNightEvent.Id : Guid` | 🔁 |
| `type` | const `'event'` | implicit | 🆕 |
| `title` | `'Serata da Marco'` | `GameNightEvent.Title : string` | ✅ |
| `subtitle` | `'15 Mar · 19:00 · Casa Marco'` | derived | 🧮 |
| `status` | `'inprogress' \| 'setup' \| 'completed'` | `GameNightEvent.Status : GameNightStatus enum` | ⚠️ Verify enum mapping (`GameNightStatus.cs` esiste) |
| `badge` | `'Confermato' \| 'Setup' \| 'Conclusa'` | mirror status | 🧮 |
| `date` | `'15 Mar 2026'` (display) | `GameNightEvent.ScheduledAt : DateTime` | 🧮 Display format |
| `time` | `'19:00–23:00'` | `GameNightEvent.ScheduledAt + DurationMinutes`/`EndAt` | ⚠️ Verify columns |
| `location` | `'📍 Casa di Marco'` | `GameNightEvent.Location : string?` + emoji prefix UI (errata: NOT `LocationDescription`) | ⚠️ Mockup prefix emoji UI-injected |
| `participantIds` | `string[]` | `GameNightInvitation.UserId : Guid[]` (1:N via Invitations) | ⚠️ Mockup pre-joined, BE entity collection |
| `gameIds` | `string[]` `['g-azul', 'g-wingspan', …]` | `GameNightEvent.GameIds : List<Guid>` ✅ **embedded directly** + esiste anche `GameNightPlaylist` aggregate per playlist evolute | ⚠️ Verificato: il campo `GameIds` è inline (non FK), `GameNightPlaylist.cs` è separato per playlist features avanzate |
| `confirmed` | number `3` | derived: `GameNightEvent.Rsvps.Where(r => r.Status == GameNightInvitationStatus.Accepted).Count()` | 🧮 |
| `pending` | number `2` | derived: count `Rsvps` where status pending | 🧮 |
| _(mockup non lo mostra)_ | — | `GameNightEvent.OrganizerId : Guid` 🆕 | 🗃️ BE-only — chi crea l'evento |
| _(mockup non lo mostra)_ | — | `GameNightEvent.Description : string?` 🆕 | 🗃️ BE-only — descrizione lunga opzionale |
| _(mockup non lo mostra)_ | — | `GameNightEvent.MaxPlayers : int?` 🆕 (≥ 2 enforced) | 🗃️ BE-only — capacity gate |
| _(mockup non lo mostra)_ | — | `GameNightEvent.Reminder24hSentAt : DateTimeOffset?` + `Reminder1hSentAt : DateTimeOffset?` 🆕 | 🗃️ BE-only — telemetria reminder Quartz scheduling |
| _(mockup non lo mostra)_ | — | `GameNightEvent.CreatedAt : DateTimeOffset` + `UpdatedAt : DateTimeOffset?` | 🗃️ Audit standard |

**Diagnostic**:
- ✅ Full mapping disponibile (BE ha `GameNightEvent`, `GameNightInvitation`, `GameNightRsvp`, `GameNightSession`, `GameNightPlaylist`, + 8 Events + 5 EventHandlers + Repository + Scheduler `GameNightReminderJob` + EmailService + SlackBuilder).
- ⚠️ **CORREZIONE post spot-check**: il BE espone **solo** `ScheduledAt : DateTimeOffset` come start. **NON** ha `ScheduledEndAt` né `DurationMinutes`. Il mockup `'19:00–23:00'` (time range) richiede:
  - (a) UI deriva l'end (es. default 4h) — fragile
  - (b) **migration** per aggiungere `EndAt : DateTimeOffset?` o `DurationMinutes : int?`
  - **Decisione design richiesta**.
- ⚠️ Tipo verificato: tutti i datetime BE sono **`DateTimeOffset`** (non `DateTime`). Le date mockup `'15 Mar 2026'` devono essere serializzate con TZ info.
- Sub-mapping confermato:
  - `GameNightRsvp.cs` enum status: il BE usa `GameNightInvitationStatus` enum (path `Domain/Enums/GameNightInvitationStatus.cs`). Verificare valori esatti aprendo file.
  - RSVP API endpoints in `gameNightsClient.ts`.

## 7.5. Spot-check verificato — `GameNightEvent.cs` ✅

File aperto: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs`

**Shape esatto verificato** (estratto dal file, riga 13-30):

```csharp
internal sealed class GameNightEvent : AggregateRoot<Guid>
{
    public Guid OrganizerId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public string? Location { get; private set; }
    public int? MaxPlayers { get; private set; }
    public List<Guid> GameIds { get; private set; } = [];
    public GameNightStatus Status { get; private set; }
    public DateTimeOffset? Reminder24hSentAt { get; private set; }
    public DateTimeOffset? Reminder1hSentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public IReadOnlyList<GameNightRsvp> Rsvps => _rsvps.AsReadOnly();
}
```

**Validation in constructor** (verificato riga 54-61):
- `OrganizerId` non vuoto
- `Title` non vuoto, max 200 caratteri
- `MaxPlayers` ≥ 2 quando provided
- `Status` default `GameNightStatus.Draft`

**Pattern DDD verificato**:
- Aggregate root con private setters + factory method `Create()` (vedi riga 77)
- Visibility `internal sealed` (consumato via Application layer, non da `Api.Tests` direttamente — `InternalsVisibleTo` in `Api.csproj`)
- Lista `_rsvps : List<GameNightRsvp>` private; esposta read-only `Rsvps : IReadOnlyList<GameNightRsvp>`
- `#pragma warning disable MA0049` per naming conflict (folder name = class name)

**Implicazioni per FE**:
- Tutti i timestamp sono `DateTimeOffset` (TZ-aware) → serializzazione ISO 8601 con offset
- `GameIds` è inline list (no FK), non `Playlist` (esiste `GameNightPlaylist` separato per features avanzate)
- `Location` è semplice `string?` — niente geocoding/coordinates BE; UI eventualmente formatta con emoji prefix
- Lifecycle `Status` enum: gli stati sono `Draft → Published → Completed/Cancelled` (vedi summary doc riga 9)



---

## 8. `Toolkit` (mockup `tk-*`) ↔ ⚠️ Due BC potenziali

**BE BC sorgenti**: 2 BC paralleli con "Toolkit"-named entities:
1. `GameToolkit.Domain.Entities.Toolkit.cs` + `ToolkitVersion.cs` + `ToolkitWidget.cs` + `GameToolkit.cs` (AI-toolkit, KB-driven generation)
2. `GameToolbox.Domain.Entities.Toolbox.cs` + `ToolboxTemplate.cs` + `ToolboxTool.cs` + `Phase.cs` (toolbox manuale, card decks, phases, session tools)

Mockup `tk-azul-v2` con "3 strumenti pubblicati" e `version: 2` ricorda probabilmente la **`GameToolbox`** (toolbox manuale con tool atomici).

| Mockup field | Tipo mockup | BE field (GameToolbox.Toolbox) | Status |
|---|---|---|---|
| `id` | `'tk-azul-v2'` | `Toolbox.Id : Guid` | 🔁 |
| `title` | `'Azul Toolkit v2'` | `Toolbox.Name : string` | 🔁 |
| `subtitle` | `'Pubblicato · 3 strumenti'` | derived | 🧮 |
| `status` | `'active' \| 'setup'` | `Toolbox.PublishStatus : enum` | ⚠️ Verify enum |
| `badge` | `'Pubblicato' \| 'Draft'` | mirror status | 🧮 |
| `version` | number `2` | `Toolbox.Version : int` (OR semver string?) | ✅ |
| `toolCount` | number `3` | derived from `ToolboxTool` count | 🧮 |
| `useCount` | number `12` | derived from session usage aggregation | 🧮 |
| `gameId` | `'g-azul' \| null` | `Toolbox.GameId : Guid?` (FK SharedGame, null = generic) | 🔁 |
| `owner` | `'p-marco'` | `Toolbox.OwnerUserId : Guid` (FK User) | 🔁 |

**Diagnostic**:
- ⚠️ **Decisione design**: mockup non distingue GameToolbox vs GameToolkit. FE deve scegliere uno o l'altro — più probabile **GameToolbox** per il mockup actor "owner publica template con tools/phases".
- BE `GameToolkit.cs` invece sembra wrapper KB-driven (AI suggests tools) — probabilmente per SP7 features.

---

## 9. `Tool` (mockup `t-*`) ↔ `ToolboxTool.cs` (+ `Phase.cs`)

**BE BC sorgente**: `GameToolbox`

| Mockup field | Tipo mockup | BE field | Status |
|---|---|---|---|
| `id` | `'t-timer'` | `ToolboxTool.Id : Guid` | 🔁 |
| `title` | `'Timer Turno Azul'` | `ToolboxTool.Name : string` | 🔁 |
| `subtitle` | `'Timer · 5:00 · Per giocatore'` | derived | 🧮 |
| `status` | `'active'` | `ToolboxTool.IsActive : bool` | 🔁 |
| `kind` | `'timer' \| 'counter' \| 'deck' \| 'dice' \| 'tracker'` | `ToolboxTool.ToolType : ToolType enum` | ⚠️ Verify enum value mapping |
| `config` | JSON `{ duration: '5:00', perPlayer: true, warning: '30s' }` | `ToolboxTool.ConfigJson : string` (JSONB column) | ⚠️ Schemaless — verificare schema validation per kind |
| `uses` | number `23` | derived from usage telemetry | 🧮 |
| `toolkitId` | `'tk-azul-v2'` | `ToolboxTool.ToolboxId : Guid` (FK Toolbox) | 🔁 toolkitId → ToolboxId |

**Diagnostic**:
- ⚠️ **`config` schemaless**: BE memorizza JSONB libero, FE deve enforcing schema per kind (`timer` schema ≠ `dice` schema). Validation FE-side richiesta. Pattern Zod recommended.
- Mapping per **5 kind values**: ognuno corrisponde probabilmente a `ToolType.{Timer, Counter, Deck, Dice, Tracker}` o equivalente. Verificare nel file `Phase.cs` se "phase" è ulteriore wrapping.

---

## 10. `HouseRule` (richiesto da prompt 0.2, NON in `data.js`)

**BE BC sorgente**: `AgentMemory.Domain.Models.HouseRule.cs` ✅ (file path: `BoundedContexts/AgentMemory/Domain/Models/HouseRule.cs` — _Models_ folder, non _Entities_, suggerisce Value Object o Aggregate sub-record)

**Mapping FE TODO** (post-design discussion):
- `id : Guid`
- `userId : Guid` (chi ha definito)
- `gameId : Guid` (per quale gioco)
- `groupId : Guid?` (per quale gruppo, opzionale)
- `originalQuestion : string` (cosa l'utente chiedeva originariamente)
- `officialRule : string?` (cosa diceva il rulebook)
- `houseRuleText : string` (la house rule)
- `source : HouseRuleSource enum` (auto-suggested-by-chat-low-confidence \| user-defined-manual)
- `createdAt : DateTime`, `updatedAt : DateTime?`
- (verificare schema completo aprendo il file)

**Diagnostic**:
- ✅ BE supporta HouseRule pattern (AgentMemory BC dedicato + Source enum). Validare endpoint `POST /api/v1/games/{id}/house-rules` (citato in BACKEND_PROMPTS.md § 6.1).

---

## 11. `GameNight` (con RSVP)

→ già coperto in **§ 7 Event** sopra. Le 6 entity BE (`GameNightEvent`, `GameNightInvitation`, `GameNightRsvp`, `GameNightSession`, `GameNightPlaylist`, `GameNightStatus` enum) coprono **completamente** il workflow proposto in `BACKEND_PROMPTS.md` § 8.1-8.3.

---

## 12. `RSVP`

**BE entity**: `GameNightRsvp.cs` ✅ (`apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightRsvp.cs`)
**BE DTO**: `GameNightRsvpDto.cs` ✅

**Mockup field expected**:
- `eventId : Guid`, `userId : Guid`, `status : 'yes' \| 'maybe' \| 'no' \| 'pending'`, `respondedAt : DateTime?`, `notes : string?`

→ shape ben coperto BE. Endpoint `PATCH /api/v1/game-nights/{id}/rsvp { status }`.

---

## 13. `Notification` (richiesto da prompt 0.2, NON in `data.js`)

**BE BC sorgente**: `UserNotifications` ✅ **molto ricco**

Entities + Aggregates + VOs:
- `Notification.cs` (aggregate root)
- `NotificationPreferences.cs` (aggregate)
- `NotificationQueueItem.cs` (aggregate, scheduled delivery)
- `NotificationType` enum
- `NotificationSeverity` enum
- `NotificationChannelType` enum (in-app, email, push, slack)
- `NotificationQueueStatus` enum
- `NotificationPayloads` (sealed payloads per type)
- Plus DTOs: `NotificationDto`, `NotificationPreferencesDto`, `NotificationMetricsDto`, `NotificationQueueItemDto`
- Plus repositories + Slack builder + Dispatcher + CleanupJob

**Diagnostic**:
- ✅ Backend già **pronto in produzione** per: RSVP, indicizzazione PDF, agent update, billing, system. Vedi `BACKEND_PROMPTS.md` § 11.3 (categorie attese).
- Endpoint API ipotizzati nel handoff: `GET /api/notifications?since=&category=`, `PATCH /api/notifications/{id}/read`, `POST /api/notifications/mark-all-read`. → mappare al pattern v1: `/api/v1/notifications/*` (ESLint rule `local/api-client-v1-prefix = error` enforced). Verificare `notifications.ts` client esiste con questi endpoint.

## 13.5. Spot-check verificato — `Notification.cs` ✅

File aperto: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/Notification.cs`

**Shape esatto verificato** (riga 10-22):

```csharp
internal sealed class Notification : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }      // VO class, not simple enum
    public NotificationSeverity Severity { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }              // NOTE: 'Message' (not 'Body' or 'Content')
    public string? Link { get; private set; }                // Deep-link URL
    public string? Metadata { get; private set; }            // JSON string
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }          // NOTE: DateTime (not DateTimeOffset — inconsistenza con GameNightEvent)
    public DateTime? ReadAt { get; private set; }
    public Guid? CorrelationId { get; private set; }
}
```

**Domain methods identificati**:
- `MarkAsRead()` — idempotente, set `IsRead = true` + `ReadAt = UtcNow`
- Constructor con validation: `Title` non vuoto, `Message` non vuoto, `Type`/`Severity` non null

**Differenza con `GameNightEvent`**:
- ⚠️ `Notification` usa **`DateTime`** (UTC kind), `GameNightEvent` usa **`DateTimeOffset`**. Inconsistency cross-BC. FE deve gestire entrambi.
- `Type` e `Severity` sono **value object classes** (non semplici enum) — guard `ArgumentNullException.ThrowIfNull(type)` nel costruttore. Path: `UserNotifications/Domain/ValueObjects/NotificationType.cs` + `NotificationSeverity.cs` + `NotificationPayloads.cs` + `NotificationChannelType.cs` + `NotificationQueueStatus.cs`.

**Field-by-field mapping mockup activity feed `data.js`** (lines 240-250) ↔ Notification BE:

| Mockup activity field | BE field | Status |
|---|---|---|
| `id` | `Notification.Id : Guid` | 🔁 |
| `at` (display "10 min fa") | derived from `CreatedAt` | 🧮 |
| `who` ("Marco R.") | derived from `UserId` join → `User.DisplayName` | 🧮 |
| `what` ("ha avviato") | derived from `Type` semantic | 🧮 |
| `ref` (`'s-azul-live'`) | derived from `Metadata` JSON or `Link` URL parse | 🧮 |
| `kind` (`'session'`/`'chat'`/`'kb'`/`'event'`/`'toolkit'`/`'game'`) | derived from `Type` semantic mapping | 🧮 |

**Implicazione**: il feed activity del mockup è una **proiezione UI** del `Notification` BE — non c'è entity `Activity` dedicata. UI deriva da `Type` + `Metadata`.

---

## 14. `UserPreferences` (richiesto da prompt 0.2, NON in `data.js`)

**BE entity**: `SystemConfiguration.Domain.Entities.UserPreferences.cs` ✅ + `UserPreferencesRepository.cs`

→ shape probabile: theme preference, locale, accessibility, default agent, confidence threshold, notification channels, privacy settings. (verificare schema completo aprendo il file).

→ matchabile con flow `/settings` proposto in `BACKEND_PROMPTS.md` § 11.2.

---

## 15. Tabella riassuntiva — Action items

| # | Mockup entity | BE coverage | Migration needed? | UI computed/derived | Decisione richiesta |
|---|---|---|---|---|---|
| 1 | Game | ✅ multi-entity (SharedGame + UserLibraryEntry + Wishlist + many VO) | ❌ no — display format only | ~6 fields derived (stars/badge/kbState/totalPlays/winRate/avgScore) | Endpoint `?include=stats` ottimization |
| 2 | Player | ✅ User + RecordPlayer + LiveSessionPlayer | ❌ no — naming reconciliation | ~7 fields derived | Confermare `User.AccentHue` esiste o usare hash |
| 3 | Session | ⚠️ 4+ entity con "Session" nel nome | ❌ no — reconciliation FE-side | `state` vs `status` doppio discriminator | **Scegliere canonical**: SessionTracking.Session |
| 4 | Agent | ✅ AgentConfiguration | ❌ no | latency/invocations derived | Mapping model display string ↔ canonical id |
| 5 | KB Document | ✅ PdfDocumentEntity + VectorDocument | ❌ no | chunks count derived | Format size/embedding string display |
| 6 | Chat | ✅ ChatThread | ❌ no | msgCount/lastAt derived | — |
| 7 | Event (GameNight) | ✅ GameNightEvent + 5 satellites | ❌ no — BE molto più ricco | `time`/`location` display formatting | Verificare `LocationDescription` colonna esiste |
| 8 | Toolkit | ⚠️ 2 BC paralleli (GameToolbox vs GameToolkit) | ❌ no | toolCount/useCount derived | **Scegliere quale BC** mappa il mockup |
| 9 | Tool | ✅ ToolboxTool | ❌ no | uses derived | Validazione `config` schema per kind FE-side |
| 10 | HouseRule | ✅ AgentMemory.HouseRule + HouseRuleSource enum | ❌ no | — | — |
| 11 | GameNight | → § 7 | — | — | — |
| 12 | RSVP | ✅ GameNightRsvp + DTO | ❌ no | — | — |
| 13 | Notification | ✅ Notification + 4 satellites + 4 VOs + Dispatcher + Slack + Scheduler | ❌ no | — | Verificare endpoint v1 |
| 14 | UserPreferences | ✅ UserPreferences entity + Repository | ❌ no | — | Verificare shape completa |

## 16. Diagnostica complessiva

### ✅ Cosa è già coperto (no migration needed)

- **14/14 entity mockup hanno una BE counterpart** (anche se talvolta multi-entity aggregate).
- Tutti i discriminator (`status`) hanno equivalente enum BE (verificare allineamento valori).
- Tutte le relazioni 1:N (sessions → players, agents → docs, events → invitations) sono BE-modeled.
- Notifiche + scheduling + Slack + push + email infrastruttura **già in produzione**.
- HouseRule pattern + AgentMemory BC dedicato + auto-detection low-confidence flow → backend ready per SP7.

### ⚠️ Dove serve decisione design / reconciliation FE

1. **Session vs GameSession vs LiveGameSession**: scegliere canonical. _Suggerito_: `SessionTracking.Session` per aggregate root, `LiveGameSession` per realtime state buffer.
2. **GameToolbox vs GameToolkit**: scegliere quale BC mappa il mockup `tk-*`. _Suggerito_: `GameToolbox` per manuale, `GameToolkit` per AI-generated.
3. **`Session.state` vs `Session.status`**: dropping duplicate dal mockup.
4. **ID slug vs Guid**: mantenere slug human-readable nel mock data, ma _i contratti API restano Guid_ in produzione.
5. **`config` JSON schema** per ogni `Tool.kind`: definire Zod schemas FE-side per validation.
6. **`Player.color` (hue numerico)**: derivare da `User.Id` hash o aggiungere colonna `User.AccentHue : int?` BE.

### 🧮 Endpoint optimization opportunities

Molti campi mockup sono **derived/aggregated**:

- `Game`: 6 campi (stars, badge, kbState, totalPlays, winRate, avgScore)
- `Player`: 7 campi (subtitle, initials, color, status, badge, totalWins, totalSessions, winRate, fav)
- `Session`: 4 campi (subtitle, toolCount, duration, winner)
- `Agent`: 3 campi (subtitle, docs, invocations, avgLatency)
- `KB`: 2 campi (subtitle, chunks)
- `Chat`: 2 campi (subtitle, msgCount, lastAt)
- `Event`: 4 campi (subtitle, date, time, confirmed, pending)
- `Toolkit/Tool`: 2 campi (subtitle, uses)

→ Per evitare N+1 query, ogni endpoint detail `/api/v1/{entity}/{id}` deve esporre `?include=stats` o equivalente. Verificare se i 60+ client già implementano questo pattern.

### ❌ Migration realmente necessarie

**Nessuna trovata** — il BE è già abbastanza ricco. Le uniche aggiunte opzionali sarebbero:

- `User.AccentHue : int?` (opzionale, può essere hash-derived)
- `SharedGame.CoverGradient : string?` (opzionale, può essere hash-derived da Title o derivato da cover image dominant colors)
- (verificare presenza `SessionInvite.Code` se non già esistente)

→ Le si decidono _dopo_ Step 6 in base ai mock screen prioritari.

## 17. Limiti scope di questo audit

- _Non_ ho aperto ogni file `.cs` per leggere campo-per-campo. Per ogni entity ho letto **nome + namespace + folder context**. La diff esatta field-by-field può richiedere lettura di:
  - File entity + EF Core configuration in `Infrastructure/Persistence/Configurations/*Configuration.cs`
  - File migrations in `Infrastructure/Persistence/Migrations/`
- _Non_ ho confrontato gli **enum values** esattamente (es. `'inprogress'` mockup vs `SessionStatus.InProgress` BE). Lo si fa al primo touch della feature.
- _Non_ ho verificato la copertura **API v1**: alcuni endpoint citati in `BACKEND_PROMPTS.md` potrebbero richiedere prefisso (`/api/v1/`) o struttura URL diversa. Da validare per Step 7 (pilot screen).
- **`SharedGame.cs` aggregate root**: la mia ricerca ha visto solo Value Objects/sub-entity (`GameDesigner`, `GamePublisher`, `GameMechanic`, ecc.). Verosimilmente l'aggregate root sta in `Aggregates/SharedGame.cs` o `Domain/SharedGame.cs` — _da aprire prima del touch_.

## 18. Stato dei deliverable Step 1-6

| Step | Deliverable | Stato | Path |
|---|---|---|---|
| 1 | `design_handoff/CODEBASE_AUDIT.md` | ✅ generato (sessione precedente) | `admin-mockups/design_handoff/CODEBASE_AUDIT.md` |
| 5 | `design_handoff/SCHEMA_DIFF.md` | ✅ generato (questo file) | `admin-mockups/design_handoff/SCHEMA_DIFF.md` |
| 6 | `design_handoff/COMPONENTS_AUDIT.md` | ⏳ next | _da generare_ |

---

**Generato da Claude Code Opus 4.7 in modalità read-only.** Nessuna migration eseguita. Nessuna modifica al codebase. Decisioni design (Session vs GameSession, GameToolbox vs GameToolkit, ecc.) demandate al maintainer.
