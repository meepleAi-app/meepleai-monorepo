# SP5 F3-FU-2 — KB Used-by tab (Design)

- **Date**: 2026-05-29
- **Issue**: [#1651](https://github.com/meepleAi-app/meepleai-monorepo/issues/1651) (P3, `enhancement`, `area/backend`, `area/frontend`)
- **Branch**: `feature/issue-1651-used-by-tab` (parent: `main-dev`)
- **Wave**: SP5 F3-FU (gemello di #1650 — Ingestion log tab, già mergiato PR #1668)
- **Sibling follow-ups**: #1650 (Ingestion log ✅), #1653 (Azioni avanzate), #1654 (Preview PDF), #1655 (Badge count sub-nav)
- **Status**: Draft — reviewed via `/sc:spec-panel` (Wiegers, Fowler, Nygard, Crispin), pending user approval per implementazione

## Contesto

La PR [#1649](https://github.com/meepleAi-app/meepleai-monorepo/pull/1649) ha introdotto `KbDocDetailPanel` come pannello destro dell'Explorer KB. La PR [#1668](https://github.com/meepleAi-app/meepleai-monorepo/pull/1668) (#1650) ha aggiunto il pattern tabbed interno URL-driven (`KbDocDetailTabs`, tab `?tab=ingestion`).

Questa issue aggiunge un **secondo tab interno** — `?tab=used-by` — che, dato il documento selezionato, elenca **gli agent AI che lo consumano**. È read-only.

### Decisioni di prodotto (AskUserQuestion 2026-05-29)

| # | Domanda | Scelta | Implicazione |
|---|---|---|---|
| D1 | Cosa conta come "consumo" | **Solo `KbCardIds` esplicito** | L'agent ha il `docId` nella sua lista KB. È il link autoritativo: il retrieval RAG a query-time usa esattamente `KbCardIds`. Zero falsi positivi. NON si includono agent game-scoped senza il doc. |
| D2 | Azioni per riga | **Read-only + link** | Nessuna scrittura. Detach documento differito (eventuale follow-up). |
| D3 | Agent di sistema | **Mostra tutti, con badge "sistema"** | Include `IsSystemDefined` con badge distintivo (arbitro/game-master/chat). |

## Decisioni tecniche (post spec-panel review)

Il panel di esperti ha sollevato 6 finding sulla bozza iniziale (che riusava in-memory scan + `AgentDto`). Le decisioni qui sotto incorporano i fix.

| # | Finding (esperto) | Decisione |
|---|---|---|
| T1 | Scan in-memory di *tutti* gli agent non limitato — gli auto-agent private-game (`AutoCreateAgentOnPdfReadyHandler`) fanno crescere il totale con utenti×giochi (Nygard) | **Query JSONB containment** `kb_card_ids @> '["{docId}"]'::jsonb` + **indice GIN** su `kb_card_ids`. Il filtro è in Postgres; il result set scala col numero di match, non col totale agent. |
| T2 | `AgentDto` non espone `IsSystemDefined`/`TypologySlug` ed è condiviso con la chat user-facing (Fowler) | **DTO dedicato** `KbDocConsumingAgentDto`. Coerente con la regola "no shared models between queries" e col precedente `IngestionLogDto` (#1650). |
| T3 | Criteri di accettazione non misurabili (Wiegers) | 8 AC testabili espliciti (sotto). |
| T4 | Containment JSONB non traducibile su EF InMemory (Crispin) | Integration test con **Testcontainers Postgres** per-AC (no unit-mock per il filtro). |
| T5 | Edge case: gioco rimosso, docId duplicato in KbCardIds, `Guid.Empty` (Crispin) | Tabella edge case + empty state dedicato. |
| T6 | Target link e `GameName` null non specificati (Wiegers) | Link → `/admin/agents/definitions/{agentId}`; `GameName` null → label "KB globale". |

## Architettura

```
GET /api/v1/admin/kb/docs/{docId:guid}/agents
       │  (RequireAdminSessionFilter ereditato dal group /admin/kb)
       ▼
GetConsumingAgentsByDocumentIdQuery(docId) → IMediator
       │
       ▼
GetConsumingAgentsByDocumentIdQueryHandler
   1. _agentRepository.GetByConsumedDocumentAsync(docId, ct)
        → EF: WHERE kb_card_ids @> '["{docId}"]'::jsonb  (HasQueryFilter esclude is_deleted)
        → indice GIN ix_agent_definitions_kb_card_ids
   2. Bulk-resolve GameName via ISharedGameRepository.GetNamesByIdsAsync(gameIds)  (no N+1, pattern #660)
   3. Map → KbDocConsumingAgentDto[]
       │
       ▼
IReadOnlyList<KbDocConsumingAgentDto>
       │
       ▼ (frontend)
useKbDocConsumingAgents(docId)  ◄── React Query
   - queryKey: ['kb', 'doc', docId, 'consuming-agents']
   - refetchInterval: false   (associazione statica — niente polling, a differenza di ingestion-log)
   - enabled: docId !== null && tab === 'used-by'
       │
       ▼
KbDocDetailPanel  (legge ?tab via useSearchParams)
   ├─ <KbDocDetailTabs activeTab/>  (MODIFY: aggiunge tab "Used by")
   ├─ Tab "Overview"  (esistente)
   ├─ Tab "Ingestion log"  (#1650)
   └─ Tab "Used by"  (NEW)
        └─ UsedByPanel
             ├─ UsedByAgentRow[]   (nome, type chip, badge "sistema", status, gioco, usage, link)
             └─ UsedByEmptyState   ("Nessun agent consuma questo documento")
```

### Perché JSONB containment e non in-memory

`AgentDefinition.KbCardIds` è una proprietà calcolata (`builder.Ignore`) backed dalla shadow `_kbCardIdsJson` (colonna `kb_card_ids` di tipo `jsonb`, default `"[]"`). Due strade:

- ❌ **In-memory** (`GetAllAsync` + `.Where(a => a.KbCardIds.Contains(docId))`): coerente col pattern di `GetAllAgentsQueryHandler`, MA carica *ogni* agent del sistema e deserializza il JSON di ciascuno per restituirne pochi. Gli auto-agent private-game rendono il totale non limitato → non scala.
- ✅ **JSONB `@>`** (scelta): il containment è valutato da Postgres. Con Npgsql si esprime via `EF.Functions.JsonContains(EF.Property<string>(a, "_kbCardIdsJson"), $"[\"{docId}\"]")` oppure, se la traduzione LINQ risulta scomoda, via `FromSqlInterpolated` sulla entity (tutte le colonne mappate). Indice GIN `jsonb_path_ops` per performance.

> **Nota**: il `HasQueryFilter(a => !a.IsDeleted)` resta attivo → gli agent soft-deleted sono esclusi automaticamente, anche via `FromSql`.

## Componenti

### Backend (nuovi sotto `BoundedContexts/KnowledgeBase/`)

- `Application/Queries/GetConsumingAgentsByDocumentIdQuery.cs` — record `(Guid DocumentId) : IRequest<IReadOnlyList<KbDocConsumingAgentDto>>`
- `Application/Queries/GetConsumingAgentsByDocumentIdQueryHandler.cs` — internal sealed; usa `IAgentDefinitionRepository` + `ISharedGameRepository`
- `Application/DTOs/KbDocConsumingAgentDto.cs` — DTO dedicato (sotto)
- `Domain/Repositories/IAgentDefinitionRepository.cs` — **MODIFY**: aggiungi `Task<IReadOnlyList<AgentDefinition>> GetByConsumedDocumentAsync(Guid documentId, CancellationToken ct = default)`
- `Infrastructure/Persistence/AgentDefinitionRepository.cs` — **MODIFY**: implementa containment JSONB
- `Infrastructure/Persistence/Migrations/` — **NEW migration**: indice GIN su `kb_card_ids`
- `Routing/AdminKnowledgeBaseEndpoints.cs` — **MODIFY**: map `GET /docs/{docId:guid}/agents` (dentro `kbGroup`, dopo `ingestion-log`)

DTO dedicato:

```csharp
internal record KbDocConsumingAgentDto(
    Guid Id,
    string Name,
    string Type,
    bool IsActive,
    string Status,              // Draft | Testing | Published
    bool IsSystemDefined,
    string? TypologySlug,       // "arbitro" | "game-master" | "chat" | null
    Guid? GameId,
    string? GameName,           // null → FE mostra "KB globale"
    int InvocationCount,
    DateTime? LastInvokedAt
);
```

> **DI**: nessuna nuova registrazione — MediatR auto-discovery del handler; entrambi i repository sono già registrati (usati da `GetAllAgentsQueryHandler`).

### Frontend (nuovi sotto `apps/web/src/`)

```
components/admin/knowledge-base/explorer/
├── KbDocDetailPanel.tsx          (MODIFY: branch render tab 'used-by')
├── KbDocDetailTabs.tsx           (MODIFY: KbDocTabKey += 'used-by'; TABS += {key:'used-by', label:'Used by'})
└── used-by/
    ├── UsedByPanel.tsx           (NEW: container — orchestra query + stati loading/empty/error/ready)
    ├── UsedByAgentRow.tsx        (NEW: riga agent — nome, type chip, badge sistema, status, gioco, usage, link)
    ├── UsedByEmptyState.tsx      (NEW: empty state)
    └── __tests__/
        ├── UsedByPanel.test.tsx
        └── UsedByAgentRow.test.tsx

hooks/queries/
└── useKbDocConsumingAgents.ts    (NEW: React Query, refetchInterval=false)

lib/api/
└── admin-kb.ts                   (MODIFY: aggiungi fetchConsumingAgents(docId))
```

Path discipline: tutto sotto `components/admin/knowledge-base/explorer/`, allineato a `ingestion/` (#1650).

## Criteri di accettazione (testabili)

| AC | Dato | Quando | Allora |
|---|---|---|---|
| AC1 | Agent A con `docId` ∈ KbCardIds | GET `/docs/{docId}/agents` | A è nella lista |
| AC2 | Agent B senza `docId` ∈ KbCardIds | idem | B **non** è nella lista |
| AC3 | Agent C soft-deleted con `docId` ∈ KbCardIds | idem | C **non** è nella lista (query filter) |
| AC4 | System agent S con `docId` ∈ KbCardIds | idem | S presente, `IsSystemDefined = true`, `TypologySlug` valorizzato |
| AC5 | `docId` consumato da 0 agent | idem | 200 + lista vuota → FE empty state |
| AC6 | Agent con `GameId` valorizzato | idem | `GameName` risolto; se gioco rimosso/null → `GameName = null` → FE "KB globale" |
| AC7 | `docId = Guid.Empty` | idem | 200 + lista vuota (difensivo) |
| AC8 | Utente non-admin | idem | 401/403 (`RequireAdminSessionFilter`) |

## Edge case

| Scenario | Behavior |
|---|---|
| `docId` null (FE) | Tab non monta la query; placeholder "Seleziona un documento" |
| Doc consumato da system + custom agent | Entrambi mostrati; system con badge |
| `docId` duplicato in KbCardIds di un agent | L'agent compare **una sola volta** (containment `@>` è set-membership) |
| Agent con `GameId` → SharedGame rimosso | `GameName` null → label "KB globale" (no crash) |
| docId malformato (non-guid) | Route constraint `{docId:guid}` → 404 routing |
| Backend 5xx | React Query retry 2× → error state con "Riprova" |
| Tab Overview/Ingestion attiva | `useKbDocConsumingAgents` non parte (`enabled: tab === 'used-by'`) |

## Testing

### Backend

**Integration** (`Integration/KnowledgeBase/ConsumingAgentsEndpointTests.cs` — Testcontainers Postgres, il containment JSONB **non** è traducibile su EF InMemory):
- `GET 200` con agent che ha docId in KbCardIds → lista contiene l'agent (AC1)
- `GET 200` esclude agent senza docId (AC2)
- `GET 200` esclude agent soft-deleted (AC3)
- `GET 200` include system agent con flag (AC4)
- `GET 200` lista vuota se 0 match (AC5)
- `GET 200` risolve GameName; null se gioco assente (AC6)
- `GET 200` con Guid.Empty → lista vuota (AC7)
- `GET 401` senza auth (AC8)

**Unit** (`Application.Tests/.../GetConsumingAgentsByDocumentIdQueryHandlerTests.cs`) — solo la logica di mapping/bulk-name (il repo è mockato; il containment è coperto dagli integration):
- mapping DTO completo (system flag, typology, status string)
- bulk GameName resolution (no N+1, dictionary lookup)
- repo ritorna lista vuota → handler ritorna lista vuota

### Frontend (target 85%+ Vitest)

- `useKbDocConsumingAgents.test.ts`: `enabled` solo se `docId !== null && tab === 'used-by'`; `refetchInterval=false`
- `UsedByAgentRow.test.tsx`: badge "sistema" solo se `isSystemDefined`; label "KB globale" se `gameName` null; href `/admin/agents/definitions/{id}`; status chip per Draft/Testing/Published
- `UsedByPanel.test.tsx`: stati loading (skeleton) / empty (0 agent) / ready (lista) / error (retry)
- `KbDocDetailTabs.test.tsx`: terzo tab "Used by" presente, link preserva `docId`, `?tab=used-by`
- `KbDocDetailPanel.test.tsx`: render branch `tab=used-by` (preserva i 7+2 test esistenti)

### E2E smoke (Playwright, opzionale P3)

`apps/web/e2e/admin/kb-used-by.smoke.spec.ts`: login admin → `/admin/knowledge-base?docId={existing}&tab=used-by` → verifica lista o empty state; click riga → naviga a `/admin/agents/definitions/{id}`.

## Out of scope (follow-up)

- ❌ **Detach documento** (rimuovi docId da KbCardIds) — D2 = read-only. Eventuale follow-up con command + conferma.
- ❌ **Paginazione** — il result set è piccolo (un doc appartiene a un gioco → pochi agent). Da rivedere solo se emergono doc condivisi da decine di agent.
- ❌ **Polling** — l'associazione è statica (cambia solo su edit agent).
- ❌ **Filtri (per type/status)** — over-engineering per P3.
- ❌ **Reverse view** (documenti di un agent) — è già coperto altrove nel detail dell'agent.

## Rischi noti

| Rischio | Probabilità | Mitigazione |
|---|---|---|
| Traduzione LINQ `EF.Functions.JsonContains` su shadow property scomoda | Media | Fallback `FromSqlInterpolated` con `@>` parametrizzato (no SQL injection — `{docId}` è Guid) |
| Indice GIN non applicato in staging (migration manuale, vedi memoria) | Media | Verificare applicazione migration in deploy; il containment funziona anche senza indice (solo più lento) |
| Link `/admin/agents/definitions/{id}` per agent system | Bassa | Route esistente gestisce tutti gli agent; verificare che il detail renderizzi system agent (read-only) |
| `KbCardIds` contiene docId di un doc poi eliminato | Bassa | Fuori scope qui — è un dato stale lato agent, non un problema di questa view (mostra solo agent → doc, non viceversa) |

## Definition of Done

- [ ] Backend: query + handler + DTO dedicato + repo method JSONB `@>` + migration indice GIN + integration tests (Testcontainers) + unit tests
- [ ] Frontend: 3 nuovi componenti + 1 hook + admin-kb.ts + tests (≥85% sulle nuove righe)
- [ ] `KbDocDetailTabs` esteso con tab "Used by"; `KbDocDetailPanel` branch render
- [ ] Lint + typecheck verdi (FE); `dotnet build` + test verdi (BE)
- [ ] PR verso `main-dev` che linka questo design doc
- [ ] `/code-review:code-review` eseguito, issue P0/P1 risolti
- [ ] Issue #1651 aggiornata su GitHub con riferimento PR
- [ ] Merge in `main-dev` + issue #1651 chiusa con auto-link al commit

## Riferimenti

- Issue: [#1651](https://github.com/meepleAi-app/meepleai-monorepo/issues/1651) · gemello [#1650](https://github.com/meepleAi-app/meepleai-monorepo/issues/1650) (PR #1668)
- Design doc gemello: `docs/superpowers/specs/2026-05-29-kb-ingestion-log-tab-design.md`
- Backend entity: `BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs` (KbCardIds #4932)
- EF config: `BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs` (kb_card_ids jsonb)
- Pattern bulk-name: `BoundedContexts/KnowledgeBase/Application/Queries/GetAllAgentsQueryHandler.cs` (#660)
- Routing: `Routing/AdminKnowledgeBaseEndpoints.cs` (group `/admin/kb`, endpoint `ingestion-log` #1650)
- FE detail panel: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`
- FE tabs: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailTabs.tsx`
- FE agent detail route (link target): `apps/web/src/app/admin/(dashboard)/agents/definitions/[id]/page.tsx`
