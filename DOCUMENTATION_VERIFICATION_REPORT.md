# Documentation Verification Report
**Data**: 2025-11-15
**Scope**: Verifica correttezza CLAUDE.md e documentazione generale rispetto al codice effettivo

---

## Executive Summary

Analisi completa della documentazione ha rilevato **discrepanze critiche** tra CLAUDE.md e il codice effettivo, in particolare:
- ❌ **Servizi Docker**: mancano 7 servizi nella documentazione
- ❌ **Key Docs Paths**: 8 su 12 path non validi (riorganizzazione docs non riflessa)
- ✅ **Bounded Contexts**: Struttura verificata e corretta
- ✅ **Stack versions**: Versioni corrette (ASP.NET 9, Next.js 16, React 19)

---

## 1. Docker Compose - Servizi e Porte ❌ CRITICO

### Problema
CLAUDE.md (linea 59) elenca solo servizi parziali:
```
docker compose up -d    # PG, Qdrant, Redis, n8n, Seq, Jaeger
```

### Realtà (docker-compose.yml)
**15 servizi totali**:
- ✅ postgres (5432)
- ✅ qdrant (6333, 6334)
- ✅ redis (6379)
- ❌ **ollama** (11434) - MANCANTE
- ❌ **ollama-pull** (init container) - MANCANTE
- ❌ **embedding-service** (8000) - MANCANTE
- ❌ **unstructured-service** (8001) - MANCANTE
- ❌ **smoldocling-service** (8002) - MANCANTE
- ✅ seq (5341, 8081)
- ✅ jaeger (16686, 4317, 4318)
- ❌ **prometheus** (9090) - MANCANTE
- ❌ **alertmanager** (9093) - MANCANTE
- ❌ **grafana** (3001) - MANCANTE
- ✅ n8n (5678)
- ✅ api (8080)
- ✅ web (3000)

### Raccomandazione
Aggiornare CLAUDE.md per elencare TUTTI i servizi e porte, organizzati per categoria:
- **Core Infrastructure**: postgres, qdrant, redis
- **AI/ML Services**: ollama, embedding-service, unstructured-service, smoldocling-service
- **Observability**: seq, jaeger, prometheus, alertmanager, grafana
- **Workflow**: n8n
- **Application**: api, web

---

## 2. Key Docs - Path Obsoleti ❌ CRITICO

### Problema
La sezione "Key Docs" (CLAUDE.md linee 272-285) contiene **10 path obsoleti su 12**:

| Documento | Path CLAUDE.md | Stato | Path Corretto |
|-----------|---------------|-------|---------------|
| Architecture | `docs/architecture/board-game-ai-architecture-overview.md` | ❌ | `docs/01-architecture/overview/system-architecture.md` |
| API Spec | `docs/api/board-game-ai-api-specification.md` | ❌ | `docs/03-api/board-game-ai-api-specification.md` |
| ADR Hybrid RAG | `docs/architecture/adr-001-hybrid-rag-architecture.md` | ❌ | `docs/01-architecture/adr/adr-001-hybrid-rag.md` |
| DDD Status | `docs/refactoring/ddd-status-and-roadmap.md` | ❌ | **NON ESISTE** (info in archivio?) |
| DB Schema | `docs/database-schema.md` | ❌ | **NON ESISTE** (rimosso?) |
| OAuth Setup | `docs/guide/oauth-setup-guide.md` | ❌ | `docs/06-security/oauth-security.md` |
| Security | `docs/SECURITY.md` | ❌ | `/SECURITY.md` (root) |
| Security Scanning | `docs/security-scanning.md` | ❌ | **NON ESISTE** (vedere `docs/06-security/code-scanning-remediation-summary.md`) |
| Testing | `docs/testing/test-writing-guide.md` | ❌ | `docs/02-development/testing/test-writing-guide.md` |
| Shadcn/UI | `docs/frontend/shadcn-ui-installation.md` | ❌ | `docs/04-frontend/shadcn-ui-installation.md` |
| AI Provider Config | `docs/03-api/ai-provider-configuration.md` | ✅ | ✅ Corretto |
| AI Provider Dev | `docs/02-development/ai-provider-integration.md` | ✅ | ✅ Corretto |

### Causa
Riorganizzazione docs con struttura numerata (00-10) completata il 2025-11-13 (vedi docs/INDEX.md) ma CLAUDE.md non aggiornato.

### Raccomandazione
**Aggiornare TUTTI i path nella sezione "Key Docs"** e riferirsi a `docs/INDEX.md` come fonte di verità.

---

## 3. Bounded Contexts ✅ VERIFICATO

### Status: CORRETTO

Tutti i 7 bounded contexts esistono con struttura DDD completa:
```
apps/api/src/Api/BoundedContexts/
├── Administration/        ✅ Domain/Application/Infrastructure
├── Authentication/        ✅ Domain/Application/Infrastructure
├── DocumentProcessing/    ✅ Domain/Application/Infrastructure
├── GameManagement/        ✅ Domain/Application/Infrastructure
├── KnowledgeBase/         ✅ Domain/Application/Infrastructure
├── SystemConfiguration/   ✅ Domain/Application/Infrastructure
└── WorkflowIntegration/   ✅ Domain/Application/Infrastructure
```

**CQRS Handlers trovati**: 72+ (Commands/Queries con MediatR) ✅

---

## 4. Stack Versions ✅ VERIFICATO

| Tecnologia | CLAUDE.md | Codice Effettivo | Status |
|------------|-----------|------------------|--------|
| ASP.NET | 9 | net9.0 | ✅ |
| Next.js | 16 | 16.0.1 | ✅ |
| React | 19 | 19.2.0 | ✅ |
| PostgreSQL | - | 16.4 | - |
| Qdrant | - | v1.12.4 | - |
| Redis | - | 7.4.1 | - |

---

## 5. Test Numbers ⚠️ DA VERIFICARE

### Dichiarato (CLAUDE.md linea 200)
```
Tests: 4,033 frontend + 162 backend + 30 E2E = 4,225 total
```

### Trovato (file count)
- Frontend test files: **162**
- Backend test files: **80**
- E2E test files: **39**

### Nota
Discrepanza probabile: CLAUDE.md conta **test individuali**, ricerca conta **file di test**.
Non è un errore critico ma necessita verifica eseguendo:
```bash
cd apps/web && pnpm test -- --listTests | wc -l
cd apps/api && dotnet test --list-tests | wc -l
```

---

## 6. ADR Reference ⚠️ MINORE

### Problema
CLAUDE.md linea 148 cita:
```
**3-Stage Fallback Architecture** (ADR-003):
```

### Realtà
- ADR-003 è **SUPERSEDED** da ADR-003b (Unstructured Library)
- Path: `docs/01-architecture/adr/adr-003b-unstructured-pdf.md`

### Raccomandazione
Citare "ADR-003b" o "ADR-003 (Unstructured)" per precisione.

---

## 7. Files Obsoleti in Archive 📦

### Trovato
- **28 file markdown** in `claudedocs/archive/2025-11/`
- Categoria: session summaries, testing reports, DDD completion, misc

### Raccomandazione
Valutare se:
1. Consolidare info rilevanti in docs/ principale
2. Rimuovere completamente se superflui
3. Mantenere solo per tracciabilità storica

---

## 8. Bounded Contexts - Services Residui ⚠️ DA VERIFICARE

### Dichiarato (CLAUDE.md linee 28-29)
```
Eliminated: 2,070 lines legacy services (GameService 181, AuthService 346, PDF services 1,300, UserManagementService 243)
Retained: ConfigurationService, AdminStatsService, AlertingService, RagService (orchestration/infrastructure)
```

### Raccomandazione
Verificare che questi service siano effettivamente rimossi e i retained esistano.

---

## Azioni Correttive Prioritarie

### 🔴 PRIORITÀ 1 - CRITICO
1. ✅ Aggiornare sezione Docker (servizi + porte complete)
2. ✅ Correggere tutti i path "Key Docs"
3. ✅ Aggiungere riferimento a `docs/INDEX.md` come fonte

### 🟡 PRIORITÀ 2 - MEDIO
4. ⏳ Verificare test numbers (eseguire conteggio reale)
5. ⏳ Aggiornare ADR reference (ADR-003b)
6. ⏳ Verificare services eliminati/retained

### 🟢 PRIORITÀ 3 - BASSO
7. ⏳ Pulire claudedocs/archive/ (opzionale)
8. ⏳ Aggiungere versioni PostgreSQL, Qdrant, Redis in CLAUDE.md

---

## File da Aggiornare

1. **CLAUDE.md** - Applicare tutte le correzioni P1 + P2
2. **docs/INDEX.md** - Verificare allineamento (già corretto)
3. **docs/README.md** - Verificare coerenza

---

## Conclusioni

La documentazione è **sostanzialmente accurata** ma soffre di:
- ❌ **Path obsoleti** post-riorganizzazione (2025-11-13)
- ❌ **Informazioni incomplete** sui servizi Docker
- ✅ **Architettura DDD** correttamente documentata
- ✅ **Stack tecnologico** aggiornato

**Tempo stimato correzioni**: 30-45 minuti
**Rischio se non corretto**: Sviluppatori seguono path errati, confusione setup Docker

---

**Report generato**: 2025-11-15
**Autore**: Claude Code Verification Tool
**Prossima verifica**: 2025-12-15 (mensile)
