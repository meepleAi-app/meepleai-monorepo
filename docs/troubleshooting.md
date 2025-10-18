# Troubleshooting Guide

Questa guida raccoglie problemi comuni riscontrati nel sistema MeepleAI, con le soluzioni implementate e le misure preventive adottate.

## Come Usare Questa Guida

1. **Cerca per tipo di errore**: Usa Ctrl+F per cercare messaggi di errore specifici
2. **Consulta per layer**: Backend, Frontend, Infra, Database
3. **Vedi misure preventive**: Ogni sezione include validation guards, test coverage, monitoring

---

## Backend (ASP.NET Core)

### ConnectionTimeout: Redis Connection Pool Exhausted

**Sintomo**: L'applicazione risponde con timeout su endpoint che usano cache Redis (es. `/api/v1/agents/qa`)

**Root Cause**: Connection pool Redis esaurito sotto carico elevato, nessuna gestione circuit breaker

**Soluzione**:
- Implementato Circuit Breaker pattern per Redis connections
- Configurato pool size dinamico in `appsettings.json`
- Aggiunto fallback a direct database query se cache non disponibile

**Prevenzione**:
- Validation guard: `REDIS_URL` validato all'avvio dell'app
- Test coverage: `RagServiceResilienceTests.cs` (8 test)
- Monitoring: `meepleai.circuitbreaker.state_changes.total` metric

**Riferimenti**: Issue #XXX, PR #YYY

---

## Frontend (Next.js)

### TypeScript: Property Does Not Exist on Type

**Sintomo**: Build fallisce con errori TypeScript su proprietà mancanti

**Root Cause**: Mismatch tra type definitions e implementazione API

**Soluzione**:
- Aggiornato `@/lib/api.ts` con type definitions corrette
- Sincronizzati types con backend DTOs

**Prevenzione**:
- Validation: Strict TypeScript mode abilitato
- Test coverage: Type tests in `__tests__/api.test.ts`
- CI: `pnpm typecheck` obbligatorio prima del merge

**Riferimenti**: Issue #XXX, PR #YYY

---

## Database (PostgreSQL)

### Migration: Relation Does Not Exist

**Sintomo**: L'app fallisce all'avvio con errore `42P01: relation "table_name" does not exist`

**Root Cause**: Migrations non applicate o schema out of sync

**Soluzione**:
- Eseguito `dotnet ef database update`
- Verificato `__EFMigrationsHistory` table
- Reset database in dev environment

**Prevenzione**:
- Validation: Auto-apply migrations in Development (`Program.cs:184`)
- Test coverage: Migration integration tests
- CI: Migration dry-run in pipeline

**Riferimenti**: Issue #XXX, PR #YYY

---

## Infrastructure (Docker)

### Docker: Port Already Allocated

**Sintomo**: `docker compose up` fallisce con errore port binding

**Root Cause**: Altro processo usa la porta o container zombie

**Soluzione**:
```bash
# Trova processo sulla porta
netstat -ano | findstr :8080
# Termina processo
taskkill /PID <pid> /F
# O cambia porta in docker-compose.yml
```

**Prevenzione**:
- Documentation: Guida port conflicts in README
- Scripts: `tools/check-ports.ps1` per pre-flight check
- CI: Health checks verificano port availability

**Riferimenti**: Issue #XXX, PR #YYY

---

## CORS & Authentication

### CORS: No Access-Control-Allow-Origin Header

**Sintomo**: Fetch da frontend fallisce con CORS error

**Root Cause**: CORS policy non configurata o origin non allowed

**Soluzione**:
- Aggiunto origin in `appsettings.json:AllowedOrigins`
- Verificato CORS middleware in `Program.cs`
- Abilitato credentials in frontend fetch

**Prevenzione**:
- Validation: CORS config validata all'avvio
- Test coverage: CORS integration tests
- Monitoring: Log CORS rejections in Seq

**Riferimenti**: Issue #XXX, PR #YYY

---

## Performance & Scalability

### High Memory Usage: PDF Processing

**Sintomo**: Container API va OOM durante processing PDF grandi

**Root Cause**: Caricamento intero PDF in memoria senza streaming

**Soluzione**:
- Implementato streaming per PDF extraction
- Aggiunto max file size limit (100MB)
- Configurato memory limits in docker-compose

**Prevenzione**:
- Validation: File size check pre-upload
- Test coverage: Large file processing tests
- Monitoring: Memory usage metrics in Prometheus

**Riferimenti**: Issue #XXX, PR #YYY

---

## Testing & CI/CD

### Test Failures: Testcontainers Timeout

**Sintomo**: Integration tests falliscono con timeout in CI

**Root Cause**: Container startup lento su CI runners

**Soluzione**:
- Aumentato timeout da 30s a 120s
- Aggiunto health check polling
- Cached Docker images in CI

**Prevenzione**:
- CI: Pre-pull images in setup step
- Test: Retry logic per container startup
- Monitoring: CI test duration tracking

**Riferimenti**: Issue #XXX, PR #YYY

---

## Observability

### Seq Logs: Not Receiving Events

**Sintomo**: Seq dashboard vuoto, nessun log visibile

**Root Cause**: `SEQ_URL` non configurato o Seq container down

**Soluzione**:
```bash
# Verifica Seq health
curl http://localhost:8081/api
# Verifica env var
echo $SEQ_URL
# Restart Seq container
docker compose restart seq
```

**Prevenzione**:
- Validation: SEQ_URL validato all'avvio con fallback a console
- Health check: Seq incluso in `/health` endpoint
- Documentation: Observability guide in `docs/observability.md`

**Riferimenti**: Issue #XXX, PR #YYY

---

## Come Aggiungere Nuove Voci

Il comando `/debug` aggiorna automaticamente questo file quando risolve errori. Puoi anche aggiungere manualmente:

```markdown
## {Layer}: {Error Type}

**Sintomo**: {Come si manifesta}

**Root Cause**: {Causa tecnica}

**Soluzione**: {Fix implementato}

**Prevenzione**:
- {Validation guard}
- {Test coverage}
- {Monitoring}

**Riferimenti**: Issue #XXX, PR #YYY

---
```

## Risorse Correlate

- [Observability Guide](./observability.md) - Health checks, logging, Seq, Jaeger
- [Database Schema](./database-schema.md) - Riferimento completo schema DB
- [Security Guide](./SECURITY.md) - Best practices sicurezza
- [CLAUDE.md](../CLAUDE.md) - Guida completa al progetto
