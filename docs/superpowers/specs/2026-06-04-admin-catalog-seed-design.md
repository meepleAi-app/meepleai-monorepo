# Admin Catalog Seed — Design Spec

**Date**: 2026-06-04
**Author**: MeepleAI maintainers (brainstorm sessione)
**Status**: Design draft (awaiting user approval before implementation plan)
**Related**: Umbrella #1821 (CLOSED), #1823 L2 Wikidata cover (OPEN P2), #1835 catalog-ingestion FE patterns

## 1. Problem statement

MeepleAI necessita di popolare il catalogo `SharedGameCatalogEntry` con ~1000+ giochi per coprire tre use case:

1. **RAG su rulebook** — chat AI sui giochi (sottoinsieme con PDF caricato)
2. **Discovery / library browsing** — utente naviga il catalogo per aggiungere giochi alla propria libreria
3. **Session tracking** — utente seleziona un gioco quando registra sessioni/punteggi

Manual data entry per 1000+ giochi è fuori scope. Serve un import bulk admin-driven con curation gate.

**Vincoli legali noti** (dall'umbrella #1821):
- BGG ToS clausole "competes with or displaces the market" e "primary purpose of gaining advertising or subscription revenue"
- User-submitted content (descriptions, ratings, images) è copyrightabile (BGG / publishers)
- Pattern di rischio già escluso: BGG runtime fetch, BGG-cache redistribution, AI-generated covers

**Nuovo scenario in scope (low risk)**: admin one-shot import di **soli dati fattuali** (titolo, anno, designer, publisher, mechanics tags, players, time, BGG ID, Wikidata Qid), curato via review gate, mai display all'utente con attribuzione visibile.

## 2. Goals & non-goals

### Goals
- Catalogo iniziale di 500-1000 giochi seedati con accuracy validata da admin review
- Provider mix multi-fonte per resilienza legal/operational
- Audit trail completo (provider, sourceUrl, fetchedAt, sourceField per ogni field)
- Feature flag runtime per kill-switch immediato (no redeploy)
- Coerenza grafica con admin admin pages esistenti (#1835 catalog-ingestion pattern)

### Non-goals
- ❌ Sync continuo bidirezionale (one-shot per gioco, mai mirror)
- ❌ Display all'utente di "Data from BGG" (FE agnostic alla sorgente)
- ❌ Import di description/text/cover (escluso per copyright)
- ❌ Import di rating/statistics aggregati (DB sui generis EU + competing market)
- ❌ Endpoint pubblico (tutti admin-only)
- ❌ Cover image import (gestito separatamente da #1823 L2 Wikidata cover)
- ❌ Pro/Premium marketing che cita BGG come selling point

## 3. Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Admin UI: /admin/catalog/seed-queue                    │
│  (paste BGG IDs | single add | search Wikidata)         │
└─────────────────────┬───────────────────────────────────┘
                      │ CreateCatalogSeedDraftCommand
                      ▼
┌─────────────────────────────────────────────────────────┐
│  CatalogSeedAggregatorService                           │
│  ┌────────────────────────┐  ┌──────────────────────┐  │
│  │ IWikidataCatalogProvider│ │ IBggCatalogProvider  │  │
│  │ (primary, CC0)         │  │ (fallback, BGG XML)  │  │
│  │ SPARQL / Q items       │  │ XML API2 whitelisted │  │
│  └────────────┬───────────┘  └──────────┬───────────┘  │
│               └────────────┬────────────┘              │
│                            ▼                            │
│              CatalogSeedDraft (Pending)                 │
│              + Provenance per field                     │
└──────────────────────────┬──────────────────────────────┘
                           │ admin approve
                           ▼
                  SharedGameCatalogEntry
                  + BggSeededAt + ProvenanceJson
```

### 3.1 Bounded context

Nuovo sub-domain `CatalogSeeding` dentro `SharedGameCatalog` BC. Riusa `SharedGameRepository` per Approve step (insert/upsert in `SharedGameCatalogEntry`).

### 3.2 Strategy pattern: ICatalogProvider

```csharp
internal interface ICatalogProvider
{
    string Name { get; } // "wikidata" | "bgg"
    Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct);
}

internal sealed record CatalogProviderQuery(int? BggId, string? WikidataQid, string? SearchTerm);

internal sealed record CatalogProviderResult(
    IReadOnlyDictionary<string, FieldValue> Fields, // field name → value+metadata
    string? RawPayloadJson,
    string? ErrorMessage);

internal sealed record FieldValue(
    string Provider,        // "wikidata" or "bgg"
    string SourceUrl,
    string SourceField,
    DateTime FetchedAt,
    object Value);
```

### 3.3 Aggregator (provider chain)

`CatalogSeedAggregator` chiama prima Wikidata, poi BGG per ogni campo mancante. Strategia:

1. Wikidata fetch (primary) — popola tutti i campi disponibili
2. Per ogni `AllowedField` mancante, BGG fetch (fallback)
3. Merge in unica `Provenance` map field→FieldValue
4. Persistenza: `CatalogSeedDraft.ProvenanceJson` + `RawPayloadJson`

```jsonc
// CatalogSeedDraft.ProvenanceJson (esempio: Catan)
{
  "title": {
    "value": "Catan",
    "provider": "wikidata",
    "sourceUrl": "https://www.wikidata.org/wiki/Q98056728",
    "sourceField": "labels.en",
    "fetchedAt": "2026-06-04T12:34:56Z"
  },
  "yearPublished": {
    "value": 1995,
    "provider": "wikidata",
    "sourceUrl": "https://www.wikidata.org/wiki/Q98056728",
    "sourceField": "P577",
    "fetchedAt": "2026-06-04T12:34:56Z"
  },
  "mechanics": {
    "value": ["Trading", "Modular Board"],
    "provider": "bgg",
    "sourceUrl": "https://boardgamegeek.com/xmlapi2/thing?id=13",
    "sourceField": "link[type=boardgamemechanic]",
    "fetchedAt": "2026-06-04T12:34:57Z"
  }
}
```

**Audit capabilities abilitate**:
- "Chi/quando/da dove" per ogni field (risposta a richiesta legal/BGG)
- Re-fetch selettivo per singolo campo (es. BGG aggiorna mechanics → ri-pullo solo quello)
- Drift detection (Quartz periodico confronta value attuale vs value fetched, alert se diverge)
- GDPR "right to know" se in futuro qualcuno richiede dettaglio dei dati ricevuti

## 4. Lifecycle del CatalogSeedDraft

```
┌─────────┐    fetch     ┌──────────┐   review   ┌──────────┐
│ Pending │ ─────────────▶│ Fetched  │ ──────────▶│ Approved │
│ (added) │  background  │ (await   │  admin     │ (copied  │
│         │  Quartz job  │  review) │  approves  │  to SGCE)│
└─────────┘              └────┬─────┘            └──────────┘
                              │ admin rejects
                              ▼
                         ┌──────────┐
                         │ Rejected │
                         │ (soft-   │
                         │  delete) │
                         └──────────┘
```

### 4.1 Stati e transizioni

| Stato | Triggering action | Side effect |
|---|---|---|
| `Pending` | Admin paste BGG ID o nome | Inserito in queue, niente API call ancora |
| `Fetched` | `CatalogSeedFetchJob` (Quartz, 1 min) processa Pending → calls Wikidata+BGG | `ProvenanceJson` popolato + `RawPayloadJson` |
| `FetchFailed` | API error dopo 3 retry | `ErrorMessage` settato, admin può manual-retry o manual-fill |
| `Approved` | Admin clicca "Pubblica" | Insert/upsert in `SharedGameCatalogEntry`, emit `SharedGameCreatedFromSeedEvent` |
| `Rejected` | Admin clicca "Scarta" | Soft-delete (IsDeleted=true) — audit conservato |

### 4.2 Entity

```csharp
public class CatalogSeedDraftEntity
{
    public Guid Id { get; set; }
    public int? BggId { get; set; }
    public string? WikidataQid { get; set; }
    public string? SearchTermInput { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ProvenanceJson { get; set; }
    public string? RawPayloadJson { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ResultingSharedGameId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FetchedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    [Timestamp] public byte[]? RowVersion { get; set; }
    public bool IsDeleted { get; set; }
}
```

### 4.3 Why `Fetched` come stato distinto

Separa lookup (slow, rate-limited, può fallire) da approval (fast, manual). Permette:
- Batch fetch overnight + review mattutina
- UI admin sempre responsive (no blocking on API calls)
- Retry indipendente da approval workflow

## 5. Admin UI: /admin/catalog/seed-queue

Single-page admin tool, layout 2 colonne (input sx, queue dx). Coerente con design system MeepleAI (tokens semantici, `data-theme`, MeepleCard pattern).

### 5.1 Wireframe

```
┌──────────────────────────────────────────────────────────────────────┐
│  /admin/catalog/seed-queue                                           │
├───────────────────────────┬──────────────────────────────────────────┤
│ ⊕ ADD TO QUEUE            │ 📋 QUEUE                                 │
│                           │                                          │
│ [Bulk paste BGG IDs]      │ Filter: [All ▼] [Pending] [Fetched]      │
│ [textarea + Enqueue]      │ Sort: created ↓                          │
│                           │                                          │
│ ───────────────           │ ┌──────────────────────────────────┐    │
│                           │ │ 🟡 BGG:13  "Catan"               │    │
│ [+ Single add by BGG ID]  │ │ Fetched · Wikidata+BGG · 12:34   │    │
│ [+ Single add by name]    │ │ [Preview ↓] [✓ Approve] [✗ Reject]│    │
│                           │ ├──────────────────────────────────┤    │
│ ───────────────           │ │ ▼ PREVIEW                        │    │
│                           │ │   Title:        Catan (wikidata) │    │
│ 🔍 SEARCH WIKIDATA        │ │   Year:         1995 (wikidata)  │    │
│ [boardgame published]     │ │   Designer:     Klaus Teuber (wd)│    │
│ [→ 14 results CC0]        │ │   Players:      3–4 (bgg)        │    │
│ [+ Add all] [pick]        │ │   Time:         60 min (bgg)     │    │
│                           │ │   Mechanics:    Trading (bgg)    │    │
│                           │ │   [View raw payloads JSON ⤓]    │    │
│                           │ └──────────────────────────────────┘    │
│                           │ ┌──────────────────────────────────┐    │
│                           │ │ 🟢 BGG:30549 "Pandemic"          │    │
│                           │ │ Approved · 2 days ago            │    │
│                           │ └──────────────────────────────────┘    │
│                           │ ┌──────────────────────────────────┐    │
│                           │ │ 🔴 BGG:N/A "MyIndieGame"          │    │
│                           │ │ FetchFailed · "Not found"         │    │
│                           │ │ [Manual fill →]                   │    │
│                           │ └──────────────────────────────────┘    │
└───────────────────────────┴──────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════
   📡 LIVE STREAM (LogStream)
   [Info ▼] [BatchStarted] [Failed only]  [Pause]
   12:34:56 ⓘ BatchStarted batchId=abc count=10
   12:34:57 ⓘ SeedEntryFetched draftId=xyz bggId=13 wikidata+bgg
   12:34:58 ⚠ SeedEntryFetchFailed draftId=fff retry=2 reason=timeout
   12:35:00 ⓘ BatchCompleted batchId=abc ok=9 fail=1
═══════════════════════════════════════════════════════════════════════
```

### 5.2 Riuso componenti #1835 catalog-ingestion

| Componente esistente | Riuso in seed-queue | Adaptation |
|---|---|---|
| `SyncStatusHero.tsx` | `SeedQueueStatusHero` | KPI: Pending / Fetched / Approved / Rejected counts |
| `SyncRunTimeline.tsx` | `SeedRunTimeline` | Timeline batch fetch (overnight runs) |
| `LogStream.tsx` | riusato as-is | SSE live log del `CatalogSeedFetchJob` |
| `QueuePendingPanel.tsx` | riusato as-is | Pending + Fetched entries con approve/reject |
| `FailedItemsPanel.tsx` | riusato as-is | FetchFailed entries con retry/manual-fill |
| `AssignBggIdForm.tsx` | `AddBggIdForm` (rename) | Single add by BGG ID |
| `ManualAssignModal.tsx` | `ManualSeedModal` | Long-tail fallback manual entry |
| `CsvImportModal.tsx` | riusato as-is | Bulk paste BGG IDs CSV |
| `ExportCatalogButton.tsx` | `ExportSeedAuditButton` | Audit log export per legal review |

### 5.3 Componenti nuovi (da scrivere)

- `WikidataSearchForm.tsx` — SPARQL pre-canned (nuova feature)
- `SeedPreviewPanel.tsx` — provenance display per-field (nuova feature)
- `BggIdValidationBadge.tsx` — feedback visual su parser BGG IDs

### 5.4 Endpoint admin

Tutti `[Authorize(Roles="Admin")]`:

| Verb | Path | Comando/Query |
|---|---|---|
| POST | `/api/v1/admin/catalog/seeds/bulk` | `BulkEnqueueCatalogSeedsCommand` (max 100 IDs) |
| POST | `/api/v1/admin/catalog/seeds` | `EnqueueCatalogSeedCommand` |
| GET | `/api/v1/admin/catalog/seeds` | `ListCatalogSeedsQuery` (paginated, filterable) |
| GET | `/api/v1/admin/catalog/seeds/{id}` | `GetCatalogSeedByIdQuery` |
| POST | `/api/v1/admin/catalog/seeds/{id}/approve` | `ApproveCatalogSeedCommand` |
| POST | `/api/v1/admin/catalog/seeds/{id}/reject` | `RejectCatalogSeedCommand` |
| GET | `/api/v1/admin/catalog/seeds/stream` | SSE event stream (text/event-stream) |
| POST | `/api/v1/admin/catalog/seeds/wikidata-search` | Proxy SPARQL query |

Tutti rispettano CQRS pattern (CLAUDE.md): solo `IMediator.Send`, zero direct service injection.

### 5.5 Feature flag

`AdminCatalogSeedEnabled` runtime config (pattern come `RegistrationMode`). Default `false`. Kill-switch immediato.

## 6. Live event stream

Pattern identico a `IPdfProgressStreamService` esistente (singleton in-memory subscribers).

```
Backend
├─ ICatalogSeedStreamService (singleton, registry subscriber)
├─ CatalogSeedFetchJob publishes:
│  ├─ BatchStarted { batchId, draftIds[], startedAt }
│  ├─ SeedEntryFetched { draftId, bggId, providerUsed, durationMs }
│  ├─ SeedEntryFetchFailed { draftId, error, retryAttempt }
│  └─ BatchCompleted { batchId, succeeded, failed, totalDurationMs }
└─ GET /api/v1/admin/catalog/seeds/stream (text/event-stream, [Authorize Admin])

Frontend
└─ LogStream.tsx (riusato as-is)
   ├─ EventSource client
   ├─ Filter: livello / provider / draftId / time range
   └─ Pause/Resume + auto-scroll lock
```

**Lifecycle**:
- Subscriber registrato quando admin apre la pagina, deregistrato su beforeunload
- Buffer ultimi 200 eventi in memoria service, replay automatico al connect
- Riconnessione automatica `EventSource` su drop

## 7. Provider implementation

### 7.1 IWikidataCatalogProvider (primary, CC0)

**Endpoint**: SPARQL `https://query.wikidata.org/sparql`

**Query pattern** (BGG ID → Wikidata Qid):
```sparql
SELECT ?game ?gameLabel ?yearPublished ?designer ?publisher ?minPlayers ?maxPlayers
WHERE {
  ?game wdt:P2339 "13".
  OPTIONAL { ?game wdt:P577 ?yearPublished. }
  OPTIONAL { ?game wdt:P178 ?designer. }
  OPTIONAL { ?game wdt:P123 ?publisher. }
  OPTIONAL { ?game wdt:P1873 ?minPlayers. }
  OPTIONAL { ?game wdt:P1872 ?maxPlayers. }
  SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
}
LIMIT 1
```

**Campi mappati**:
| MeepleAI field | Wikidata property | Note |
|---|---|---|
| `Title` | `labels.en` | Multilang via JSON if i18n needed |
| `YearPublished` | `P577` (publication date) | Extract year |
| `Designers[]` | `P178` | Multi-value |
| `Publishers[]` | `P123` | Multi-value |
| `MinPlayers`/`MaxPlayers` | `P1873` / `P1872` | Numeric |
| `PlayingTimeMinutes` | `P2047` (duration) | Convert to minutes |
| `WikidataQid` | item URI | For cross-reference |

**Rate limit**: SPARQL limit ~30s/query + 5 query/sec. Polly retry exp 1s/5s/30s.

**License**: tutti i dati Wikidata sono **CC0**. Niente attribution required. Zero rischio legale.

### 7.2 IBggCatalogProvider (fallback, whitelisted)

**Endpoint**: `https://boardgamegeek.com/xmlapi2/thing?id={BggId}&stats=0`

**Whitelist hard-coded** (in `BggImportFieldFilter`):
```csharp
internal static class BggImportFieldFilter
{
    public static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "name",
        "yearpublished",
        "minplayers", "maxplayers",
        "playingtime", "minplaytime", "maxplaytime",
        "minage",
        "link[type=boardgamedesigner]",
        "link[type=boardgamepublisher]",
        "link[type=boardgameartist]",
        "link[type=boardgamemechanic]",
        "link[type=boardgamecategory]",
        "link[type=boardgamefamily]",
    };

    // Hard-coded forbidden fields, mai mappati anche se presenti nella response:
    public static readonly HashSet<string> ForbiddenFields = new(StringComparer.Ordinal)
    {
        "description",     // user-submitted text → BGG/author copyright
        "image", "thumbnail",  // editor copyright
        "statistics",      // average rating, users rated → DB sui generis EU
        "comments",
        "videos",
    };
}
```

**Unit test obbligatorio**: fallisce se `AllowedFields` viene esteso con campi non whitelisted.

**Rate limit**: BGG XML API ~30 req/min, `Retry-After` header su 202 Accepted. Polly: retry exp 1s/5s/30s, max 3 attempt, DLQ. Inter-request delay ≥1s mandatory.

**User-Agent obbligatorio**: `MeepleAI/1.0 (admin-catalog-seed; abuse@meepleai.app)` — identifica + permette BGG contact prima di azione legale.

### 7.3 Quartz CatalogSeedFetchJob

```csharp
[DisallowConcurrentExecution]
public sealed class CatalogSeedFetchJob : IJob
{
    public const int BatchSize = 10;
    public const int DelayBetweenItemsMs = 1000; // BGG rate limit compliance
    // Schedule: every 1 minute via Quartz
}
```

Pattern coerente con `PdfProcessingQuartzJob` e `BackfillPdfCoversJob` (#1873). Picks up max 10 `Pending` drafts per run.

## 8. Safeguard legali

| Safeguard | Implementazione |
|---|---|
| **Feature flag** | `AdminCatalogSeedEnabled` runtime config. Default `false`. Spegne tutto senza redeploy. |
| **Provenance retention** | `CatalogSeedDraftEntity.ProvenanceJson` + `RawPayloadJson` mai eliminati. Audit GDPR/BGG. |
| **Audit log** | Domain event `CatalogSeedApproved`, `CatalogSeedRejected`, `BggFetchInvoked` in `domain_event_logs`. |
| **Whitelist enforcement** | `BggImportFieldFilter` static + unit test fail se aggiungono fields fuori whitelist. |
| **Cap bulk** | `POST /seeds/bulk` capped a 100 IDs/batch + rate-limit 1 batch/5min per admin user. |
| **No public API** | Tutti endpoint `[Authorize(Roles="Admin")]`. Niente endpoint pubblici con attribuzione BGG. |
| **ToS re-check** | Quartz `BggTosWatcherJob` mensile fetch ToS URL + hash. Alert se cambiato. Manual review obbligatoria. |
| **Provider provenance display** | FE Admin mostra "via Wikidata/BGG". Utenti finali vedono solo dato finito (agnostic). |
| **Terms of Service update** | Clausola: "We may seed our catalog with publicly available data; original sources retain their respective rights." |

**Privacy policy**: NO update necessario (no PII utente in scope — vedi §8.5 per analisi GDPR sui designer/publisher names).

## 8.5 Legal framework references

> ⚠️ **Disclaimer**: questa sezione analizza il rischio basato su ToS pubblici e doctrine note. Non è consulenza legale. Pre-rollout pubblico è **fortemente raccomandato** un parere legale formale (~1h) per validare l'interpretazione delle clausole "competes/displaces" e "substantial part" applicate al caso MeepleAI.

### 8.5.1 BGG Terms of Service

Fonte: `boardgamegeek.com/terms` (verificato 2026-06-02 nell'umbrella #1821, ancora la versione corrente al 2026-06-04).

Clausole materialmente applicabili a questo design (testo BGG seguito da analisi):

#### Section "Restrictions" — Framing
> *"You may NOT use any framing techniques to enclose any trademark, logo, or other proprietary information (including but not limited to images, text, page layout, or forms) of BoardGameGeek without express written consent of BGG."*

**Applicazione**: vieta embedding di asset BGG con look-and-feel BGG.
**Mitigazione design**: NESSUN asset BGG (cover, descrizioni, comments, ratings) viene importato. Solo fatti puri. Già coperto da §2 non-goals + `BggImportFieldFilter.ForbiddenFields` whitelist guard.

#### Section "Restrictions" — Commercial purpose
> *"(ii) use of Geek Websites or APIs for the primary purpose of gaining advertising or subscription revenue"*

**Applicazione**: BGG API NON può essere il **fine** primario di subscription revenue.
**Mitigazione design**: BGG seed è MEZZO operativo (admin one-shot per popolare catalogo iniziale), non FINE del modello Pro/Premium #1739. Marketing Pro/Premium NON menziona "BGG import" come selling point. Documentato in §2 non-goals.

#### Section "Restrictions" — Market competition
> *"(iv) any use that competes with or displaces the market for BoardGameGeek"*

**Applicazione**: ambigua, interpretativa. Spada di Damocle principale.
**Analisi**: MeepleAI usa il catalogo per RAG/library/session/agenti AI — funzionalità **complementari** a BGG, non sostitutive del browsing/community/forum. L'utente che vuole "vedere voti & commenti su BGG" va su BGG, non su MeepleAI.
**Mitigazione design**:
- Catalogo MeepleAI NON è una clone-UI di `/boardgame/X` BGG-style
- NESSUN endpoint pubblico espone i dati seedati con attribuzione "data from BGG"
- Funzionalità AI/RAG/session sono il differenziatore — non un mirror di BGG
- FE Admin mostra "via Wikidata/BGG" SOLO in pagina admin (provenance UI), MAI all'utente finale

### 8.5.2 EU Database Directive 96/9/EC — Sui Generis Right

**Articolo 7**: "the maker of a database which shows that there has been qualitatively and/or quantitatively a substantial investment in either the obtaining, verification or presentation of the contents shall have the right to prevent extraction and/or re-utilization of the whole or of a substantial part".

Diritto **sui generis**, distinto dal copyright dei singoli dati.

**Applicato a BGG**:
- BGG ha probabilmente "substantial investment" (15+ anni di curation user-driven + verification staff) → DB sui generis applicabile
- Per importare 1000-2000 record fattuali:
  - In valore assoluto: numero importante
  - In percentuale: <1.5% di BGG (~150k+ giochi)
  - **"Substantial part"** è valutato per qualità e quantità (CJEU C-203/02 "The British Horseracing Board"). 1.5% qualitativo limitato (solo metadata, no rating/desc) è argomentabile **non-substantial**.

**Aggravante**: Articolo 7.5 vieta "repeated and systematic extraction of insubstantial parts" se "conflicts with normal exploitation of the database or unreasonably prejudices legitimate interests".
**Mitigazione design**:
- One-shot import per gioco (NO sync, NO scheduled re-extraction)
- Cap 100 IDs/bulk + rate-limit 5min impedisce systematic scraping
- BggTosWatcherJob mensile assicura che la nostra estrazione si fermi se BGG vieta esplicitamente

### 8.5.3 US Copyright — Feist Publications v. Rural Telephone Service (1991)

**Supreme Court ruling**: facts are NOT copyrightable. Solo "originality in selection, coordination, or arrangement" può attrarre "thin copyright" sul database aggregato, MA non sui singoli fatti.

**Applicato a metadata giochi**:
- Titolo, anno pubblicazione, designer name, publisher name, BGG ID, mechanics labels, players, time → **facts** = no copyright protezione
- Selection BGG (quali giochi includere nel loro catalogo) → thin copyright sull'aggregato BGG, NON sui singoli gioco
- Estrazione gioco-per-gioco di soli fatti = legalmente OK in US (no Feist violation)

**NON applicabile** ai seguenti campi (escluse dalla whitelist):
- `description` text → user-submitted, copyright dell'autore (o BGG come licensee)
- `image`/`thumbnail` → editor copyright (Kosmos, Asmodee, ecc.)
- `comments`/`reviews` → user-submitted, copyright autore
- `statistics`/`average rating` → aggregato derivato dall'investimento BGG = potenziale "thin copyright" sull'aggregato + DB sui generis EU

**Mitigazione design**: `BggImportFieldFilter.ForbiddenFields` hard-codifica l'esclusione + unit test guard. §7.2.

### 8.5.4 GDPR (EU Regulation 2016/679)

**Articolo 4(1)**: "personal data" = "any information relating to an identified or identifiable natural person".

**Applicato al design**:
- Designer names (es. "Klaus Teuber") = **personal data** se identificabile (vivente, identificato professionalmente)
- Publisher names = persona giuridica → fuori scope GDPR
- Artist names = personal data (se identificabile)
- NESSUN trattamento di PII utente finale in scope (admin import non tocca dati utenti)

**Base giuridica del trattamento** (Articolo 6):
- **6(1)(f) legitimate interest**: identificazione catalogo giochi è interesse legittimo MeepleAI (necessario per il servizio). Bilanciamento vs diritti dell'interessato: minimo intrusivo (dati professionali pubblici, no PII sensibile).
- Alternativa: **6(1)(e)** se MeepleAI è in public interest (improbabile), oppure **6(1)(a) consent** (non applicabile a designers terzi che non hanno consentito a MeepleAI specifically).

**Exemption "publicly available data"**: i designer/publisher names sono "publicly available data about public figures in the context of their professional activity" — typicamente fuori dai requisiti più stringenti GDPR (es. no DPIA necessaria).

**Diritto di cancellazione** (Articolo 17): se un designer richiede cancellazione, MeepleAI deve poter identificare il record. Mitigazione: `Provenance JSON` permette `DELETE WHERE designer_name = 'X' AND provider = 'wikidata'`.

**Mitigazione design**:
- Privacy policy update **OPTIONAL** (publicly available professional data)
- DPO consultation **NOT required** per questo scope
- Diritto cancellazione gestibile via `Provenance JSON` lookup

### 8.5.5 Wikidata License (CC0 1.0 Universal)

Fonte: `https://creativecommons.org/publicdomain/zero/1.0/`

> *"The person who associated a work with this deed has dedicated the work to the public domain by waiving all of his or her rights to the work worldwide under copyright law, including all related and neighboring rights, to the extent allowed by law."*

**Applicazione**: Wikidata data può essere usato senza restrizione, senza attribuzione richiesta.
**Zero rischio legale** per la parte Wikidata-primary del design.

**Best practice non-obbligatoria**: attribuzione "Powered by Wikidata data" nel footer di un About page MeepleAI è cortese ma non legalmente necessaria.

### 8.5.6 Pre-rollout legal checklist

Prima del rollout pubblico (mai prima di MVP staging interno):

- [ ] 1h consultazione legale per validare interpretazione "competes/displaces"
- [ ] Terms of Service MeepleAI aggiornati con clausola "publicly available data sources"
- [ ] User-Agent BGG con email `abuse@meepleai.app` attiva e monitorata
- [ ] BggTosWatcherJob attivo + alert configurato verso oncall
- [ ] Audit log export (`/admin/catalog/seeds/export`) funzionante per legal request
- [ ] Documentation: ADR-NNN dedicato a "Catalog seed legal posture" in `docs/for-claude/architecture/adr/`

## 9. Bounded contexts mapping

```
SharedGameCatalog BC (esistente, esteso)
├─ Domain/
│  ├─ Aggregates/SharedGame.cs              (esistente, riusato)
│  └─ ValueObjects/Provenance.cs            (nuovo: per-field source tracking)
├─ Application/
│  ├─ Commands/
│  │  ├─ EnqueueCatalogSeedCommand.cs       (single)
│  │  ├─ BulkEnqueueCatalogSeedsCommand.cs  (max 100)
│  │  ├─ ApproveCatalogSeedCommand.cs
│  │  └─ RejectCatalogSeedCommand.cs
│  ├─ Queries/
│  │  ├─ ListCatalogSeedsQuery.cs
│  │  └─ GetCatalogSeedByIdQuery.cs
│  ├─ Jobs/
│  │  └─ CatalogSeedFetchJob.cs             (Quartz 1 min, BatchSize=10)
│  ├─ Services/
│  │  ├─ ICatalogSeedAggregator.cs          (Wikidata > BGG chain)
│  │  ├─ ICatalogSeedStreamService.cs       (singleton SSE)
│  │  └─ IBggTosWatcherService.cs           (monthly ToS hash check)
│  └─ EventHandlers/
│     └─ CatalogSeedApprovedEventHandler.cs (insert/upsert in SharedGameCatalogEntry)
└─ Infrastructure/
   ├─ Entities/CatalogSeedDraftEntity.cs    (RowVersion, audit fields, IsDeleted)
   ├─ Providers/
   │  ├─ WikidataCatalogProvider.cs         (SPARQL HttpClient + Polly)
   │  ├─ BggCatalogProvider.cs              (XML API2 + BggImportFieldFilter)
   │  └─ BggImportFieldFilter.cs            (whitelist hard-coded)
   └─ Persistence/CatalogSeedDraftRepository.cs
```

## 10. Effort estimate

| Componente | Effort |
|---|---|
| DB migration + entity | 1h |
| Wikidata provider + tests | 4h |
| BGG provider + whitelist filter + tests | 4h |
| CatalogSeedAggregator + provenance | 3h |
| Quartz CatalogSeedFetchJob + tests | 3h |
| Application commands/queries + handlers | 5h |
| ICatalogSeedStreamService + SSE endpoint | 3h |
| Domain events + audit handler | 2h |
| BggTosWatcherService + Quartz schedule | 2h |
| FE riusato componenti #1835 + adaption | 6h |
| FE nuovi component (WikidataSearchForm, SeedPreviewPanel) | 4h |
| E2E + admin auth integration tests | 4h |
| Spec self-review + docs | 2h |
| **TOTAL** | **~43h** |

## 11. Open questions / future considerations

- **Bulk approve/reject**: deferred (MVP usa per-entry approve). Da valutare dopo feedback admin.
- **Excel import**: deferred (MVP solo CSV paste).
- **Stale entries refresh**: deferred. Quartz periodico per re-fetch entries Approved >6 mesi è raccomandato ma fuori MVP.
- **Multi-language support**: Wikidata supporta `labels.it`, `labels.de`. Da decidere se import multi-lang da subito o solo `labels.en`.
- **Lighthouse legal review**: prima del rollout pubblico, 1h consultazione legale raccomandata per validare interpretazione "competes with market" clausola.

## 12. Definition of Done

- [ ] DB migration applicata + reverted-clean test
- [ ] Wikidata provider con SPARQL + 5+ unit test (happy/missing/error/multi-lang/rate-limit)
- [ ] BGG provider con whitelist + 5+ unit test (happy/forbidden-field-filter/rate-limit/retry/timeout)
- [ ] `BggImportFieldFilter` unit test che fallisce se forbidden field viene aggiunto a allowed
- [ ] CatalogSeedAggregator con 3+ test (wd-only/bgg-only/merge)
- [ ] CatalogSeedFetchJob con 5+ unit test (no-eligible/batch-size/fault-tolerance/oldest-first)
- [ ] Admin endpoints con [Authorize Admin] + integration test
- [ ] SSE stream service con subscribe/publish/buffer test
- [ ] FE Admin UI con 10+ component test (Vitest) + 1 E2E Playwright
- [ ] Feature flag `AdminCatalogSeedEnabled` documentato in `/admin/config` + default false
- [ ] BggTosWatcherJob attivo + alert configurato
- [ ] User-Agent BGG documentato + `abuse@meepleai.app` email mailbox attiva
- [ ] Terms of Service aggiornato con clausola seed
- [ ] Audit log export funzionante (CSV con tutti i `domain_event_logs` per category catalog-seed)
- [ ] Documentation: `docs/for-developers/specs/2026-06-04-admin-catalog-seed.md` (spec) + `docs/superpowers/plans/2026-06-04-admin-catalog-seed-plan.md` (plan)
- [ ] Pre-rollout checklist (§8.5.6) completata prima di abilitare il feature flag in staging/prod
- [ ] ADR dedicato "Catalog seed legal posture" creato in `docs/for-claude/architecture/adr/`

---

🤖 Spec generated via brainstorming skill (superpowers v5.1.0) on 2026-06-04.
