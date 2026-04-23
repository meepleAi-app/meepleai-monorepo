# Mechanic Extractor AI-first — Piano Tecnico M1.1–M1.3

**Stato**: Proposta implementativa
**Data**: 2026-04-23
**Scope**: Milestone M1 — Core Pipeline (issue ME-M1.1 → ME-M1.3, #523-#525)
**Related**: [ADR-051](../architecture/adr/adr-051-mechanic-extractor-ip-policy.md) · [Issue roadmap](../roadmap/mechanic-extractor-ai-first-issues.md) · [UI mockups](./mechanic-extractor-ai-first-mockups.md)

> Questo documento traduce le tre issue fondamentali (schema, prompt+command, guardrails) in un piano concreto: file da creare, DDL idempotente, JSON schema, firme validator, test fixtures. Non include codice completo (ogni issue produce la sua PR) ma ne definisce i contratti.

---

## 1. Panoramica build order

```
┌─────────────────────────────────────────────────────────────┐
│ ME-M1.1  Schema DB + EF                        #523          │
│   ├─ MechanicAnalysis (aggregate root)                       │
│   ├─ MechanicClaim (entity, child)                           │
│   ├─ MechanicCitation (value object, owned)                  │
│   └─ migration 20260428_AddMechanicAnalyses.cs               │
├─────────────────────────────────────────────────────────────┤
│ ME-M1.2  Command GenerateMechanicAnalysis      #524          │
│   ├─ Prompt template v1 (system + user)                      │
│   ├─ JSON schema output (claims[] with citations[])          │
│   ├─ Handler: chunk retrieval → LLM → persist Draft          │
│   └─ Rollback on validator failure (see M1.3)                │
├─────────────────────────────────────────────────────────────┤
│ ME-M1.3  Guardrails T1-T4, T8                  #525          │
│   ├─ T1 QuoteLengthValidator (≤25 words)                     │
│   ├─ T2 LiteralCopyValidator (>10 consecutive words)         │
│   ├─ T3 CitationRequiredValidator                            │
│   ├─ T4 CitationPageVerifier (substring + page exists)       │
│   └─ T8 CostCapValidator (pre-flight € 2.00)                 │
└─────────────────────────────────────────────────────────────┘
```

**Parallelizzabile**: M1.1 e prompt template di M1.2 possono procedere in parallelo. Il validator di M1.3 può essere stub-bed durante M1.2 (contract-first).

---

## 2. ME-M1.1 — Schema & Persistenza

### 2.1 Entity layout (DDD)

**Bounded context**: `SharedGameCatalog` (condiviso, come `MechanicDraft`)
**Aggregate root**: `MechanicAnalysis`

```
MechanicAnalysis (aggregate root)
├─ Id, SharedGameId, PdfDocumentId, PromptVersion, Status, CreatedBy, ReviewedBy, ReviewedAt
├─ TotalTokensUsed, EstimatedCostUsd, ModelUsed, Provider
│
│  — T8 cost governance (ADR-051) —
├─ CostCapUsd (decimal, default 2.0 from config MechanicExtractor:CostCapUsd)
├─ CostCapOverrideAt, CostCapOverrideBy, CostCapOverrideReason (admin-only override trail)
│
│  — T5 takedown kill-switch + SLA tracking —
├─ IsSuppressed (bool), SuppressedAt, SuppressedBy, SuppressionReason
├─ SuppressionRequestedAt (nullable — when the takedown request arrived)
├─ SuppressionRequestSource (nullable enum: Email=1 | Legal=2 | Other=3)
│
├─ [concurrency] xmin (PostgreSQL system column — NOT mapped as C# property;
│                     configured via .UseXminAsConcurrencyToken() in EF)
└─ Claims: IReadOnlyList<MechanicClaim>
       ├─ Id, AnalysisId, Section (enum), Text, DisplayOrder, Status, ReviewedBy, ReviewedAt, RejectionNote
       ├─ [concurrency] xmin (same pattern as parent)
       └─ Citations: IReadOnlyList<MechanicCitation>  (child entity with Id, separate table — not OwnsMany
                                                       because indexed lookup by PdfPage required for side-panel)
              ├─ Id, ClaimId, DisplayOrder
              ├─ PdfPage (int, CHECK > 0)
              ├─ Quote (string, CHECK quote_word_count ≤ 25 at DB level — T1 belt-and-braces)
              └─ ChunkId (Guid?, FK to text_chunks with ON DELETE SET NULL)
```

**Audit architecture decision** (Fowler P0-3): due tabelle separate invece di una tabella polimorfica con `EventType` discriminator. Rationale:

- `mechanic_status_audit` (lifecycle: Draft→InReview→Published→Rejected)
- `mechanic_suppression_audit` (IP kill-switch: takedown evidence chain)

Ogni tabella ha schema stretto (no colonne nullable condizionali), indici propri, e consumer che sa esattamente quale storia sta leggendo. Join esplicito quando serve timeline unificata admin (query rara, overhead trascurabile). Alternative scartate: (A) tabella unica con EventType → fragile allo schema evolution, (B) event store generico con JSONB payload → perde type safety e indici partial.

**File da creare**:

| Path | Purpose |
|------|---------|
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs` | Aggregate root |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs` | Child entity |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCitation.cs` | Owned value object |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicAnalysisStatus.cs` | `Draft\|InReview\|Published\|Rejected\|Suppressed` |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicClaimStatus.cs` | `Pending\|Approved\|Rejected` |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicSection.cs` | `Summary\|Mechanics\|Victory\|Resources\|Phases\|Faq` |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/IMechanicAnalysisRepository.cs` | Repo interface |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicAnalysisRepository.cs` | Impl |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/MechanicAnalysisConfiguration.cs` | EF config |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/MechanicClaimConfiguration.cs` | EF config |
| `apps/api/src/Api/Infrastructure/Migrations/20260428XXXXXX_AddMechanicAnalyses.cs` | Migration |

### 2.2 DDL idempotente (migration body)

Pattern derivato da [PR #517](https://github.com/meepleAi-app/meepleai-monorepo/pull/517). **No `AddColumn`/`CreateTable` standard EF** — solo raw SQL con `IF NOT EXISTS` per resistere a drift staging.

#### 2.2.0 Migration template (copy-paste ready)

Il `Up()` della migrazione `AddMechanicAnalyses` deve avvolgere ogni DDL destruttivo in guardia di idempotenza. Template pronto all'uso (Hightower P1-5):

```csharp
// apps/api/src/Api/Infrastructure/Migrations/20260428XXXXXX_AddMechanicAnalyses.cs
protected override void Up(MigrationBuilder migrationBuilder)
{
    // === mechanic_analyses ===
    migrationBuilder.Sql(@"
        CREATE TABLE IF NOT EXISTS mechanic_analyses (
            /* ...colonne come §2.2.1... */
        );
    ");

    // Aggiunta colonne incrementali (se la tabella già esiste in drift)
    migrationBuilder.Sql(@"
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_name='mechanic_analyses' AND column_name='CostCapUsd') THEN
                ALTER TABLE mechanic_analyses
                    ADD COLUMN ""CostCapUsd"" numeric(10,4) NOT NULL DEFAULT 2.0;
            END IF;
        END $$;
    ");

    // Indici e CHECK constraints via guardia pg_constraint / pg_indexes
    migrationBuilder.Sql(@"
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_constraint
                           WHERE conname = 'CK_mechanic_citations_quote_words') THEN
                ALTER TABLE mechanic_citations
                    ADD CONSTRAINT ""CK_mechanic_citations_quote_words""
                    CHECK (array_length(regexp_split_to_array(trim(""Quote""), E'\\s+'), 1) <= 25);
            END IF;
        END $$;
    ");

    // ...(continuare per tutti i statement sotto §2.2.1-§2.2.4)
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // DROP IF EXISTS, simmetrico ma ordine inverso per FK
    migrationBuilder.Sql(@"DROP TABLE IF EXISTS mechanic_suppression_audit CASCADE;");
    migrationBuilder.Sql(@"DROP TABLE IF EXISTS mechanic_status_audit CASCADE;");
    migrationBuilder.Sql(@"DROP TABLE IF EXISTS mechanic_citations CASCADE;");
    migrationBuilder.Sql(@"DROP TABLE IF EXISTS mechanic_claims CASCADE;");
    migrationBuilder.Sql(@"DROP TABLE IF EXISTS mechanic_analyses CASCADE;");
}
```

**Vincolo di test idempotenza**: la migrazione deve passare 3 scenari CI:
1. DB vuoto → crea tutto
2. DB con schema completo → no-op (nessun errore)
3. DB con schema parziale (es. solo `mechanic_analyses` senza `CostCapUsd`) → aggiunge colonne mancanti

#### 2.2.1 mechanic_analyses

```sql
CREATE TABLE IF NOT EXISTS mechanic_analyses (
    "Id" uuid PRIMARY KEY,
    "SharedGameId" uuid NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
    "PdfDocumentId" uuid NOT NULL REFERENCES pdf_documents("Id") ON DELETE RESTRICT,
    "PromptVersion" varchar(32) NOT NULL,
    "Status" int NOT NULL DEFAULT 0,              -- Draft=0, InReview=1, Published=2, Rejected=3, Suppressed=4
    "CreatedBy" uuid NOT NULL REFERENCES users("Id"),
    "CreatedAt" timestamptz NOT NULL DEFAULT NOW(),
    "ReviewedBy" uuid NULL REFERENCES users("Id"),
    "ReviewedAt" timestamptz NULL,
    "TotalTokensUsed" int NOT NULL DEFAULT 0,
    "EstimatedCostUsd" numeric(10,4) NOT NULL DEFAULT 0,
    "ModelUsed" varchar(128) NOT NULL,
    "Provider" varchar(32) NOT NULL,              -- "deepseek" | "openrouter" | …

    -- T8 cost governance (ADR-051)
    "CostCapUsd" numeric(10,4) NOT NULL DEFAULT 2.0,
    "CostCapOverrideAt" timestamptz NULL,
    "CostCapOverrideBy" uuid NULL REFERENCES users("Id"),
    "CostCapOverrideReason" text NULL,

    -- T5 takedown kill-switch + SLA tracking
    "IsSuppressed" boolean NOT NULL DEFAULT false,
    "SuppressedAt" timestamptz NULL,
    "SuppressedBy" uuid NULL REFERENCES users("Id"),
    "SuppressionReason" text NULL,
    "SuppressionRequestedAt" timestamptz NULL,
    "SuppressionRequestSource" int NULL,          -- 1=Email, 2=Legal, 3=Other
    "RejectionReason" text NULL,

    -- Invariant: se override è presente, TUTTI i campi override devono esserlo
    CONSTRAINT "CK_mechanic_analyses_cost_override_complete"
        CHECK (
            ("CostCapOverrideAt" IS NULL AND "CostCapOverrideBy" IS NULL AND "CostCapOverrideReason" IS NULL)
         OR ("CostCapOverrideAt" IS NOT NULL AND "CostCapOverrideBy" IS NOT NULL AND "CostCapOverrideReason" IS NOT NULL)
        ),
    -- Invariant: se suppressed, SuppressedAt e SuppressedBy obbligatori
    CONSTRAINT "CK_mechanic_analyses_suppression_complete"
        CHECK (
            ("IsSuppressed" = false)
         OR ("IsSuppressed" = true AND "SuppressedAt" IS NOT NULL AND "SuppressedBy" IS NOT NULL)
        )
    -- NOTE: EF concurrency token is the PostgreSQL system column `xmin` (auto-present on every table).
    --       Do NOT declare `xmin` explicitly — configure via `.UseXminAsConcurrencyToken()` in EF only.
);

-- Unique: one analysis per (game, pdf, prompt_version) — T7 reproducibility
CREATE UNIQUE INDEX IF NOT EXISTS "UX_mechanic_analyses_shared_game_pdf_prompt"
    ON mechanic_analyses ("SharedGameId", "PdfDocumentId", "PromptVersion")
    WHERE "Status" <> 3;  -- exclude rejected so re-runs don't collide

-- Player-facing lookup: only published + not suppressed
CREATE INDEX IF NOT EXISTS "IX_mechanic_analyses_shared_game_published"
    ON mechanic_analyses ("SharedGameId", "Status")
    WHERE "Status" = 2 AND "IsSuppressed" = false;

-- Admin review queue
CREATE INDEX IF NOT EXISTS "IX_mechanic_analyses_status_created"
    ON mechanic_analyses ("Status", "CreatedAt" DESC);

-- Takedown SLA analytics (admin dashboard): time from request to effective suppression
CREATE INDEX IF NOT EXISTS "IX_mechanic_analyses_suppression_sla"
    ON mechanic_analyses ("SuppressionRequestedAt", "SuppressedAt")
    WHERE "IsSuppressed" = true AND "SuppressionRequestedAt" IS NOT NULL;
```

#### 2.2.2 mechanic_claims (child entity)

```sql
CREATE TABLE IF NOT EXISTS mechanic_claims (
    "Id" uuid PRIMARY KEY,
    "AnalysisId" uuid NOT NULL REFERENCES mechanic_analyses("Id") ON DELETE CASCADE,
    "Section" int NOT NULL,                      -- Summary=0, Mechanics=1, Victory=2, Resources=3, Phases=4, Faq=5
    "Text" text NOT NULL,
    "DisplayOrder" int NOT NULL DEFAULT 0,
    "Status" int NOT NULL DEFAULT 0,             -- Pending=0, Approved=1, Rejected=2
    "ReviewedBy" uuid NULL REFERENCES users("Id"),
    "ReviewedAt" timestamptz NULL,
    "RejectionNote" text NULL
    -- NOTE: `xmin` system column used as concurrency token via EF `.UseXminAsConcurrencyToken()`.
);

CREATE INDEX IF NOT EXISTS "IX_mechanic_claims_analysis_section_order"
    ON mechanic_claims ("AnalysisId", "Section", "DisplayOrder");

CREATE INDEX IF NOT EXISTS "IX_mechanic_claims_pending"
    ON mechanic_claims ("AnalysisId")
    WHERE "Status" = 0;
```

#### 2.2.3 mechanic_citations

```sql
-- Decision: separate table — allows indexed page lookup for side-panel navigation
CREATE TABLE IF NOT EXISTS mechanic_citations (
    "Id" uuid PRIMARY KEY,
    "ClaimId" uuid NOT NULL REFERENCES mechanic_claims("Id") ON DELETE CASCADE,
    "PdfPage" int NOT NULL CHECK ("PdfPage" > 0),
    "Quote" varchar(400) NOT NULL,               -- ~25 words * 16 chars upper bound
    "ChunkId" uuid NULL REFERENCES text_chunks("Id") ON DELETE SET NULL,
    "DisplayOrder" int NOT NULL DEFAULT 0,

    -- T1 belt-and-braces: DB-level enforcement of 25-word cap
    -- Tokenizza su whitespace dopo trim. Garantisce che anche un bug nel validator C#
    -- non permetta di persistere quote > 25 parole (legal evidence chain integrity).
    CONSTRAINT "CK_mechanic_citations_quote_words"
        CHECK (array_length(regexp_split_to_array(trim("Quote"), E'\\s+'), 1) <= 25)
);

CREATE INDEX IF NOT EXISTS "IX_mechanic_citations_claim_page"
    ON mechanic_citations ("ClaimId", "PdfPage");
```

#### 2.2.4 Audit tables (split architecture — Fowler P0-3)

Due tabelle separate, una per tipo di evento. Rationale in §2.1.

```sql
-- Lifecycle audit: Draft→InReview→Published→Rejected transitions
CREATE TABLE IF NOT EXISTS mechanic_status_audit (
    "Id" uuid PRIMARY KEY,
    "AnalysisId" uuid NOT NULL REFERENCES mechanic_analyses("Id") ON DELETE CASCADE,
    "FromStatus" int NOT NULL,                    -- Draft=0, InReview=1, Published=2, Rejected=3
    "ToStatus" int NOT NULL,
    "ActorId" uuid NOT NULL REFERENCES users("Id"),
    "Note" text NULL,                             -- es. rejection reason
    "OccurredAt" timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_mechanic_status_audit_transition"
        CHECK ("FromStatus" <> "ToStatus")        -- no self-loop
);

CREATE INDEX IF NOT EXISTS "IX_mechanic_status_audit_analysis_occurred"
    ON mechanic_status_audit ("AnalysisId", "OccurredAt" DESC);

-- Suppression audit: takedown evidence chain (legal/IP)
-- Separata da status_audit perché IsSuppressed è ortogonale a Status
-- (una Published può essere Suppressed senza cambiare Status).
CREATE TABLE IF NOT EXISTS mechanic_suppression_audit (
    "Id" uuid PRIMARY KEY,
    "AnalysisId" uuid NOT NULL REFERENCES mechanic_analyses("Id") ON DELETE CASCADE,
    "FromSuppressed" boolean NOT NULL,
    "ToSuppressed" boolean NOT NULL,
    "ActorId" uuid NOT NULL REFERENCES users("Id"),
    "Reason" text NOT NULL,                       -- obbligatorio (takedown reference minimum)
    "RequestSource" int NULL,                     -- 1=Email, 2=Legal, 3=Other (NULL solo per unsuppress)
    "RequestedAt" timestamptz NULL,               -- quando il takedown fu notificato
    "OccurredAt" timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_mechanic_suppression_audit_toggle"
        CHECK ("FromSuppressed" <> "ToSuppressed")
);

CREATE INDEX IF NOT EXISTS "IX_mechanic_suppression_audit_analysis_occurred"
    ON mechanic_suppression_audit ("AnalysisId", "OccurredAt" DESC);

-- SLA analytics: quante ore tra richiesta e suppression effettiva
CREATE INDEX IF NOT EXISTS "IX_mechanic_suppression_audit_sla"
    ON mechanic_suppression_audit ("RequestedAt", "OccurredAt")
    WHERE "ToSuppressed" = true AND "RequestedAt" IS NOT NULL;
```

**Idempotency wrapper**: avvolgere ogni statement che usa tipi nuovi (enum check, FK) in `DO $$ ... IF NOT EXISTS ... END $$;` come pattern PR #517 e template §2.2.0.

### 2.3 EF configurations — punti chiave

- `MechanicAnalysis`: `HasKey(x => x.Id)`; concurrency via `.UseXminAsConcurrencyToken()` — **NON** mappare `xmin` come proprietà C#, è system column auto-gestita (EF 9 read-only). `HasMany(a => a.Claims).WithOne().OnDelete(Cascade)`.
- `MechanicClaim`: FK ad `AnalysisId` + `.OnDelete(DeleteBehavior.Cascade)`, navigation collection di `Citations`; concurrency identico via `.UseXminAsConcurrencyToken()`.
- `MechanicCitation`: `HasKey(x => x.Id)`; indexed lookup su `ClaimId`. Value-object-like ma con Id per FK chunk (scelta pragmatica: separate table, non `OwnsMany` — vedi §2.1 rationale).
- **Global query filter** su `MechanicAnalysis`: `.HasQueryFilter(a => !a.IsSuppressed)` — player non vede mai contenuti sotto takedown anche se un bug dimentica il check. Admin queries che devono vedere i suppressed: `.IgnoreQueryFilters()`.

#### Audit trail — domain events pattern (Fowler P2-1)

Preferenza: **domain events emessi dall'aggregato**, dispatcher nel `MediatorSaveChangesInterceptor` che è già in codebase (coerente con pattern `AggregateRoot.AddDomainEvent()`). Rifiutata soluzione pure-interceptor che legge ChangeTracker, perché rompe l'incapsulamento dell'aggregato.

**Flusso**:

```
1. Endpoint → IMediator.Send(ApproveMechanicAnalysisCommand)
2. Handler: analysis.Approve(actorId)
   ├─ aggregate valida transizione + muta stato
   └─ aggregate invoca this.AddDomainEvent(new MechanicAnalysisStatusChanged(id, from, to, actorId))
3. Handler: repository.UpdateAsync(analysis); await unitOfWork.SaveChangesAsync()
4. MediatorSaveChangesInterceptor (già registrato):
   ├─ collect domain events da tutti gli aggregate tracked
   ├─ persist audit row PRIMA di SaveChanges (stessa transaction)
   └─ IMediator.Publish(event) DOPO SaveChanges (per side effects extra-aggregato, es. notifications)
5. Handler ritorna result
```

**Eventi richiesti** (in `Domain/Events/`):

| Evento | Trigger method | Audit table |
|--------|----------------|-------------|
| `MechanicAnalysisStatusChanged` | `SubmitForReview`, `Approve`, `Reject`, `Publish` | `mechanic_status_audit` |
| `MechanicAnalysisSuppressed` | `Suppress(actorId, reason, requestedAt, source)` | `mechanic_suppression_audit` |
| `MechanicAnalysisUnsuppressed` | `Unsuppress(actorId, reason)` | `mechanic_suppression_audit` |
| `MechanicAnalysisCostCapOverridden` | `OverrideCostCap(actorId, newCapUsd, reason)` | `mechanic_status_audit` (o dedicata in M2) |

**Invariante atomicità**: l'audit row e il state change dell'aggregato **devono** essere nella stessa transaction DB. Se la scrittura audit fallisce, l'intero SaveChanges rollbacks. Implementato via `SaveChangesInterceptor.SavingChangesAsync` (append rows al ChangeTracker prima del commit finale).

### 2.4 Deprecazione Variant C — zero-risk sunset

`mechanic_drafts` resta intatto, zero migrazione dati (0 righe). In M1.6 (issue separata):
```sql
-- Solo banner deprecation tracciato via config, nessuna DDL destruttiva fino a M6+
COMMENT ON TABLE mechanic_drafts IS 'DEPRECATED 2026-04-28: superseded by mechanic_analyses. Removal: 2026-10+';
```

---

## 3. ME-M1.2 — Command & Prompt

### 3.1 Command contract

**File da creare** in `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/`:

```
GenerateMechanicAnalysisCommand.cs          -- record (SharedGameId, PdfDocumentId, PromptVersion?, ForceRegenerate?)
GenerateMechanicAnalysisCommandValidator.cs -- FluentValidation
GenerateMechanicAnalysisCommandHandler.cs   -- pipeline: retrieval → prompt → LLM → parse → validate → persist
GenerateMechanicAnalysisResult.cs           -- { AnalysisId, ClaimCount, TokensUsed, CostUsd, Warnings[] }
```

**Dependencies** (constructor injection):
- `IMechanicAnalysisRepository`
- `ISharedGameRepository` (game title + metadata)
- `IPdfDocumentRepository` (verify access + page count)
- `ITextChunkSearchService` (riuso da KB — retrieval per section)
- `ILlmClient` via `ILlmProviderSelector` (DeepSeek primary, OpenRouter fallback)
- `IMechanicOutputValidator` (M1.3 guardrails, contract-first — può essere stub)
- `IAnalysisCostEstimator` (T8 pre-flight)
- `ILogger<GenerateMechanicAnalysisCommandHandler>`
- `TimeProvider`

### 3.2 Pipeline handler (pseudo-flow)

```
1. Resolve game + pdf, verify pdf.Status = Ready
2. Resolve effective cost cap (T8):
   effectiveCapUsd = existingAnalysis?.CostCapUsd                         // re-run path: usa il cap della analysis esistente
                  ?? config.MechanicExtractor.CostCapUsd                   // default da appsettings (seed della nuova entity)
                  ?? 2.00m                                                 // hard fallback
   pre-flight estimate:
     estimated = Σ sections (K_chunks * avgTokensPerChunk + expectedClaims * avgTokensPerClaim) * modelPricing
   decision matrix:
     if estimated ≤ effectiveCapUsd                                 → proceed
     if estimated > effectiveCapUsd AND command.CostCapOverride == null
                                                                    → return 402 Payment Required
     if estimated > effectiveCapUsd AND command.CostCapOverride != null:
         - command.CostCapOverride must provide NewCapUsd + Reason (FluentValidation §3.1)
         - operatore deve essere admin (authz layer)
         - handler invoca analysis.OverrideCostCap(actorId, newCapUsd, reason)
             → muta CostCapUsd + CostCapOverrideAt/By/Reason sull'aggregato
             → aggregato emette MechanicAnalysisCostCapOverridden (§2.3) → audit row stessa transaction
         - proceed con effectiveCapUsd = newCapUsd
3. For each section in [Summary, Mechanics, Victory, Resources, Phases, Faq]:
   a. retrieve top-K chunks (K=6) via hybrid search with section-specific query
   b. build user prompt (§3.3) with retrieved chunks + section instructions
   c. call LLM (temp=0.2, max_tokens=1500, JSON mode enforced)
   d. parse JSON output → List<ClaimDraft>
   e. run guardrails T1-T4 (M1.3) — on failure: retry once with reinforced prompt, then skip claim
   f. accumulate tokens + cost
   g. mid-generation guardrail:
        if cumulativeCost > effectiveCapUsd * 1.5 → abort loop, go to step 4 with RejectionReason
4. Persist MechanicAnalysis(Status=Draft) with all validated claims
   - snapshot di CostCapUsd dal config/override (non più hardcoded)
5. MechanicAnalysisStatusChanged(null → Draft) → audit row (via SaveChangesInterceptor)
6. Return result (includes warnings for skipped claims + effectiveCapUsd + totalCostUsd)
```

**Key decisions**:
- `CostCapUsd` è **persistito sull'aggregate**, non letto solo da config al volo: garantisce che un'analysis generata al cap €2.00 non cambi se il config sale a €5.00 in futuro (reproducibility + audit trail).
- Override è una **transizione di dominio** (`OverrideCostCap`), non un flag nel command: audit trail via domain event evita che bug nel handler dimentichi di loggare.
- Rollback mid-generation: se `cumulativeCost > effectiveCapUsd * 1.5`, persist partial con `RejectionReason = "cost cap breached at section X — estimated={est}, cap={cap}, actual={actual}"`.
- Retry logic (max 1 retry per section) solo su guardrail failure, non su LLM network error (quello è ritentato dal client).
- Temperature bassa (0.2) per deterministic output riducendo hallucination.

### 3.3 Prompt template v1

**File**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractorPrompts.cs`

#### System prompt (static)

```
You are an expert board game rulebook analyst. Your job is to extract structured
reference claims from the provided rulebook excerpts and produce a JSON output
that follows the exact schema below.

HARD RULES (violating ANY means your output will be rejected):
1. REPHRASE EVERY CLAIM — do NOT copy sentences verbatim from the excerpts.
   Use your own words while preserving the rule's meaning precisely.
2. Every claim MUST include at least one citation with:
   - pdf_page: the page number in the rulebook
   - quote: a short excerpt from the rulebook (≤ 25 words) to attribute the source
3. The quote MUST be a verbatim substring of the excerpt (for verification),
   but the claim text itself MUST be rephrased.
4. If the excerpt does not contain clear evidence for a claim, do NOT invent it —
   omit that claim entirely.
5. Output MUST be valid JSON matching the schema — no prose before or after.

Writing style:
- Concise, player-friendly (Italian unless excerpts are exclusively English).
- One rule per claim — do not stack multiple rules in a single claim.
- Avoid jargon not present in the rulebook.
```

#### User prompt (per section, templated)

```
Game: {GameTitle}
Section to extract: {SectionName}
Section description: {SectionDescription}

Rulebook excerpts (numbered, with page references):

[Excerpt 1 — page {p1}]
{chunk_1_text}

[Excerpt 2 — page {p2}]
{chunk_2_text}

... (up to 6 excerpts)

Produce claims for the "{SectionName}" section only.
Target: {MinClaims}-{MaxClaims} claims.

Return JSON with this structure:
{
  "section": "{SectionName}",
  "claims": [
    {
      "text": "Rephrased rule in your own words",
      "citations": [
        {
          "pdf_page": 12,
          "quote": "verbatim snippet ≤ 25 words from the excerpt",
          "excerpt_id": 1
        }
      ]
    }
  ]
}
```

**Section metadata** (drives `{SectionName}`, `{SectionDescription}`, `{MinClaims}`, `{MaxClaims}`):

| Section | Description | Min | Max |
|---------|-------------|-----|-----|
| Summary | Scopo del gioco, 2-4 righe narrative sul flusso di gioco | 2 | 4 |
| Mechanics | Meccaniche principali (es. drafting, worker placement) | 4 | 10 |
| Victory | Condizione di vittoria + come si calcolano i punti | 1 | 4 |
| Resources | Risorse e come si ottengono/spendono | 2 | 8 |
| Phases | Fasi/round/turno sequenziali | 3 | 12 |
| Faq | Domande frequenti o regole spesso fraintese | 0 | 6 |

### 3.4 JSON schema (OpenAI/DeepSeek `response_format: json_schema`)

```json
{
  "name": "mechanic_section_output",
  "strict": true,
  "schema": {
    "type": "object",
    "required": ["section", "claims"],
    "additionalProperties": false,
    "properties": {
      "section": {
        "type": "string",
        "enum": ["Summary", "Mechanics", "Victory", "Resources", "Phases", "Faq"]
      },
      "claims": {
        "type": "array",
        "maxItems": 12,
        "items": {
          "type": "object",
          "required": ["text", "citations"],
          "additionalProperties": false,
          "properties": {
            "text": { "type": "string", "minLength": 10, "maxLength": 600 },
            "citations": {
              "type": "array",
              "minItems": 1,
              "maxItems": 3,
              "items": {
                "type": "object",
                "required": ["pdf_page", "quote", "excerpt_id"],
                "additionalProperties": false,
                "properties": {
                  "pdf_page": { "type": "integer", "minimum": 1 },
                  "quote": { "type": "string", "minLength": 5, "maxLength": 300 },
                  "excerpt_id": { "type": "integer", "minimum": 1 }
                }
              }
            }
          }
        }
      }
    }
  }
}
```

**Schema enforcement**: provider passa via `ResponseFormat = JsonSchema(...)`. Se il provider (es. Ollama locale) non supporta strict JSON schema → fallback a `ResponseFormat.JsonObject` + validation C# side (già fatto da M1.3 T3).

### 3.5 PromptVersion — riproducibilità

- Valore costante in codice: `public const string MechanicExtractorPromptV1 = "v1.0.0";`
- Cambio del prompt o dello schema → **bump semver** (es. `v1.1.0` se cambia solo wording, `v2.0.0` se cambia schema)
- Stored in `mechanic_analyses.PromptVersion` + unique constraint (§2.2) consente re-run per stesso (game, pdf) con nuove versioni senza conflitti

---

## 4. ME-M1.3 — Guardrails T1-T4, T8

### 4.1 Interface & orchestration

**File**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Services/IMechanicOutputValidator.cs`

```csharp
public interface IMechanicOutputValidator
{
    /// <summary>
    /// Runs T1-T4 guardrails on a single claim. T8 runs separately at handler level.
    /// Returns success if all checks pass, otherwise a failure with ordered reasons.
    /// </summary>
    MechanicValidationResult Validate(
        ClaimDraft draft,
        IReadOnlyList<RetrievedChunk> sourceChunks);
}

public sealed record MechanicValidationResult(
    bool IsValid,
    IReadOnlyList<MechanicValidationFailure> Failures);

public sealed record MechanicValidationFailure(
    string Rule,              // "T1", "T2", "T3", "T4"
    string CitationRef,       // "citation[0]" or "claim.text"
    string Message);
```

### 4.2 Individual validators (composition)

**T1 — QuoteLength ≤ 25 words**
```
File: QuoteLengthValidator.cs
Pure: input citation.quote → count words (split by whitespace after normalize) → ≤ 25
Edge: hyphenated words "worker-placement" count as 1 (split by regex \s+, not \W+)
Fail: FailureRule="T1", Message="Quote length N words exceeds cap 25"
```

**T2 — Literal copy > 10 consecutive words from source**
```
File: LiteralCopyValidator.cs
Algorithm:
  1. Normalize claim.text (lowercase, strip punctuation except intra-word)
  2. Normalize all source chunks similarly
  3. Sliding window on claim.text tokens (n=10):
     for each 10-gram, check if it appears as substring in any source chunk
  4. First match → fail
Complexity: O(claim_tokens * source_total_chars) — acceptable for claims ≤ 600 chars
Fail: FailureRule="T2", Message="Detected literal copy from source: '<offending 10-gram>'"
```
⚠️ Known false-positive: nomi propri di gioco (es. "il Mazzo Terreni del Nord") possono essere identici. Mitigazione v1: whitelist di frasi ≤ 3 parole (nomi dal `shared_games.Name` + expansions). v2 potenziale: n-gram overlap statistico invece di exact match.

**T3 — Citation obbligatoria**
```
File: CitationRequiredValidator.cs
Pure: citations.Count ≥ 1 (already enforced by JSON schema minItems:1, but double-check post-parse)
Fail: FailureRule="T3", Message="Claim has no citations"
```

**T4 — Citation page + quote verifiable**
```
File: CitationPageVerifier.cs
Deps: IPdfDocumentRepository (page count lookup)
Algorithm for each citation:
  1. Verify pdf_page ∈ [1, pdf.PageCount]
  2. Find source chunk matching citation.excerpt_id (from retrieval context)
  3. Verify citation.quote is a substring of that chunk's text (case-insensitive, whitespace-tolerant)
Fail modes:
  T4-a: "Page N does not exist (pdf has M pages)"
  T4-b: "Quote not found in source excerpt ID E"
  T4-c: "Excerpt ID E not in retrieval set"
```

**T8 — Cost cap pre-flight (handler-level, not per-claim)**
```
File: AnalysisCostEstimator.cs
Input:
  - sections.Count, avgChunkTokens, modelPricing (da IModelPricingCatalog)
  - effectiveCapUsd (risolto dal handler, non hardcoded qui — vedi §3.2 step 2)
Estimate:
  inputTokens = sections * K_chunks * avgTokensPerChunk + sections * systemPromptTokens
  outputTokens = sections * expectedClaimsPerSection * avgTokensPerClaim
  estimatedUsd = (inputTokens * inputPricePerM + outputTokens * outputPricePerM) / 1_000_000
Decision:
  EstimateResult {
      EstimatedUsd,
      EffectiveCapUsd,
      WithinCap : estimatedUsd ≤ effectiveCapUsd,
      RequiresOverride : !WithinCap && !commandHasOverride
  }
Cap resolution order (handler-owned, estimator is pure):
  1. re-run path       → existingAnalysis.CostCapUsd (persisted snapshot, reproducibility)
  2. new analysis path → config MechanicExtractor:CostCapUsd (default 2.00 USD)
  3. admin override    → analysis.OverrideCostCap(actorId, newCap, reason)
                          → muta CostCapUsd + emette MechanicAnalysisCostCapOverridden
                          → audit row (stessa transaction di SaveChanges)
Fail-fast: command returns 402 Payment Required before any LLM call if
           RequiresOverride == true && command.CostCapOverride == null
```

### 4.3 Retry policy — one-shot with reinforcement

Quando `validator.Validate(...)` fallisce **solo per T1 o T2**:
1. Build reinforcement suffix:
   - T1 fail: `"Your previous quote was N words (limit: 25). Regenerate with shorter quotes."`
   - T2 fail: `"Your previous claim contained verbatim copy: '<10-gram>'. Rephrase completely."`
2. Single retry with appended instruction
3. Second failure → skip claim, log warning, continue with rest

T3/T4 failures = non retry-able (structural) → skip claim immediately.

### 4.4 Where it plugs into pipeline

```
Handler.GenerateMechanicAnalysisAsync:
    …
    var response = await llmClient.GenerateCompletionAsync(…);
    var parsed = JsonSerializer.Deserialize<SectionOutput>(response.Text);

    foreach (var claimDraft in parsed.Claims)
    {
        var result = validator.Validate(claimDraft, retrievedChunks);
        if (!result.IsValid && result.Failures.Any(f => f.Rule is "T1" or "T2"))
        {
            // retry once
            var retried = await llmClient.GenerateCompletionAsync(reinforcedPrompt);
            claimDraft = TryParseClaim(retried);
            result = validator.Validate(claimDraft, retrievedChunks);
        }

        if (!result.IsValid)
        {
            warnings.Add($"Section {section}: skipped claim — {string.Join("; ", result.Failures)}");
            continue;
        }

        validatedClaims.Add(claimDraft);
    }
    …
```

---

## 5. Test fixtures & strategy

### 5.1 Fixture dataset — Catan subset

**Location**: `apps/api/tests/Api.Tests/Fixtures/MechanicExtractor/catan-subset/`

| File | Content |
|------|---------|
| `catan-excerpt-setup.txt` | Pagina 4 — setup board |
| `catan-excerpt-victory.txt` | Pagina 7 — 10 punti vittoria |
| `catan-excerpt-resources.txt` | Pagina 5 — 5 risorse (wood, brick, sheep, wheat, ore) |
| `catan-expected-claims.json` | Golden output (6 claims) per regression |

### 5.2 Test layering

**Unit tests** (`apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/`):
- `QuoteLengthValidatorTests` — boundary: 24/25/26 parole, unicode normalization
- `LiteralCopyValidatorTests` — exact match, case variation, whitespace tolerance, false-positive whitelist (game names)
- `CitationPageVerifierTests` — page out of range, quote substring match, missing excerpt_id
- `AnalysisCostEstimatorTests` — cost calculation, override path, model pricing lookup
- `MechanicAnalysisTests` (aggregate) — state transitions Draft→InReview→Published, cannot mutate after Published, suppression
- `GenerateMechanicAnalysisCommandValidatorTests` — FluentValidation rules

**Integration tests** (with Testcontainers Postgres):
- `MechanicAnalysisRepositoryTests` — persist + reload with Claims + Citations, concurrency (xmin), query filter (IsSuppressed)
- `GenerateMechanicAnalysisCommandHandlerTests` — in-memory LLM fake that returns golden JSON → assert persisted structure matches expected

**E2E / feature tests** (stubbed LLM):
- `GenerateMechanicAnalysis_CatanFixture_ProducesValidAnalysis` — retrieval stub + LLM fake returning pre-canned response → end-to-end DB state
- `GenerateMechanicAnalysis_CostCapExceeded_ReturnsPaymentRequired`
- `GenerateMechanicAnalysis_LiteralCopy_RetriesOnce_ThenSkips`

**No real LLM calls in CI.** Mage Knight fixture è manual-only (richiede PDF + chiave API + budget).

### 5.3 Coverage gates (da CLAUDE.md: target 90%)

- Validators: 95%+ (pure logic, no excuses)
- Handler: 85%+ (pipeline branches + retry + cost cap)
- Repository: 80%+ (EF-heavy, diminishing returns above)

---

## 6. Routing & endpoint (fuori scope M1 ma contratto)

L'endpoint verrà aggiunto in **M1.4** (#526 — Admin Generation Form backend). Pattern CQRS:

```csharp
// apps/api/src/Api/Routing/AdminMechanicAnalysisEndpoints.cs (nuovo file)
app.MapPost("/api/v1/admin/mechanic-analyses", async (
    GenerateMechanicAnalysisCommand command,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(command, ct);
    return Results.Created($"/api/v1/admin/mechanic-analyses/{result.AnalysisId}", result);
})
.RequireAuthorization("AdminOnly")
.WithTags("Admin — Mechanic Extractor")
.Produces<GenerateMechanicAnalysisResult>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status402PaymentRequired)
.Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);
```

---

## 7. Rischi & mitigazioni

| Rischio | Probabilità | Impatto | Mitigazione |
|---------|-------------|---------|-------------|
| T2 false-positive blocca troppi claim | Alta | Medio | Whitelist nomi propri + metrica "claims skipped" in telemetria; tuning threshold 10→12 parole se noise |
| DeepSeek non supporta strict JSON schema | Media | Basso | Fallback ResponseFormat.JsonObject + schema validation C# — già previsto |
| Pre-flight cost estimate sottovaluta | Bassa | Alto | Hard cutoff mid-generation se cumulative > 1.5x estimate; `CostCapUsd` è persistito sull'aggregato (re-run usa il cap originale) |
| Concurrency token `xmin` mis-configurato (shadow column) | Bassa | Alto | DDL **non** dichiara `xmin` (system column); EF usa `.UseXminAsConcurrencyToken()` + AC-4 integration test copre collision |
| `xmin` + logical replication / pglogical (P2-3) | Bassa | Medio | `xmin` è per-database: se in futuro si adotta logical replication verso una read-replica esterna, il valore può differire → concurrency check vale solo sulla primary. Mitigation: read-from-primary per admin review queue; in M2 valutare `uuid version` column custom se entra pglogical |
| Idempotent migration su staging drift | Media | Alto | Pattern PR #517 (raw SQL IF NOT EXISTS) + template §2.2.0; CI deve eseguire i 3 scenari (vuoto / completo / parziale) |
| Audit architecture — join cost su timeline unificata | Bassa | Basso | Scelta two-table (§2.1, §2.2.4): query admin "timeline completa di un'analysis" richiede UNION ALL fra `mechanic_status_audit` e `mechanic_suppression_audit` — query rara e indicizzata per `AnalysisId` in entrambe le tabelle |
| Claims multilingua (regolamento IT vs EN) | Media | Basso | Language detection su chunks → passato al prompt come hint; fallback EN |

---

## 8. Checklist DoD per milestone M1.1–M1.3

### M1.1 (#523) schema

**Deliverables**
- [ ] Aggregate root `MechanicAnalysis` + child entity `MechanicClaim` + child entity `MechanicCitation` + child entities `MechanicStatusAudit`/`MechanicSuppressionAudit` + enums
- [ ] Domain methods: `Create`, `SubmitForReview`, `Approve`, `Reject`, `Publish`, `Suppress`, `Unsuppress`, `OverrideCostCap` (+ invalid-transition exceptions)
- [ ] Domain events (§2.3): `MechanicAnalysisStatusChanged`, `MechanicAnalysisSuppressed`, `MechanicAnalysisUnsuppressed`, `MechanicAnalysisCostCapOverridden`
- [ ] EF configurations con OnDelete + query filter `IsSuppressed` + `.UseXminAsConcurrencyToken()` + cost-override + suppression CHECK constraints
- [ ] `SaveChangesInterceptor` (o estensione del `MediatorSaveChangesInterceptor` esistente) che proietta ogni evento in una audit row della tabella corretta, **nella stessa transaction**
- [ ] Migration idempotente (pattern PR #517 + template §2.2.0); 3 scenari CI: DB vuoto, DB completo, DB con schema parziale
- [ ] Repository interface + impl (`AsSplitQuery` per Claims+Citations)
- [ ] Unit test aggregate (Create, state transitions, invariants, double-reject, publish-without-claims)
- [ ] Integration test repository con Testcontainers Postgres (persist/reload/concurrency + audit + CHECK constraints + partial unique index)
- [ ] `dotnet ef database update` verde su DB pulito + DB con schema parziale (drift test)
- [ ] PR review approved + merge in `main-dev`

**Acceptance Criteria (Gherkin, testabili)**

AC-1..AC-8 coprono happy path e invarianti base; AC-9..AC-15 coprono edge cases e guardrails aggiunti dal review Wiegers/Adzic/Nygard.

```gherkin
AC-1 Creation with valid inputs:
  Given uno SharedGame esistente e un PdfDocument Status=Ready per quel gioco
  When chiamo MechanicAnalysis.Create(sharedGameId, pdfId, promptVersion="v1", createdBy=adminId)
  Then l'aggregate viene creato con Status=Draft, IsSuppressed=false, Claims vuoto
   And CostCapUsd viene inizializzato dal config MechanicExtractor:CostCapUsd (default 2.00)
   And il concurrency token `xmin` è popolato dal DB dopo il primo SaveChanges

AC-2 State machine — transizioni valide + audit (two-table):
  Given un MechanicAnalysis con Status=Draft
  When chiamo SubmitForReview(actorId)
  Then Status diventa InReview
   And l'aggregato emette MechanicAnalysisStatusChanged(from=Draft, to=InReview, actorId)
   And una riga è creata in mechanic_status_audit con FromStatus=0, ToStatus=1, ActorId=actorId, OccurredAt=NOW()
   And NESSUNA riga viene creata in mechanic_suppression_audit

AC-3 State machine — transizione invalida:
  Given un MechanicAnalysis con Status=Published
  When chiamo SubmitForReview(actorId)
  Then viene lanciata InvalidStateTransitionException
   And nessuna riga viene creata in mechanic_status_audit né mechanic_suppression_audit

AC-4 Optimistic concurrency (xmin):
  Given due istanze simultanee A e B dello stesso MechanicAnalysis (Status=InReview)
        caricate da due handler in transazioni separate
  When A chiama Approve(actorId_A) e fa SaveChanges con successo
   And B chiama Reject(actorId_B) e fa SaveChanges
  Then il SaveChanges di B fallisce con DbUpdateConcurrencyException
   And la riga DB ha Status=Approved (dal commit di A, NON Rejected di B)
   And mechanic_status_audit contiene UNA sola riga (Draft→InReview→Approved path finale),
       mai la transizione InReview→Rejected che è stata rigettata

AC-5 Uniqueness constraint (T7 reproducibility) — esempio concreto:
  Given SharedGameId="6b76bdb3-424e-44bc-b02f-cb939ee9538b" (Catan, da ADR-051)
    And PdfDocumentId="<catan-pdf-id>" Status=Ready
    And un MechanicAnalysis (Catan, catan-pdf, promptVersion="v1.0.0", Status=Published)
  When provo a inserire un secondo MechanicAnalysis con gli stessi (Catan, catan-pdf, "v1.0.0") e Status=Draft
  Then il DB solleva unique violation su UX_mechanic_analyses_shared_game_pdf_prompt

  Given invece il primo record ha Status=Rejected
  When inserisco il secondo con stessi (Catan, catan-pdf, "v1.0.0")
  Then l'insert passa (il partial index WHERE Status <> 3 esclude i rejected)

  Given un terzo caso: stesso Catan+catan-pdf, ma promptVersion="v1.1.0"
  When inserisco mentre esiste già (Catan, catan-pdf, "v1.0.0", Published)
  Then l'insert passa (PromptVersion è parte della uniqueness key)

AC-6 Kill-switch query filter:
  Given un MechanicAnalysis con Status=Published, IsSuppressed=true
  When un player query via DbContext normale (no IgnoreQueryFilters)
  Then il record NON viene restituito
  When un admin query con .IgnoreQueryFilters()
  Then il record viene restituito

AC-7 Suppression audit (takedown evidence chain, two-table):
  Given un MechanicAnalysis con IsSuppressed=false, Status=Published
        e SharedGameId="8a9f8a90-594a-4316-a32e-daa43a4356fb" (Mage Knight, da ADR-051)
  When chiamo Suppress(actorId, reason="takedown email from WizKids 2026-05-12",
                       requestedAt="2026-05-10", source=SuppressionSource.Email)
  Then IsSuppressed=true, SuppressedAt e SuppressedBy popolati
   And SuppressionRequestedAt=2026-05-10, SuppressionRequestSource=1 (Email)
   And Status RIMANE Published (suppression è ortogonale a status)
   And l'aggregato emette MechanicAnalysisSuppressed
   And una riga è creata in mechanic_suppression_audit con
       FromSuppressed=false, ToSuppressed=true, Reason="takedown email from WizKids 2026-05-12",
       RequestSource=1, RequestedAt=2026-05-10, OccurredAt=NOW()
   And NESSUNA riga è creata in mechanic_status_audit
   And la riga è recuperabile via IX_mechanic_suppression_audit_sla
       (per SLA dashboard: OccurredAt - RequestedAt = latenza takedown)

AC-8 Cascade integrity:
  Given un MechanicAnalysis con 3 MechanicClaim, ognuno con 2 MechanicCitation,
        5 righe in mechanic_status_audit e 2 in mechanic_suppression_audit
  When l'aggregate viene cancellato (admin hard delete — non takedown)
  Then tutti i claims e le citations vengono cancellati via FK cascade
   And le righe di mechanic_status_audit e mechanic_suppression_audit associate
       vengono cancellate via CASCADE on AnalysisId
   And i text_chunks referenziati da Citations.ChunkId NON vengono toccati
       (FK ON DELETE SET NULL su Citation.ChunkId)

AC-9 Citation quote word cap — DB-level enforcement (T1 belt-and-braces):
  Given un tentativo di persistere una MechanicCitation con Quote contenente 26 parole
  When il SaveChanges viene inviato al DB (anche bypassando il validator C#)
  Then il DB solleva CheckConstraintViolation su CK_mechanic_citations_quote_words
   And la transazione rollback

  Given una Quote con 25 parole (boundary)
  When SaveChanges
  Then il persist ha successo

  Given una Quote con hyphenated compound "worker-placement" + 24 altre parole
  When regexp_split_to_array(trim(Quote), E'\\s+') viene valutato
  Then array_length = 25 (hyphen NON splitta, consistente col validator C#)

AC-10 Publish senza claims solleva eccezione di dominio:
  Given un MechanicAnalysis con Status=InReview e Claims vuoto
  When chiamo Publish(actorId)
  Then viene lanciata InvalidStateTransitionException con messaggio
       "Cannot publish analysis without approved claims"
   And nessuna riga è creata in mechanic_status_audit

  Given un MechanicAnalysis con Status=InReview e 3 Claims, tutti Status=Pending
  When chiamo Publish(actorId)
  Then viene lanciata InvalidStateTransitionException con messaggio
       "All claims must be Approved before publishing (2 pending)"

AC-11 Cost cap override — domain + audit trail:
  Given un MechanicAnalysis con CostCapUsd=2.00
  When chiamo OverrideCostCap(actorId, newCapUsd=5.00, reason="Mage Knight 100pg richiede budget extra")
  Then CostCapUsd=5.00
   And CostCapOverrideAt, CostCapOverrideBy, CostCapOverrideReason popolati
   And l'aggregato emette MechanicAnalysisCostCapOverridden
   And una riga di audit viene creata (§2.3 decide destination table: status_audit o dedicata)
   And il CHECK CK_mechanic_analyses_cost_override_complete passa (tutti i campi override popolati)

  Given il tentativo di popolare CostCapOverrideAt ma lasciare CostCapOverrideReason NULL
  When SaveChanges
  Then il DB solleva CheckConstraintViolation su CK_mechanic_analyses_cost_override_complete

AC-12 Double-reject idempotency:
  Given un MechanicAnalysis con Status=InReview
  When chiamo Reject(actorId, reason="hallucinations detected")
  Then Status=Rejected, audit row creata

  When chiamo Reject(actorId, reason="seconda rejection")
  Then viene lanciata InvalidStateTransitionException (no self-loop)
   And nessuna seconda audit row viene creata
   And l'aggregato mantiene la prima reason

AC-13 Suppress su Draft (policy decision — consentito):
  Given un MechanicAnalysis con Status=Draft, IsSuppressed=false
  When chiamo Suppress(actorId, reason="pre-emptive takedown during review", ...)
  Then IsSuppressed=true (suppression è ortogonale a Status — can suppress at any stage)
   And Status RIMANE Draft
   And mechanic_suppression_audit contiene la riga
   And il query filter nasconde il record anche ai player che non l'avrebbero visto (era Draft)
       — coerente con principio "quando in dubbio, nascondi"

AC-14 Query filter bypass documentato (regression guard):
  Given 100 MechanicAnalysis in DB, di cui 10 con IsSuppressed=true
  When un repository method usa ctx.MechanicAnalyses.ToListAsync() (no IgnoreQueryFilters)
  Then la lista restituita ha esattamente 90 elementi
  When un admin method usa ctx.MechanicAnalyses.IgnoreQueryFilters().ToListAsync()
  Then la lista restituita ha esattamente 100 elementi
   And un test di regressione verifica che TUTTI i metodi del repository
       che tornano risultati player-facing NON usano IgnoreQueryFilters

AC-15 Performance budget — split query on deep aggregate (Mage Knight scale):
  Given un MechanicAnalysis con 50 Claims, ogni Claim con 3 Citations (150 citations totali)
  When il repository carica analysis + claims + citations via AsSplitQuery
  Then la query completa < 250ms su DB staging (p95)
   And il numero di roundtrip SQL è 3 (non N+1)
   And la memoria allocata è < 2MB per l'aggregate hydrato

  NOTE: threshold indicativo — regola in base al profiling del primo run Mage Knight.
        Obiettivo è prevenire N+1 regression, non garantire latenza hardcoded.
```

### M1.2 (#524) command + prompt
- [ ] Prompt v1 file committato con versione costante
- [ ] JSON schema file committato
- [ ] Command + Validator + Handler + Result
- [ ] Integration test con LLM fake (golden fixture)
- [ ] Telemetria: tokens + cost + latency per sezione
- [ ] DI registration in `SharedGameCatalogServiceExtensions`
- [ ] PR review + merge

### M1.3 (#525) guardrails
- [ ] 4 validator + estimator impl + interface
- [ ] Retry logic nel handler (plug da M1.2)
- [ ] Unit test per ogni guardrail con boundary cases
- [ ] Whitelist game names per T2
- [ ] Documentazione runbook: come regolare threshold T2 in produzione
- [ ] PR review + merge
- [ ] **Gate M1** pronto: generate-and-review pipeline end-to-end su Catan

---

## 9. Glossario cross-reference

| Simbolo | Significato |
|---------|-------------|
| T1–T8 | Technical constraints da ADR-051 §Policy |
| Prompt v1.0.0 | Initial template, semver-tracked in `mechanic_analyses.PromptVersion` |
| Section | One of {Summary, Mechanics, Victory, Resources, Phases, Faq} |
| Claim | Una singola affermazione estratta, con ≥1 citation |
| Citation | Coppia (pdf_page, quote ≤ 25 parole) |
| Analysis | Aggregato di claims per (SharedGame × PdfDocument × PromptVersion) |

---

## Changelog

- **2026-04-23** — Revisione post `/sc:spec-panel` (Wiegers, Fowler, Nygard, Adzic, Crispin, Hightower, Doumont).
  - §2.1 Entity: aggiunte `CostCapUsd`, `CostCapOverride*`, `SuppressionRequestedAt`, `SuppressionRequestSource`; nota Quote CHECK; decisione architetturale audit split (Fowler P0-3).
  - §2.2 DDL: §2.2.0 migration template idempotente (Hightower P1-5); CHECK constraints su override completeness, suppression completeness, quote word count; due tabelle audit separate (`mechanic_status_audit` + `mechanic_suppression_audit`) con partial index SLA.
  - §2.3 EF: pattern ibrido domain events + SaveChangesInterceptor (Fowler P2-1); 4 eventi richiesti + mapping a audit table.
  - §3.2 Pipeline: cost cap risolto come snapshot persistito sull'aggregato (reproducibility) + override come transizione di dominio; cutoff mid-generation 1.5× cap.
  - §4.2 T8: estimator puro, cap resolution orchestrata dal handler (estimator non legge config).
  - §7 Rischi: riga xmin + logical replication (P2-3) e nota costo join timeline audit two-table.
  - §8 DoD: deliverables estesi con domain events + interceptor + 3 scenari CI migration; AC-2/4/5/7 rinforzati con esempi concreti (Catan `6b76bdb3...`, Mage Knight `8a9f8a90...` da ADR-051); aggiunti AC-9 (quote CHECK DB-level), AC-10 (publish senza claims), AC-11 (cost cap override + CHECK), AC-12 (double-reject idempotency), AC-13 (suppress su Draft), AC-14 (query filter regression), AC-15 (performance budget split query).

---

**End of Artifact 4.** Next actionable: implementare #523 (M1.1 schema) su branch `feature/issue-523-mechanic-schema`.
