# Gamebook Multi-Book Generalization — design

**Date**: 2026-05-19
**Status**: Draft (spec-panel review + Socratic brainstorm)
**Owner**: TBD
**Tracking issue**: TBD
**Type**: Domain refactor (assorbito da [#1320 Game Entity Reset](./2026-05-19-game-entity-reset.md) Phase 2)
**Depends on**: [#1320 Game Entity Reset](./2026-05-19-game-entity-reset.md) (must land first or jointly)
**Blocked by**: [#4228 AgentDefinition redesign](./2026-05-19-game-entity-reset.md#out-of-scope-deferred) per invariante `AgentDefinition.kb_links` (vedi §3)

---

## 1. Context

La spec [2026-05-07 Libro Game Nanolith demo design](../../superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md) ha fissato l'assunzione che i giochi-libro abbiano sempre **2 PDF indicizzati distinti** (`Press Start` tutorial + `Rules` regolamento) più **2 libri fisici narrativi** (`Storybook` + `Encounter Book`). Questa contingenza di Nanolith è stata promossa a **schema di dominio** in 6 punti:

| # | Posizione | Codifica hardcoded |
|---|---|---|
| 1 | §2 N1 — Goal "Q&A setup & tutorial Press Start KB" | Tutorial = libro separato |
| 2 | §2 N2 — Goal "Q&A regole in-game Rules KB" | Regole = libro separato |
| 3 | §8.1 #3-#4 | Pre-condizione: 2 PDF indicizzati distinti |
| 4 | §8.1 #5 | Agent "linked a **entrambi** i KB" |
| 5 | §10 D2 | "4 documenti fisici Nanolith, di cui 2 PDF (Rules + Press Start)" |
| 6 | `GamebookPageType` enum a 2 valori `{Storybook=0, Encounter=1}` | Tipo libro narrativo hardcoded |

### Casi reali che rompono il modello

| Gioco | Configurazione libri | Conseguenza per modello attuale |
|---|---|---|
| **Nanolith** | 4 libri (PressStart + Rules + Storybook + Encounter) | OK (caso target) |
| **Fighting Fantasy** | 1 libro all-in-one (tutorial+regole+narrativa+encounter) | KO: 1 PDF ≠ 2 KB attesi; PageType enum non rappresentabile |
| **Maracaibo** | 2 libri (Rulebook tutorial+regole, ParagraphBook narrativa) | KO: tutorial NON in libro separato |
| **Tainted Grail** | 5+ libri (Rulebook + ExplorationJournal + ChapterTome×N) | KO: 1 libro narrativo ≠ N tomi |
| **Choose Your Own Adventure** | 1 libro fisico, no PDF indicizzabile | KO: 0 KB indicizzati attesi 2 |
| **7th Continent** | 0 libri (card-based) | KO: companion disabilitato silenziosamente |

### Why now

- Il modello deve generalizzare PRIMA che arrivi il secondo gioco-libro nel catalogo Aaron
- #1320 Game Entity Reset offre una finestra di refactor in cui possiamo introdurre `GameBook` aggregato senza migration costs aggiuntivi
- Dati dogfood Aaron sono già destinati a essere persi dal reset #1320 → nessun blocker su data preservation

### Non-goals

- Migration / backfill di dati esistenti (assorbito da #1320 nuke)
- Promotion workflow personal→community GameBook (deferred a iter successivo)
- AgentDefinition redesign (deferred a #4228)
- Frontend admin CRUD UI per GameBook community (scope-cap: solo backend domain + minimal user-facing changes)

---

## 2. Problem statement

La spec attuale confonde **tre dimensioni** che dovrebbero essere ortogonali:

1. **Funzione informativa** richiesta dall'utente (setup / regola in-game / paragrafo narrativo / encounter)
2. **Artefatto fisico** che la trasporta (Press Start booklet / Rulebook / Storybook / Encounter Book / All-in-one tome)
3. **Stato digitale** in MeepleAI (PDF indicizzato come KB / cartaceo→foto live / non indicizzabile)

Nanolith ha 4 artefatti distinti, 2 dei quali PDF → la spec ha promosso questa contingenza a **schema**. Il modello generalizzato deve esprimere **N libri arbitrari** per gioco, ciascuno con **ruoli multipli** (un libro può fare tutorial + reference insieme), **scheme di paragrafazione differenti** (numerati, paginati, sezionati, none), e **stato di indicizzazione indipendente** (PDF disponibile o solo cartaceo).

---

## 3. Target schema (post-#1320)

### 3.1 Nuovo aggregato `GameBook` (`GameManagement` BC)

```csharp
namespace Api.BoundedContexts.GameManagement.Domain.Entities;

public sealed class GameBook
{
    public Guid Id { get; private set; }
    public GameRef GameRef { get; private set; } = default!;  // discriminator post-#1320
    public Guid? OwnerUserId { get; private set; }            // null = community (admin-managed), set = personal
    public string DisplayName { get; private set; } = default!;
    public GameBookRole Roles { get; private set; }            // flag enum, multi-valued
    public ParagraphScheme ParagraphScheme { get; private set; }
    public string Language { get; private set; } = default!;   // ISO 639-1, 2 chars
    public bool SequentialRead { get; private set; }
    public Guid? KbSourceDocId { get; private set; }           // FK pdf_documents, null se cartaceo-only
    public bool PhysicalOnly { get; private set; }             // true = solo foto-live, no PDF indicizzabile
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    public static GameBook CreateCommunity(
        GameRef gameRef,
        string displayName,
        GameBookRole roles,
        ParagraphScheme paragraphScheme,
        string language,
        bool sequentialRead,
        Guid? kbSourceDocId,
        bool physicalOnly,
        Guid createdBy)
    {
        // validation + factory
    }

    public static GameBook CreatePersonal(
        GameRef gameRef,
        Guid ownerUserId,
        string displayName,
        GameBookRole roles,
        ParagraphScheme paragraphScheme,
        string language,
        bool sequentialRead,
        Guid? kbSourceDocId,
        bool physicalOnly)
    {
        // validation + factory
    }

    // mutators: UpdateRoles, AttachKbSource, DetachKbSource, Rename, SoftDelete
}

[Flags]
public enum GameBookRole
{
    None = 0,
    Tutorial = 1,
    RulesReference = 2,
    Narrative = 4,
    Encounter = 8,
    Lore = 16,
    Setup = 32,
}

public enum ParagraphScheme
{
    None = 0,             // no paragraph addressing scheme (e.g. plain rulebook)
    ParagraphNumber = 1,  // numbered paragraphs (§N, §147)
    PageNumber = 2,       // page-addressed (pag. 47)
    Section = 3,          // section-addressed (encounter E7, chapter 3.2)
}
```

### 3.2 Tabella `game_books`

```sql
CREATE TABLE game_books (
    id                UUID         PRIMARY KEY,
    game_ref_id       UUID         NOT NULL,         -- post-#1320 discriminator
    game_ref_kind     SMALLINT     NOT NULL,         -- 0=Shared, 1=Private
    owner_user_id     UUID         NULL REFERENCES auth_users(id),
    display_name      VARCHAR(120) NOT NULL,
    roles             INTEGER      NOT NULL,         -- GameBookRole flags
    paragraph_scheme  SMALLINT     NOT NULL,         -- ParagraphScheme enum
    language          CHAR(2)      NOT NULL,
    sequential_read   BOOLEAN      NOT NULL DEFAULT false,
    kb_source_doc_id  UUID         NULL REFERENCES pdf_documents(id),
    physical_only     BOOLEAN      NOT NULL DEFAULT false,
    created_at        TIMESTAMPTZ  NOT NULL,
    updated_at        TIMESTAMPTZ  NOT NULL,
    deleted_at        TIMESTAMPTZ  NULL,
    created_by        UUID         NOT NULL,
    updated_by        UUID         NULL,
    row_version       BYTEA        NOT NULL
);

CREATE INDEX ix_game_books_game_ref ON game_books(game_ref_kind, game_ref_id, deleted_at);
CREATE INDEX ix_game_books_owner_user_id ON game_books(owner_user_id, deleted_at)
    WHERE owner_user_id IS NOT NULL;

-- Invariante: 1 PDF non può essere kb_source di 2 libri community simultanei.
-- Personal books con owner_user_id != null sono esclusi (più utenti possono linkare lo stesso PDF privatamente).
CREATE UNIQUE INDEX ux_game_books_kb_source_community
    ON game_books(kb_source_doc_id)
    WHERE kb_source_doc_id IS NOT NULL
      AND owner_user_id IS NULL
      AND deleted_at IS NULL;

-- Coerenza: physical_only=true implica kb_source_doc_id IS NULL
ALTER TABLE game_books ADD CONSTRAINT chk_game_books_physical_kb_coherence
    CHECK ((physical_only = true AND kb_source_doc_id IS NULL)
        OR (physical_only = false));
```

### 3.3 Tabelle gamebook esistenti — schema generalizzato

Tutte le tabelle gamebook esistenti vengono **droppate da #1320 Phase 3** e ri-create con questo schema nella initial migration post-reset:

```sql
-- =================== gamebook_campaign_sessions (SessionTracking BC) ===================
CREATE TABLE gamebook_campaign_sessions (
    id              UUID         PRIMARY KEY,
    game_ref_id     UUID         NOT NULL,                       -- discriminator post-#1320
    game_ref_kind   SMALLINT     NOT NULL,                       -- 0=Shared, 1=Private
    owner_user_id   UUID         NOT NULL REFERENCES auth_users(id),
    title           VARCHAR(120) NOT NULL,
    party_json      JSONB        NULL,
    scoring_json    JSONB        NULL,
    created_at      TIMESTAMPTZ  NOT NULL,
    updated_at      TIMESTAMPTZ  NOT NULL,
    deleted_at      TIMESTAMPTZ  NULL,
    created_by      UUID         NOT NULL,
    updated_by      UUID         NULL,
    row_version     BYTEA        NOT NULL
);
-- Note: GamebookProgress VO rimosso. Last paragraph è ora per-book in tabella separata.

CREATE INDEX ix_gamebook_campaign_sessions_owner
    ON gamebook_campaign_sessions(owner_user_id, deleted_at);
CREATE INDEX ix_gamebook_campaign_sessions_game_ref
    ON gamebook_campaign_sessions(game_ref_kind, game_ref_id, deleted_at);

-- =================== gamebook_session_book_progress (SessionTracking BC) ===================
-- NEW: traccia progress per-book all'interno della stessa campagna
CREATE TABLE gamebook_session_book_progress (
    id                   UUID         PRIMARY KEY,
    campaign_session_id  UUID         NOT NULL REFERENCES gamebook_campaign_sessions(id) ON DELETE CASCADE,
    game_book_id         UUID         NOT NULL REFERENCES game_books(id),
    last_location        VARCHAR(40)  NOT NULL,                  -- "§289" | "page-47" | "E7"
    history_json         JSONB        NOT NULL DEFAULT '[]'::jsonb,
    last_visited_at      TIMESTAMPTZ  NOT NULL,
    notes_json           JSONB        NULL,
    UNIQUE (campaign_session_id, game_book_id)
);

-- =================== gamebook_translated_paragraphs (SessionTracking BC) ===================
CREATE TABLE gamebook_translated_paragraphs (
    id                       UUID         PRIMARY KEY,
    campaign_session_id      UUID         NOT NULL REFERENCES gamebook_campaign_sessions(id) ON DELETE CASCADE,
    game_book_id             UUID         NOT NULL REFERENCES game_books(id),  -- NEW (era PageType enum)
    photo_artifact_id        UUID         NOT NULL REFERENCES gamebook_photo_artifacts(id),
    paragraph_number         INTEGER      NOT NULL,
    source_text_en           TEXT         NOT NULL,
    translated_text_it       TEXT         NOT NULL,
    applied_glossary_terms   TEXT[]       NOT NULL DEFAULT '{}',
    glossary_snapshot_jsonb  JSONB        NULL,
    ocr_confidence           DECIMAL(3,2) NULL,
    llm_provider             VARCHAR(50)  NULL,
    llm_cost_eur             DECIMAL(6,4) NULL,
    created_at               TIMESTAMPTZ  NOT NULL,
    created_by               UUID         NOT NULL,
    UNIQUE (campaign_session_id, game_book_id, paragraph_number)
);
-- Note: GamebookPageType enum eliminato. Discriminante è ora GameBookId FK.

-- =================== gamebook_photo_artifacts (SessionTracking BC) ===================
CREATE TABLE gamebook_photo_artifacts (
    id                          UUID         PRIMARY KEY,
    campaign_session_id         UUID         NOT NULL REFERENCES gamebook_campaign_sessions(id) ON DELETE CASCADE,
    game_book_id                UUID         NOT NULL REFERENCES game_books(id),  -- NEW
    s3_key                      VARCHAR(500) NOT NULL,
    ocr_confidence_avg          DECIMAL(3,2) NULL,
    detected_paragraphs_jsonb   JSONB        NULL,
    created_at                  TIMESTAMPTZ  NOT NULL,
    expires_at                  TIMESTAMPTZ  NOT NULL
);

-- =================== gamebook_glossary_entries (SessionTracking BC) ===================
CREATE TABLE gamebook_glossary_entries (
    id                       UUID         PRIMARY KEY,
    campaign_session_id      UUID         NOT NULL REFERENCES gamebook_campaign_sessions(id) ON DELETE CASCADE,
    term_en                  VARCHAR(200) NOT NULL,
    term_it                  VARCHAR(200) NOT NULL,
    first_seen_paragraph     INTEGER      NULL,
    first_seen_book_id       UUID         NULL REFERENCES game_books(id),    -- NEW: di quale libro il primo seen
    user_edited              BOOLEAN      NOT NULL DEFAULT false,
    created_at               TIMESTAMPTZ  NOT NULL,
    updated_at               TIMESTAMPTZ  NOT NULL
);
-- Collision detection invariata (per-campaign term_en uniqueness su user_edited entries gestita lato app).
```

### 3.4 Estensione `pdf_document_chunks` per role-aware RAG

```sql
-- pdf_document_chunks è ri-creato dalla initial migration post-#1320.
-- Lo schema esteso include role_tags da subito (no ALTER post-fact).
ALTER TABLE pdf_document_chunks ADD COLUMN role_tags INTEGER NOT NULL DEFAULT 0;
-- 0 = uncategorized (populated dal RoleClassifierService at ingestion)
CREATE INDEX ix_pdf_document_chunks_role_tags
    ON pdf_document_chunks(role_tags)
    WHERE role_tags != 0;
```

`role_tags` usa stesso `GameBookRole` flag enum di `game_books.roles`. Match cross-table: `chunk.role_tags & query_role_hint > 0` → boost retrieval.

---

## 4. Use case generalization

### 4.1 Goal restructure

| Goal precedente (spec 2026-05-07) | Goal generalizzato | Note |
|---|---|---|
| N1 — Q&A setup Press Start | **N1' — Q&A informativo (pre-session SLO)** | Retrieval su tutti i KB del gioco, query_role_hint = `{Tutorial, RulesReference, Setup}`, P95 8s |
| N2 — Q&A regole in-game | **N1' — Q&A informativo (in-session SLO)** | Stesso N1', diverso SLO: P95 5s, role-hint = `{RulesReference}` |
| N3 — Translate Storybook | **N2' — Translate paragrafo narrativo** | `bookId` opzionale nell'API, auto-resolved se gioco ha 1 solo libro con role `Narrative` |
| N4 — Resume + glossary | **N3' — Resume cross-day per-book progress** | Resume UI mostra last_location per ciascun libro narrativo della campagna |

**Numerazione**: 4 goal → 3 goal generalizzati. N1+N2 merged perché differenza era solo SLO, non requirement utente.

### 4.2 Pre-condizioni generalizzate (sostituisce §8.1 spec 2026-05-07)

| # | Pre-condizione |
|---|---|
| 1 | Account dogfood con role `SuperAdmin` (Aaron) o user normale (Sara) |
| 2 | Gioco target esiste come `SharedGame` o `PrivateGame` (riferito via `GameRef`) |
| 3 | ≥ 1 `GameBook` linked al gioco con `kb_source_doc_id` indicizzato (embedding_status='complete') E `roles ⊇ {Tutorial OR RulesReference}` per abilitare N1' |
| 4 | (Per N2') ≥ 1 `GameBook` con `roles ⊇ {Narrative}` AND `paragraph_scheme != None` per abilitare photo-translate |
| 5 | `AgentDefinition.kb_links == { book.kb_source_doc_id : book ∈ game.books WHERE kb_source_doc_id IS NOT NULL AND owner_user_id IS NULL }` (community books only per ora). **Blocked by #4228** per implementazione invariante. |
| 6 | LLM provider chain configurato (OpenRouter primary) |
| 7 | R2 storage configurato per foto temp |
| 8 | Background purge foto 24h schedulato |

### 4.3 Seed Nanolith come configurazione di esempio

Lo schema generalizzato deve poter rappresentare la configurazione Nanolith attuale come **caso particolare**, non come default:

```csharp
// Seed admin script (esecuzione post-reset #1320 Phase 3)
var nanolithSharedGameId = sharedGameRepo.FindByTitle("Nanolith").Id;
var nanolithRef = GameRef.Shared(nanolithSharedGameId);

var nanolithBooks = new[]
{
    GameBook.CreateCommunity(nanolithRef, "Press Start",
        roles: GameBookRole.Tutorial | GameBookRole.Setup,
        paragraphScheme: ParagraphScheme.None, language: "en",
        sequentialRead: false, kbSourceDocId: pressStartPdfId,
        physicalOnly: false, createdBy: adminId),

    GameBook.CreateCommunity(nanolithRef, "Rules",
        roles: GameBookRole.RulesReference,
        paragraphScheme: ParagraphScheme.None, language: "en",
        sequentialRead: false, kbSourceDocId: rulesPdfId,
        physicalOnly: false, createdBy: adminId),

    GameBook.CreateCommunity(nanolithRef, "Storybook",
        roles: GameBookRole.Narrative,
        paragraphScheme: ParagraphScheme.ParagraphNumber, language: "en",
        sequentialRead: true, kbSourceDocId: null,
        physicalOnly: true, createdBy: adminId),

    GameBook.CreateCommunity(nanolithRef, "Encounter Book",
        roles: GameBookRole.Encounter,
        paragraphScheme: ParagraphScheme.Section, language: "en",
        sequentialRead: false, kbSourceDocId: null,
        physicalOnly: true, createdBy: adminId),
};
```

### 4.4 Esempi cross-gioco (validation modello)

**Fighting Fantasy: City of Thieves** — 1 libro all-in-one:

```csharp
GameBook.CreateCommunity(ffRef, "City of Thieves",
    roles: GameBookRole.Tutorial | GameBookRole.RulesReference
         | GameBookRole.Narrative | GameBookRole.Encounter,
    paragraphScheme: ParagraphScheme.ParagraphNumber, language: "en",
    sequentialRead: false, kbSourceDocId: cityOfThievesPdfId,
    physicalOnly: false, createdBy: adminId);
```

**Maracaibo** — 2 libri (rulebook+tutorial in 1, narrative in altro):

```csharp
GameBook.CreateCommunity(maracaiboRef, "Rulebook",
    roles: GameBookRole.Tutorial | GameBookRole.RulesReference,
    paragraphScheme: ParagraphScheme.None, language: "en",
    sequentialRead: false, kbSourceDocId: rulebookPdfId,
    physicalOnly: false, createdBy: adminId);

GameBook.CreateCommunity(maracaiboRef, "Story Book",
    roles: GameBookRole.Narrative,
    paragraphScheme: ParagraphScheme.ParagraphNumber, language: "en",
    sequentialRead: true, kbSourceDocId: null,
    physicalOnly: true, createdBy: adminId);
```

**7th Continent** — 0 libri (card-based, companion N/A): nessun `GameBook` creato. FM-19 handler.

---

## 5. RAG ingestion pipeline change (role-aware)

### 5.1 At-ingestion classifier (hybrid)

```
PDF upload (KB) → smoldocling extract → chunks + headings metadata
    ↓
[NEW] RoleClassifierService.ClassifyChunks(chunks)
    │
    ├── Step 1: rule-based via H1/H2 keywords
    │   • Heading matches "Setup|Quick Start|Learn to Play|Getting Started" → +Tutorial
    │   • Heading matches "Setup|Game Setup|Components" → +Setup
    │   • Heading matches "Rules|Reference|Combat|Magic|Phases" → +RulesReference
    │   • Heading matches "Adventures|Chapters|Story|Paragraph \d+" → +Narrative
    │   • Heading matches "Encounters|Scenarios|Combat Sheets" → +Encounter
    │   • Heading matches "Lore|Background|History|Codex" → +Lore
    │
    ├── Step 2: confidence check
    │   • Se rule match ≥ 0.7 (heading matches ≥ 1 pattern senza ambiguità) → assign role_tags
    │   • Altrimenti → mark for LLM fallback
    │
    └── Step 3: LLM fallback (provider chain: DeepSeek primary → OpenRouter haiku secondary, batch 10 chunks/call)
        • Prompt: "Classify this game manual chunk: [tutorial|rules|narrative|encounter|lore|setup]"
        • Multi-label output (può assegnare più role tags)
        • Costo stimato: ~$0.005 per 100 chunks su DeepSeek (sub-cent per PDF da 50 pagine), ~$0.01 fallback haiku
        • Vedi OQ-2 per razionale della provider chain
    ↓
chunks with role_tags persisted in pdf_document_chunks.role_tags
embeddings inserted in pgvector_embeddings (invariati)
```

### 5.2 At-query intent classifier

```
user query (chat) → IntentClassifierService.ClassifyQuery(query)
    │
    ├── Regex fast-path (high confidence patterns):
    │   • "setup|preparare|come si comincia|prima partita" → {Tutorial, Setup}
    │   • "qual è la regola|come funziona|posso fare X" → {RulesReference}
    │   • "leggi paragrafo|prossimo §|capitolo" → {Narrative}
    │   • "incontro|scontro|combatti" → {Encounter}
    │
    ├── Default fallback: {RulesReference} (90%+ delle query in-session)
    │
    └── (Future, deferred) LLM-based fallback per query ambigui
    ↓
query_role_hint passed to retrieval layer
```

### 5.3 Retrieval boost

```csharp
// HybridSearchService re-ranker extension
public double ComputeChunkScore(PdfDocumentChunk chunk, SearchQuery query)
{
    var baseScore = semanticScore + bm25Score + recencyScore;  // existing

    // NEW: role match signal
    if (query.RoleHint != GameBookRole.None
        && (chunk.RoleTags & query.RoleHint) != GameBookRole.None)
    {
        baseScore += 0.15;  // empirical weight, tuning per dogfood feedback
    }

    return baseScore;
}
```

Costo run-time aggiuntivo: trascurabile (1 AND bitwise per chunk in re-ranker).

---

## 6. Failure modes

Aggiunti a §6.1 spec 2026-05-07 (FM-1..FM-18 invariati):

| FM# | Configurazione patologica | Behavior atteso |
|---|---|---|
| FM-19 | Gioco con 0 `GameBook` | UI library: badge "Modalità companion non disponibile". Chat panel + photo-translate disabilitati con tooltip esplicativo. |
| FM-20 | Gioco con N>0 books ma tutti `physical_only=true` | Chat Q&A disabilitato (no KB). Photo-translate abilitato per books con role `Narrative`. |
| FM-21 | `GameBook.roles = None` (empty set, configurazione invalida) | Validation blocca create. Background consistency check segnala admin se trova row con roles=0. |
| FM-22 | Multipli `GameBook` con role `Narrative` + `paragraph_scheme = ParagraphNumber` con namespace overlap (es. 2 tomi entrambi §1-300) | Photo-translate API richiede `bookId` esplicito. Errore 400 con messaggio "Specifica quale libro narrativo stai leggendo". |
| FM-23 | `AgentDefinition.kb_links` divergente da `game.books WHERE kb_source_doc_id != null AND owner_user_id IS NULL` (drift) | Background consistency job rileva drift, auto-corregge linkando i mancanti, logga audit event. Implementazione **deferred a #4228**. |
| FM-24 | Personal `GameBook` (owner_user_id != null) usato in `gamebook_translated_paragraphs` di campagna di altro utente | Validation in command handler: `gamebook_translated_paragraphs.game_book_id` deve avere `book.owner_user_id IN (null, session.owner_user_id)`. Personal books di altri utenti non accessibili. |

---

## 7. Implementation strategy

### 7.1 Integration con #1320 phasing

GameBook generalization viene **assorbita nelle phase #1320**:

| Phase #1320 | Aggiunte da questo spec |
|---|---|
| **Phase 1** (backup infrastructure) | Invariata |
| **Phase 2** (code refactor big bang) | **Estesa**: include nuova entità `GameBook` + mapping EF + delete `GamebookPageType` enum + refactor `GamebookCampaignSession` (Progress VO removal) + refactor `TranslatedParagraph` factory (aggiunge `gameBookId`) + nuovo `RoleClassifierService` + `IntentClassifierService` + `HybridSearchService` re-ranker extension |
| **Phase 3** (execute reset) | **Estesa**: include seed admin di `GameBook` per giochi esistenti (parte del re-seed post-reset). Nanolith ottiene 4 books community, altri giochi 1 book inferito da PDF singolo se presente |

### 7.2 Code change inventory

**Backend** (.NET):

**Nuovi**:
- `BoundedContexts/GameManagement/Domain/Entities/GameBook.cs`
- `BoundedContexts/GameManagement/Domain/ValueObjects/GameBookRole.cs` (flag enum)
- `BoundedContexts/GameManagement/Domain/ValueObjects/ParagraphScheme.cs`
- `BoundedContexts/GameManagement/Domain/Repositories/IGameBookRepository.cs`
- `BoundedContexts/GameManagement/Infrastructure/Repositories/GameBookRepository.cs`
- `BoundedContexts/GameManagement/Application/Commands/{Create,Update,SoftDelete,AttachKbSource,DetachKbSource}GameBook*` + Validator + Handler
- `BoundedContexts/GameManagement/Application/Queries/{Get,List}GameBook*` + Handler
- `BoundedContexts/SessionTracking/Domain/Entities/SessionBookProgress.cs`
- `BoundedContexts/SessionTracking/Domain/Repositories/ISessionBookProgressRepository.cs`
- `BoundedContexts/KnowledgeBase/Application/Services/RoleClassifierService.cs`
- `BoundedContexts/KnowledgeBase/Application/Services/IntentClassifierService.cs`

**Refactor**:
- `BoundedContexts/SessionTracking/Domain/Entities/GamebookCampaignSession.cs` — rimuove `Progress` VO, aggiunge collection `BookProgress`
- `BoundedContexts/SessionTracking/Domain/Entities/TranslatedParagraph.cs` — `Create()` factory aggiunge `gameBookId` parameter
- `BoundedContexts/SessionTracking/Domain/Entities/GamebookPhotoArtifact.cs` — aggiunge `GameBookId` field
- `BoundedContexts/KnowledgeBase/Application/Services/HybridSearchService.cs` — re-ranker include `role_match` signal
- `BoundedContexts/KnowledgeBase/Application/Pipeline/PdfIngestionPipeline.cs` — invoca `RoleClassifierService` su chunks
- `Infrastructure/Migrations/<initial-post-1320>.cs` — schema include `game_books` + delta su `pdf_document_chunks` + delta su gamebook tables

**Delete**:
- `BoundedContexts/SessionTracking/Domain/Enums/GamebookPageType.cs`
- `BoundedContexts/SessionTracking/Domain/ValueObjects/GamebookProgress.cs`
- Tutti i call site di `GamebookPageType` (~10-15 files, principalmente `TranslatedParagraph.Create()` callers e DTO mappers)

**Frontend** (Next.js / TypeScript):

**Refactor minimo (scope-cap iter immediato)**:
- Chat panel: rimuove eventuale dropdown "Press Start vs Rules" se presente (uniforma a "chiedi qualcosa" generico)
- Photo-translate UI: se gioco ha N>1 narrative books, mostra book picker; se ha 1 solo book auto-select trasparentemente
- Resume UI: lista last_location per ciascun libro narrativo della campagna (era 1 sola riga "ultimo §N")

**Deferred a iter successivo** (out of scope iter immediato):
- Admin CRUD UI per `GameBook` community (admin gestisce via seed/script)
- User wizard per creare personal `GameBook` (S2 flow)
- BookPicker componente avanzato per N>3 books

### 7.3 Test impact

**Unit (backend)**:
- `GameBook` factory + invarianti (community vs personal, physical vs indexed, role coherence)
- `RoleClassifierService` rule-based matchers + LLM fallback mock
- `IntentClassifierService` regex patterns
- `HybridSearchService` re-ranker con role_match signal

**Integration (Testcontainers)**:
- `GameBookRepository` CRUD + unique constraint kb_source community
- `SessionBookProgressRepository` 1-N relationship
- `TranslatedParagraph` + `game_book_id` FK enforcement
- Migration apply post-#1320 reset (test idempotency)

**E2E (Playwright)**:
- Scenari Gherkin §3 aggiornati: rimossi tag `@dogfood-nanolith-press-start-rules` hardcoded, generalizzati a `@gamebook-multi-config`
- Nuovi scenari: Caso A (Fighting Fantasy all-in-one), Caso B (Maracaibo senza tutorial separato), Caso C (0 books)

### 7.4 Scope-cap effort estimate

| Component | Effort (giorni) |
|---|---|
| Backend domain + handlers (GameBook + refactor) | 3-4 |
| RAG pipeline (RoleClassifier + IntentClassifier + re-ranker) | 2-3 |
| EF migration consolidation (in #1320) | 0.5 |
| Seed script + Nanolith re-seed | 0.5 |
| Frontend minimal refactor (book picker + resume list) | 1-2 |
| Test (unit + integration + E2E) | 2-3 |
| **Totale** | **9-13 giorni** |

Indicativo: ~2-3 settimane di lavoro effettivo distribuiti su 1 sprint, allineato con #1320 Phase 2 big bang refactor.

---

## 8. Decisions log

| # | Decisione | Razionale |
|---|---|---|
| D14 | `GameBook` aggregato in `GameManagement` BC con `roles` flag enum multi-valued | Cattura variabilità inter-gioco (1..N libri, role overlap, all-in-one). Multi-valued necessario per giochi con libro all-in-one. |
| D15 | N1+N2 mergiati in N1' "Q&A informativo" con 2 contesti SLO (pre/in-session) | Stakeholder need identico, solo SLO differente. Le metriche separate restano in implementation tests. |
| D16 | `GamebookProgress` VO sostituito da `gamebook_session_book_progress` (1-N) | Supporta multipli libri narrativi (Tainted Grail, ISS Vanguard). Per Nanolith Iter 1 esiste 1 sola riga per campagna (1 solo libro narrativo). |
| D17 | Photo-translate API accetta `bookId` opzionale, auto-resolved se gioco ha 1 solo narrative book | Backward-compatible. Errore 400 esplicito se ambiguo. |
| D18 | `GameBook.OwnerUserId` nullable (null=community admin-managed, set=personal user-owned, private-by-default) | Supporto S1+S2 scenario (admin community + user personal per giochi propri). S3 publish-to-community deferred. |
| D19 | Chunk-level `role_tags` (flag enum) + hybrid classifier (rule + LLM fallback) at ingestion | Role-aware retrieval per giochi all-in-one. Costo aggiuntivo trascurabile. |
| D20 | Reset assorbito da #1320 — NO migration script separato | Single deploy. Dati dogfood Aaron già sacrificati dal reset #1320. |
| D21 | `GamebookPageType` enum **rimosso**, sostituito da `GameBookId` FK | Generalizzazione N libri. PageType è caso particolare di 2 valori che non scala. |
| D22 | `GameRef` discriminator (`game_ref_id + game_ref_kind`) usato su `game_books` e `gamebook_campaign_sessions` | Allineamento #1320: supporta sia SharedGame community che PrivateGame personal. |
| D23 | Frontend scope-cap iter immediato: solo refactor minimo (book picker condizionale + resume list). Admin CRUD UI + user wizard deferred | Backend domain è il valore primario. UI può essere iterata dopo prima validazione modello. |
| D24 | Invariante `AgentDefinition.kb_links` documentata ma implementazione **bloccata da #4228** | AgentDefinition viene re-implementata in #4228. Implementare invariante prima è waste. |

---

## 9. Open questions (carry-forward)

1. **OQ-1**: Per `GameBook` community management — chi può creare/editare nel S1 flow? Solo `SuperAdmin`, o anche role `Admin` curatori community? **Provisional**: SuperAdmin only Iter 1, espandibile Iter 2 se Sara/altri admin curatori emergono.

2. **OQ-2**: `RoleClassifierService` LLM fallback usa quale modello? `anthropic-haiku-3` (cheap, ~$0.25/1M token) o `deepseek` (cheaper, già nello stack)? **Decisione implementativa**: prima implementazione usa DeepSeek (default chain), fallback OpenRouter haiku se DeepSeek fail.

3. **OQ-3**: Migration test idempotency — la migration consolidata post-#1320 può essere ri-eseguita su DB già con `game_books` table? **Risposta tecnica**: EF Core migration tracking via `__EFMigrationsHistory` previene re-apply, ma test di idempotency `dotnet ef database update` × 2 consecutive deve passare in CI.

4. **OQ-4**: `Lore` role serve davvero? Nessun goal Iter 1 lo usa. **Decisione**: mantenere nel enum (cost zero), ma non implementare classifier rules per Lore Iter 1. Future-proof per giochi tipo Tainted Grail con codex.

5. **OQ-5**: Quando un PDF di un community `GameBook` viene aggiornato (nuovo upload sostituisce vecchio), i `gamebook_translated_paragraphs` che hanno cached il vecchio testo `source_text_en` restano validi? **Decisione**: SI, mantenuti come historical audit trail. Background job può segnalare divergenza ma non auto-invalidare. Out of scope iter immediato.

6. **OQ-6**: `RoleClassifierService` rule patterns sono in **inglese** (heading keyword tipo "Setup", "Rules", "Combat"). Per giochi futuri con PDF in italiano (es. edizioni italiane di Tainted Grail o giochi Cranio Creations), serve estensione bilingue (IT). **Provisional**: aggiungere set patterns IT in classifier config (es. "Preparazione|Regole|Combattimento|Avventure"). Implementazione defer a quando emerge il primo PDF IT nel catalogo (Aaron dogfood usa PDF EN, no impact iter immediato).

---

## 10. Definition of Done

- [ ] `GameBook` entity + repository + 4 commands + 2 queries implementati
- [ ] `gamebook_session_book_progress` table + entity + repository implementati
- [ ] `gamebook_translated_paragraphs.game_book_id` FK aggiunto + factory refactored
- [ ] `gamebook_photo_artifacts.game_book_id` FK aggiunto + repository refactored
- [ ] `GamebookPageType` enum + tutti i call site eliminati (grep restituisce empty)
- [ ] `GamebookProgress` VO eliminato (sostituito da `SessionBookProgress` collection)
- [ ] `RoleClassifierService` implementato (rule + LLM fallback) + unit tests
- [ ] `IntentClassifierService` implementato (regex + default fallback) + unit tests
- [ ] `HybridSearchService` re-ranker include `role_match` signal + integration tests
- [ ] EF migration consolidata in #1320 initial migration post-reset (idempotency test passa)
- [ ] Nanolith re-seed admin script crea 4 `GameBook` community + 4 corretti `role_tags` su Press Start + Rules chunks
- [ ] Almeno 2 esempi cross-gioco seed dataset (Fighting Fantasy all-in-one + Maracaibo 2-libri) testati in E2E
- [ ] Frontend minimal refactor: book picker condizionale (solo se gioco ha N>1 narrative books) + resume UI mostra per-book progress
- [ ] Scenari Gherkin §3 spec 2026-05-07 aggiornati con tag `@gamebook-multi-config` + 3 nuovi scenari (Caso A/B/C)
- [ ] Failure modes FM-19..FM-24 implementati + integration tests
- [ ] Documentazione: `CLAUDE.md` aggiornata con `GameBook` in tabella bounded context, link a questo spec
- [ ] PR mergiato su `main-dev` come parte di #1320 Phase 2 (single big bang PR) o follow-up PR coordinato

---

## 11. References

- Spec originale: [2026-05-07 Libro Game Nanolith Demo Design](../../superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md)
- Spec dipendenza: [2026-05-19 Game Entity Reset (#1320)](./2026-05-19-game-entity-reset.md)
- Future dipendenza: #4228 AgentDefinition redesign (per FM-23 invariante)
- Spec-panel review session (2026-05-19): expert panel critique Wiegers + Fowler + Cockburn + Newman + Adzic + Nygard
- Brainstorm Socratico (2026-05-19): risoluzione OQ-1 (S1+S2), OQ-2 (chunk-level role tagging), OQ-3 (nuke + reseed via #1320)
- CLAUDE.md bounded context table — sezione "DDD Bounded Contexts" deve aggiungere `GameBook` come parte di `GameManagement`
