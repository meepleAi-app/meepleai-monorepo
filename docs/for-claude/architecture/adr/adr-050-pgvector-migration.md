# ADR-050: Migrazione Vector Store da Qdrant a pgvector

**Status**: Implemented
**Date**: 2026-03-29
**Issue**: Refactoring KB — rimozione Qdrant
**Decision Makers**: Engineering Lead
**Related Spec**: **docs/for-developers/specs/2026-03-29-qdrant-to-pgvector-migration-design.md** _(planned)_

---

## Context

MeepleAI usava un dual-stack vettoriale: `PgVectorStoreAdapter` (pgvector su PostgreSQL) come implementazione primaria e Qdrant come servizio separato con riferimenti residui in frontend, infrastruttura e monitoring.

Il backend aveva già completato la migrazione al momento di questa decisione:
- `PgVectorStoreAdapter` era l'unica implementazione di `IVectorStoreAdapter`
- `GET /api/v1/admin/kb/vector-collections` restituiva array vuoto con commento "Qdrant removed"
- I dati vettoriali erano già esclusivamente in `vector_documents` (PostgreSQL)

Rimanevano riferimenti Qdrant "fossili" in:
- Frontend admin (pagina Vector Collections, RAG dashboard, monitoring)
- Infrastruttura Docker Compose (servizio `qdrant`)
- Prometheus alerts e health checks
- Documentazione di sviluppo

**Driver della decisione**:
1. **Semplicità operativa**: eliminare un servizio Docker riduce il surface da gestire
2. **Coerenza**: il dual-stack generava confusione nei nuovi sviluppatori
3. **Costo infrastruttura**: Qdrant richiedeva RAM dedicata (300-500 MB) senza aggiungere valore
4. **PostgreSQL già robusto**: pgvector con indice HNSW offre performance adeguate per il volume attuale (<50K vettori)

---

## Decision

**Rimuovere completamente Qdrant dallo stack** e consolidare tutto su pgvector (estensione PostgreSQL già in uso).

### Architettura risultante

```
IVectorStoreAdapter
└── PgVectorStoreAdapter (unica implementazione)
    └── PostgreSQL 16 + pgvector extension
        └── vector_documents table (embedding 1024-dim BGE-M3)
```

### Perimetro delle modifiche

| Area | Azione |
|------|--------|
| Frontend admin | Riscrittura pagina Vector Collections per pgvector |
| Frontend monitoring | Rimozione card servizio Qdrant |
| Backend endpoints | Aggiunta `GET /vector-stats` e `POST /vector-search` |
| Infra Docker | Rimozione servizio `qdrant` dal Compose |
| Prometheus | Rimozione health checks e alert Qdrant |
| Secrets | Rimozione `qdrant.secret` |
| Documentazione | Allineamento tutti i riferimenti Qdrant → pgvector |

---

## Consequences

### Positive

- Stack operativo semplificato: -1 servizio Docker (risparmio ~400 MB RAM)
- Unica fonte di verità per i dati vettoriali (PostgreSQL)
- Backup e restore unificati (i vettori fanno parte del backup PostgreSQL standard)
- Nuovi endpoint `/vector-stats` e `/vector-search` per visibilità admin

### Negative

- pgvector non scala orizzontalmente come Qdrant per dataset >1M vettori
- Indici HNSW richiedono `VACUUM` periodico per performance ottimali

### Rischio residuo

Per dataset >500K vettori valutare pgvecto.rs o migrazione a servizio dedicato (vedi ADR-045 multi-region strategy).

---

## Alternatives Considered

| Alternativa | Motivo scartata |
|------------|-----------------|
| Mantenere Qdrant come primary | Dual-stack già abbandonato di fatto, overhead operativo ingiustificato |
| Migrazione a Weaviate | Aggiunge complessità senza benefici a scala attuale |
| pgvecto.rs | Dipendenza Rust non matura, rimandato a futura valutazione |

---

**Implemented**: 2026-03-29 (commit `5fbe75c8d`)
**Maintainer**: Engineering Team
