# Design: Migrazione Qdrant → pgvector-only

**Data**: 2026-03-29
**Branch target**: `main-dev`
**Tipo**: Rimozione componente + cleanup tecnico
**Scope**: Backend (API), Frontend (Next.js), Infra (secrets, prometheus)

---

## Contesto

MeepleAI usa un dual-stack vettoriale: `PgVectorStoreAdapter` (pgvector su PostgreSQL) e riferimenti residui a Qdrant nel frontend admin e nell'infrastruttura. Il backend ha già completato la migrazione (`PgVectorStoreAdapter` è l'unica implementazione di `IVectorStoreAdapter`, `GET /api/v1/admin/kb/vector-collections` restituisce già array vuoto con commento "Qdrant removed").

L'obiettivo è eliminare tutti i riferimenti Qdrant residui, semplificare lo stack operativo, e adattare la pagina admin Vector Collections per mostrare dati reali da pgvector.

---

## Scope completo

### Da rimuovere

| Area | File/Componente | Azione |
|------|----------------|--------|
| Frontend - Admin page | `knowledge-base/vectors/page.tsx` | Riscrivere per pgvector |
| Frontend - API client | `adminAiClient.ts` | Rimuovere metodi Qdrant, aggiungere pgvector |
| Frontend - API client | `adminClient.ts` | Rimuovere re-export Qdrant types |
| Frontend - Component | `vector-collection-card.tsx` | Sostituire con `vector-game-card.tsx` |
| Frontend - Component | `kb-settings.tsx` | Rimuovere "Rebuild all Qdrant collections" |
| Frontend - RAG dashboard | `ArchitectureExplorer.tsx` | `qdrant` node → `pgvector` |
| Frontend - RAG dashboard | `PocStatus.tsx` | Aggiornare testi Qdrant → pgvector |
| Frontend - RAG dashboard | `TechnicalReference.tsx` | Aggiornare config Qdrant → pgvector |
| Frontend - RAG dashboard | `rag-data.ts`, `strategy-details-data.ts` | Sostituire riferimenti Qdrant |
| Frontend - RAG dashboard | `DecisionWalkthrough.tsx`, `StrategyFlowVisualizer.tsx`, `TokenFlowVisualizer.tsx`, `types-configurable.ts` | Aggiornare label Qdrant |
| Frontend - Monitor | `CommandCenterDashboard.tsx` | Rimuovere `qdrant` service card |
| Frontend - Monitor | `ServiceStatusCard.tsx` | Rimuovere qdrant service |
| Frontend - Stories | `ServiceCard.stories.tsx`, `ServiceHealthMatrix.stories.tsx` | Rimuovere storie qdrant |
| Frontend - KB page | `knowledge-base/page.tsx` | Aggiornare descrizione |
| Frontend - Documents page | `knowledge-base/documents/page.tsx` | `storageHealth.qdrant` → pgvector |
| Frontend - AI tab | `RagTab.tsx` | Aggiornare descrizione |
| Backend - Routing | `AdminKnowledgeBaseEndpoints.cs` | Aggiungere `GET /vector-stats` e `POST /vector-search` |
| Backend - Routing | `AdminPipelineEndpoints.cs` | `storageHealth.Qdrant.*` → pgvector equivalenti |
| Backend - Routing | `MonitoringEndpoints.cs` | Rimuovere `/health/qdrant` endpoint |
| Backend - Routing | `AdminSecretsEndpoints.cs` | Rimuovere `qdrant.secret` dalla lista |
| Backend - Routing | `AdminSandboxEndpoints.cs` | Aggiornare description (rimuovere "Qdrant") |
| Backend - Routing | `AdminPdfStorageEndpoints.cs` | Aggiornare summary |
| Backend - Routing | `SharedGameCatalogAdminEndpoints.cs` | Aggiornare description |
| Infra | `infra/secrets/qdrant.secret` | Eliminare il file |
| Infra | `infra/prometheus/alerts/api-performance.yml` | Rimuovere riferimenti Qdrant |
| Infra | `infra/prometheus/alerts/quality-metrics.yml` | Rimuovere riferimenti Qdrant |
| Infra | `infra/prometheus-rules.yml` | Rimuovere health checks Qdrant |
| Infra | `infra/alertmanager.yml` | Aggiornare commento |
| Tests | `kb-hub-gaps.test.tsx` | Aggiornare mock da Qdrant a pgvector |
| Tests | `ServicesDashboard.test.tsx`, `ResourcesTab.test.tsx` | Rimuovere assertion Qdrant |
| Tests | `ServiceCard.test.tsx`, `ServiceHealthMatrix.test.tsx`, `SystemStatus.test.tsx` | Rimuovere Qdrant |
| Tests | `ArchitectureExplorer.test.tsx` | Aggiornare nodi |

### Da aggiungere

| Area | File/Componente | Azione |
|------|----------------|--------|
| Backend - Endpoint | `AdminKnowledgeBaseEndpoints.cs` | `GET /api/v1/admin/kb/vector-stats` |
| Backend - Endpoint | `AdminKnowledgeBaseEndpoints.cs` | `POST /api/v1/admin/kb/vector-search` |
| Backend - Query | `GetVectorStatsQuery` + Handler | Stats pgvector da `vector_documents` |
| Backend - Query | `VectorSemanticSearchQuery` + Handler | Semantic search via `IVectorStoreAdapter` |
| Frontend - Component | `vector-game-card.tsx` | Card per gioco con vector count + health |
| Frontend - API client | `adminAiClient.ts` | `getVectorStats()`, `searchVectors(query, limit)` |

---

## Nuovi Endpoint Backend

### `GET /api/v1/admin/kb/vector-stats`

**Response:**
```json
{
  "totalVectors": 8420,
  "dimensions": 768,
  "gamesIndexed": 42,
  "avgHealthPercent": 97,
  "sizeEstimateBytes": 24678400,
  "gameBreakdown": [
    {
      "gameId": "uuid",
      "gameName": "Catan",
      "vectorCount": 215,
      "completedCount": 215,
      "failedCount": 0,
      "healthPercent": 100
    }
  ]
}
```

**Implementazione:** Query EF Core su `vector_documents` GROUP BY `GameId`, JOIN con `shared_game_catalog` per il nome.

### `POST /api/v1/admin/kb/vector-search`

**Request:**
```json
{
  "query": "regole per il commercio",
  "limit": 10,
  "gameId": "uuid (opzionale)"
}
```

**Response:**
```json
{
  "results": [
    {
      "id": 1234,
      "score": 0.87,
      "payload": {
        "text": "...",
        "gameId": "uuid",
        "documentId": "uuid",
        "chunkIndex": 3
      }
    }
  ]
}
```

**Implementazione:** Usa `IVectorStoreAdapter.SearchAsync()` (già esistente) con embedding della query via `IEmbeddingService`.

---

## Pagina Admin `/admin/knowledge-base/vectors` (adattata)

### Stats cards (4 cards):
- **Total Vectors** — count reale da `vector_documents`
- **Games Indexed** — giochi con almeno 1 vettore Completed
- **Dimensions** — `768` (costante da `EmbeddingProviderType.External`)
- **Avg Health** — % vettori con status = Completed

### Semantic Search panel:
- **Rimosso**: dropdown "Select collection" (non esiste in pgvector)
- **Mantenuto**: textbox query + Top N selector + bottone Search
- **Aggiunto**: filtro opzionale per gioco
- **Search** abilitato se: query non vuota (collection non richiesta)

### Game cards (sostituiscono Collection cards):
- Una card per ogni gioco indicizzato
- Mostra: nome gioco, vector count, health %, status badge
- **Rimosso**: bottoni Delete collection e Reindex (non applicabili)

---

## Refactoring StorageHealth (backend)

`PdfStorageHealthDto` ha una proprietà `QdrantInfoDto Qdrant` che il handler popola già con valori zerati (`VectorCount=0, IsAvailable=false`). Occorre:

1. **Rinominare** `QdrantInfoDto` → `VectorStoreInfoDto` e la proprietà `Qdrant` → `VectorStore`
2. **Popolare con dati reali pgvector**: il handler interroga `vector_documents` per il conteggio vettori reale invece di restituire `0`
3. **Aggiornare** `AdminPipelineEndpoints.cs`: `storageHealth.Qdrant.*` → `storageHealth.VectorStore.*`
4. **Adattare** `BuildIndexStage`: `IsAvailable` diventa `true` se PostgreSQL è disponibile (già garantito dal fatto che il handler gira)

Il campo `MemoryFormatted` viene rimosso da `VectorStoreInfoDto` (non rilevante per pgvector, che usa storage su disco PostgreSQL condiviso).

---

## Testing

### Backend (aggiornamenti):
- Aggiungere unit test per `GetVectorStatsQueryHandler`
- Aggiungere unit test per `VectorSemanticSearchQueryHandler`
- Nessun test esistente usa mock Qdrant

### Frontend (aggiornamenti):
- `kb-hub-gaps.test.tsx`: sostituire mock `searchQdrantCollection/deleteQdrantCollection/rebuildQdrantIndex` con `searchVectors/getVectorStats`
- Test monitor: rimuovere asserzioni su "Qdrant" service/uptime
- Test architecture explorer: aggiornare nodo `qdrant` → `pgvector`

---

## Infrastruttura

- `infra/secrets/qdrant.secret` → **eliminare**
- `infra/prometheus/alerts/` → rimuovere tutti i check "qdrant" (container, connection, health)
- `infra/prometheus-rules.yml` → rimuovere regole qdrant
- Docker compose → già senza container Qdrant (nessuna modifica)

---

## Rischi e mitigazioni

| Rischio | Probabilità | Mitigazione |
|---------|-------------|-------------|
| `storageHealth.Qdrant` usato in altri endpoint non trovati | Bassa | Grep completo su `storageHealth.Qdrant` prima di iniziare |
| `IEmbeddingService` non iniettabile nell'endpoint admin | Bassa | Già usato in `AdminRagExecutionEndpoints` |
| Test frontend che mockano Qdrant SDK non trovati | Bassa | Grep su `qdrant` in `__tests__` prima di iniziare |

---

## Definition of Done

- [ ] Nessun file nel repo contiene la stringa `Qdrant` (case-insensitive) eccetto commenti storici in ADR
- [ ] `qdrant.secret` non esiste in `infra/secrets/`
- [ ] `GET /api/v1/admin/kb/vector-stats` risponde con dati reali
- [ ] `POST /api/v1/admin/kb/vector-search` esegue semantic search via pgvector
- [ ] Pagina `/admin/knowledge-base/vectors` mostra dati pgvector reali
- [ ] `pnpm test` passa senza errori
- [ ] `dotnet test` passa senza errori
- [ ] `pnpm typecheck` passa senza errori
