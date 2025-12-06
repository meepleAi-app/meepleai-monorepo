# 🧪 Guida al Tester - MeepleAI 2025
**Testing Checkpoints per Roadmap Verification**

**Versione**: 2.0
**Data**: 2025-11-17
**Owner**: QA Team & Engineering Lead

---

## 📋 Indice

1. [Overview](#overview)
2. [Checkpoint Strategy](#checkpoint-strategy)
3. [CHECKPOINT 0: Baseline Verification (OBBLIGATORIO)](#checkpoint-0-baseline-verification-obbligatorio)
4. [CHECKPOINT IMMEDIATO: Critico (Week 1-2)](#checkpoint-immediato-critico-week-1-2)
5. [CHECKPOINT 1: MVP Quality Gate (Week 3-6)](#checkpoint-1-mvp-quality-gate-week-3-6)
6. [CHECKPOINT 2: Phase 1B Integration (Week 7-9)](#checkpoint-2-phase-1b-integration-week-7-9)
7. [CHECKPOINT 3: Production Readiness (Week 10-12)](#checkpoint-3-production-readiness-week-10-12)
8. [Appendice: Scenari di Test Dettagliati](#appendice-scenari-di-test-dettagliati)

---

## Overview

Questa guida fornisce **checkpoint di verifica manuale** basati sullo stato reale del progetto (aggiornato 17 Nov 2025).

### Stato Attuale Progetto

**Completato**:
- ✅ DDD Migration 100%
- ✅ Frontend Modernization (FE-IMP-001 → FE-IMP-006) 100%
- ✅ BGAI Validation Foundation (4/4 completate)

**In Progress**:
- 🔄 4 Issue Critiche P0/P1 (blockers)
- 🔄 12 Month 6 MVP tasks (quality gate)
- 🔄 6 Phase 1B tasks (integration)

**Metriche Target**:
- **Accuracy BGAI**: ≥80% (target: 95%)
- **P95 Latency**: <3s
- **Test Coverage**: ≥90%
- **Hallucination Rate**: ≤3%

---

## Checkpoint Strategy

### Approccio Basato su Rischio

I checkpoint sono organizzati per **priorità di rischio** anziché per timeline sequenziale:

| Checkpoint | Focus | Rischio | Effort | Timeframe |
|------------|-------|---------|--------|-----------|
| **0: BASELINE** | Verifica Funzioni Esistenti | 🔥 CRITICO | 4-6 ore | **PRIMA DI TUTTO** |
| **IMMEDIATO** | Critiche P0/P1 | 🔥 ALTO | 1 settimana | Week 1-2 |
| **1: MVP Quality** | Month 6 MVP Tasks | 🔥 ALTO | 3 settimane | Week 3-6 |
| **2: Phase 1B** | Integration Layers | 🟡 MEDIO | 2 settimane | Week 7-9 |
| **3: Production** | Final Polish | 🟢 BASSO | 2 settimane | Week 10-12 |

### Decision Tree

```
START
  ↓
CHECKPOINT 0 (BASELINE) → ❌ FAIL → FIX EXISTING FUNCTIONS
  ↓ ✅ PASS (Sistema funzionante)
CHECKPOINT IMMEDIATO → ❌ FAIL → FIX P0/P1 BLOCKERS
  ↓ ✅ PASS
CHECKPOINT 1 (MVP) → ❌ FAIL → FIX ACCURACY/PERFORMANCE
  ↓ ✅ PASS
CHECKPOINT 2 (Phase 1B) → ❌ FAIL → FIX INTEGRATION
  ↓ ✅ PASS
CHECKPOINT 3 (Production) → ❌ FAIL → FIX POLISH ISSUES
  ↓ ✅ PASS
PRODUCTION RELEASE 🎉
```

---

## CHECKPOINT 0: Baseline Verification (OBBLIGATORIO)

**⚠️ CRITICO**: Questo checkpoint è **OBBLIGATORIO** prima di iniziare qualsiasi sviluppo o test.

**Obiettivo**: Verificare che TUTTE le funzionalità esistenti del sistema siano funzionanti

**Timeline**: PRIMA di qualsiasi modifica al codice
**Durata Test**: 4-6 ore (test completo di tutte le funzionalità)
**Ambiente**: Development (localhost)

### Perché Checkpoint 0 è Obbligatorio?

Questo checkpoint serve a:
1. **Stabilire una baseline funzionante** del sistema
2. **Identificare problemi preesistenti** prima di aggiungere nuove modifiche
3. **Documentare lo stato "as-is"** del sistema
4. **Creare punto di riferimento** per identificare regressioni future
5. **Evitare false positives** nei test successivi

**Se questo checkpoint fallisce**: TUTTI i test successivi sono invalidi fino al fix.

---

### Prerequisiti Setup Ambiente

```bash
# 1. Clone repository (se non fatto)
git clone https://github.com/DegrassiAaron/meepleai-monorepo
cd meepleai-monorepo

# 2. Verifica branch
git checkout main
git pull origin main

# 3. Setup environment
cp infra/env/.env.dev infra/env/.env

# 4. Avvia TUTTI i 15 servizi
cd infra
docker compose up -d

# 5. Verifica tutti i servizi sono UP
docker compose ps

# Output atteso (15 servizi):
# ✅ postgres        Up      5432/tcp
# ✅ qdrant          Up      6333/tcp, 6334/tcp
# ✅ redis           Up      6379/tcp
# ✅ ollama          Up      11434/tcp
# ✅ embedding       Up      8000/tcp
# ✅ unstructured    Up      8001/tcp
# ✅ smoldocling     Up      8002/tcp
# ✅ seq             Up      8081/tcp
# ✅ jaeger          Up      16686/tcp, 14268/tcp
# ✅ prometheus      Up      9090/tcp
# ✅ alertmanager    Up      9093/tcp
# ✅ grafana         Up      3001/tcp
# ✅ n8n             Up      5678/tcp
# ✅ api             Up      8080/tcp
# ✅ web             Up      3000/tcp

# 6. Backend - apply migrations
cd ../apps/api/src/Api
dotnet ef database update
dotnet run

# 7. Frontend (nuovo terminale)
cd apps/web
pnpm install
pnpm dev
```

---

### Test B0.1: Verifica Infrastructure (15 Servizi)

**Obiettivo**: Tutti i 15 servizi Docker devono essere healthy

```bash
# Test health checks di tutti i servizi

1. PostgreSQL:
   docker exec -it postgres psql -U meepleai -d meepleai -c "SELECT version();"
   ✅ VERIFICA: Output mostra PostgreSQL version

2. Qdrant (Vector DB):
   curl http://localhost:6333/healthz
   ✅ VERIFICA: Response "OK"

3. Redis (Cache):
   docker exec -it redis redis-cli PING
   ✅ VERIFICA: Response "PONG"

4. HyperDX (Logging):
   curl http://localhost:8180/api
   ✅ VERIFICA: HTTP 200

5. HyperDX (Tracing):
   curl http://localhost:8180
   ✅ VERIFICA: HTML page loads

6. Prometheus (Metrics):
   curl http://localhost:9090/-/healthy
   ✅ VERIFICA: Response "Prometheus is Healthy"

7. Grafana (Dashboards):
   curl http://localhost:3001/api/health
   ✅ VERIFICA: {"database":"ok"}

8. n8n (Workflows):
   curl http://localhost:5678/healthz
   ✅ VERIFICA: {"status":"ok"}

9. API (Backend):
   curl http://localhost:8080/health
   ✅ VERIFICA: {"status":"Healthy"}

10. Web (Frontend):
    curl http://localhost:3000
    ✅ VERIFICA: HTML page loads

PASS: ☐ 10/10 servizi core healthy
```

---

### Test B0.2: Authentication Completa

#### Scenario B0.2.1: Cookie-Based Login
```
1. Apri browser: http://localhost:3000/login
2. ✅ VERIFICA: Login page carica senza errori

3. Test credenziali errate:
   - Email: wrong@example.com
   - Password: wrongpass
   - Click "Login"
   ✅ VERIFICA: Errore "Invalid credentials" mostrato

4. Test credenziali corrette:
   - Email: admin@meepleai.dev
   - Password: Demo123!
   - Click "Login"
   ✅ VERIFICA:
     - Redirect a /games o /dashboard
     - Cookie "meepleai-session" presente (DevTools → Application → Cookies)
     - Cookie flags: HttpOnly=true, Secure=true, SameSite=Lax

5. Refresh pagina (F5)
   ✅ VERIFICA: Rimani autenticato (no redirect a /login)

6. Logout:
   - Click Logout button
   ✅ VERIFICA:
     - Redirect a /login
     - Cookie rimosso
     - /admin inaccessibile (redirect a /login)

PASS: ☐ Cookie-based authentication OK
```

#### Scenario B0.2.2: API Key Authentication
```
1. Login come admin
2. Navigate /settings → Advanced → API Keys
3. Click "Generate API Key"
4. ✅ VERIFICA:
   - Key generata (formato: mpl_dev_xxxxxxxx)
   - Mostrata una sola volta (warning di copiare)

5. Test API key con curl:
   curl -X GET http://localhost:8080/api/v1/games \
     -H "X-API-Key: [YOUR_API_KEY]"

6. ✅ VERIFICA:
   - HTTP 200
   - Response JSON con lista games

7. Test API key invalida:
   curl -X GET http://localhost:8080/api/v1/games \
     -H "X-API-Key: invalid_key"
   ✅ VERIFICA: HTTP 401 Unauthorized

PASS: ☐ API Key authentication OK
```

#### Scenario B0.2.3: OAuth (Google/Discord/GitHub)
```
1. Logout da sessione corrente
2. Apri /login
3. ✅ VERIFICA: Pulsanti OAuth presenti

4. Click "Continue with Google"
   ✅ VERIFICA: Redirect a Google OAuth

5. [MANUAL] Autorizza con account test
6. ✅ VERIFICA callback:
   - Redirect a /games
   - Utente autenticato
   - Profile info da Google popolata

7. Navigate /settings → Privacy → OAuth Accounts
8. ✅ VERIFICA:
   - Google account "Connected"
   - Pulsante "Disconnect" disponibile

PASS: ☐ OAuth authentication OK
```

#### Scenario B0.2.4: 2FA (TOTP)
```
1. Login admin
2. Navigate /settings → Privacy → 2FA
3. ✅ VERIFICA: Sezione 2FA presente, status "Disabled"

4. Click "Enable 2FA"
5. ✅ VERIFICA:
   - QR code mostrato
   - Setup key testuale disponibile

6. Scansiona QR con app TOTP (Google Authenticator)
7. Inserisci codice 6-digit
8. Click "Verify and Enable"
9. ✅ VERIFICA:
   - 2FA abilitato
   - Backup codes mostrati (8-10 codici)

10. Logout, re-login
11. ✅ VERIFICA:
    - Step 1: Password accepted
    - Step 2: Richiesta codice 2FA
12. Inserisci codice TOTP
    ✅ VERIFICA: Login completato

PASS: ☐ 2FA authentication OK
```

---

### Test B0.3: Games Management (CRUD)

```
1. Navigate /games
   ✅ VERIFICA: Lista games mostrata

2. Click "Add Game"
   - Name: "Test Game"
   - Players: "2-4"
   - Duration: "60 min"
   - Submit
   ✅ VERIFICA: Game creato, appare in lista

3. Click game → Edit
   - Duration: "90 min"
   - Save
   ✅ VERIFICA: Update salvato

4. Delete game
   ✅ VERIFICA: Rimosso da lista

PASS: ☐ Games CRUD operations OK
```

---

### Test B0.4: PDF Upload & Processing (3-Stage Pipeline)

```
1. Navigate /upload
2. Seleziona PDF test (5-10 MB): docs/test-pdfs/catan-it.pdf

3. Durante upload:
   ✅ VERIFICA:
     - Progress bar visibile
     - UI rimane responsive
     - NO errori console

4. Upload completato:
   ✅ VERIFICA:
     - Success message
     - File in /documents list
     - Status: "Processing..."

5. Navigate /admin/documents
6. ✅ VERIFICA status progression:
   - "Queued" → "Processing" → "Completed" (30-60s)

7. Apri HyperDX logs: http://localhost:8180
8. ✅ VERIFICA sequenza log:
   - "Stage 1: Unstructured extraction"
   - Successo OPPURE fallback Stage 2/3

9. Status "Completed":
   ✅ VERIFICA:
     - Quality score ≥0.70
     - Text preview disponibile
     - Pages count corretto

PASS: ☐ PDF 3-stage pipeline OK
```

---

### Test B0.5: RAG / Chat con Streaming & Citations

```
1. Navigate /chat
2. Seleziona game: "Catan"
3. Invia domanda: "Come si costruisce una strada?"

4. ✅ VERIFICA risposta:
   - Inizia entro 2-3s
   - Streaming progressivo
   - Contenuto: "1 legno + 1 argilla"
   - Citation: [Rulebook p.X]
   - NO errori console

5. Click citation link
6. ✅ VERIFICA PDF viewer:
   - Modal apre
   - Auto-scroll a pagina citata
   - Testo evidenziato

7. Follow-up: "E quanto costa?"
8. ✅ VERIFICA:
   - Risposta contestuale
   - Context mantenuto

PASS: ☐ RAG/Chat streaming + citations OK
```

---

### Test B0.6: Admin Features

#### B0.6.1: User Management
```
1. Navigate /admin/users
2. ✅ VERIFICA: Lista utenti con 3 demo users

3. Create user: testuser@example.com
   ✅ VERIFICA: Creato, appare in lista

4. Edit role: User → Editor
   ✅ VERIFICA: Role aggiornato

5. Delete user
   ✅ VERIFICA: Rimosso

PASS: ☐ User management CRUD OK
```

#### B0.6.2: Configuration Management
```
1. Navigate /admin/configuration
2. ✅ VERIFICA categorie:
   - Features
   - RateLimit
   - AI/LLM
   - RAG
   - PDF

3. Edit config: Features:Cache:TtlMinutes = 5 → 10
4. Save
5. ✅ VERIFICA:
   - Valore aggiornato
   - HyperDX log: "Configuration updated"

6. Test rollback:
   - Click "History"
   - Click "Rollback" a versione precedente
   ✅ VERIFICA: Valore ritorna a 5

PASS: ☐ Configuration management OK
```

#### B0.6.3: System Health
```
1. Navigate /admin/health
2. ✅ VERIFICA componenti:
   - PostgreSQL: Healthy ✓
   - Redis: Healthy ✓
   - Qdrant: Healthy ✓
   - Overall: Healthy ✓

3. Simula problema:
   docker compose stop meepleai-redis

4. Refresh /admin/health
5. ✅ VERIFICA:
   - Redis: Unhealthy ✗
   - Overall: Degraded ⚠️

6. Riavvia:
   docker compose start meepleai-redis
   ✅ VERIFICA: Tutto Healthy

PASS: ☐ Health monitoring OK
```

---

### Test B0.7: Settings Page (4 Tabs)

```
1. Navigate /settings

Tab "Profile":
   ✅ Display Name input funzionante
   ✅ Change Password funzionante

Tab "Preferences":
   ✅ Theme toggle (Light/Dark) funziona
   ✅ Language switcher funziona
   ✅ Preferences persistite

Tab "Privacy":
   ✅ 2FA section presente
   ✅ OAuth accounts section presente

Tab "Advanced":
   ✅ API Keys section funzionante
   ✅ Active Sessions section funzionante

PASS: ☐ Settings 4 tabs OK
```

---

### Test B0.8: Observability Stack

```
1. HyperDX (http://localhost:8180):
   ✅ Log entries presenti
   ✅ NO errori critici (24h)

2. HyperDX (http://localhost:8180):
   ✅ Traces presenti
   ✅ Timing info disponibile

3. Prometheus (http://localhost:9090):
   ✅ Query: up → Target meepleai-api = 1
   ✅ Metrics presenti

4. Grafana (http://localhost:3001):
   ✅ Dashboards caricano
   ✅ Panels rendering

PASS: ☐ Observability stack OK
```

---

### Test B0.9: Code Quality Automated

```bash
# Backend tests
cd apps/api
dotnet build
✅ VERIFICA: Build success, zero errors

dotnet test
✅ VERIFICA: All tests pass (162+ tests)

# Frontend tests
cd apps/web
pnpm build
✅ VERIFICA: Build success

pnpm test
✅ VERIFICA: All tests pass, coverage ≥90%

PASS: ☐ Build + tests OK
```

---

### Checklist Finale CHECKPOINT 0 - BASELINE

**Infrastructure (15 servizi)**:
- ☐ PostgreSQL healthy
- ☐ Qdrant healthy
- ☐ Redis healthy
- ☐ HyperDX logs collecting
- ☐ HyperDX traces working
- ☐ Prometheus scraping
- ☐ Grafana dashboards visible
- ☐ n8n workflows ready
- ☐ API running (8080)
- ☐ Web running (3000)
- ☐ Ollama, embedding, unstructured, smoldocling UP

**Authentication**:
- ☐ Cookie-based login OK
- ☐ API Key auth OK
- ☐ OAuth (Google/Discord/GitHub) OK
- ☐ 2FA (TOTP + backup codes) OK
- ☐ Session management OK
- ☐ Logout OK

**Core Features**:
- ☐ Games CRUD OK
- ☐ PDF upload + 3-stage processing OK
- ☐ RAG/Chat streaming + citations OK
- ☐ PDF viewer + jump to page OK
- ☐ Multi-turn conversation OK

**Admin Features**:
- ☐ User management CRUD OK
- ☐ API Keys management OK
- ☐ Configuration management OK
- ☐ System health OK
- ☐ Stats dashboard OK

**Settings Page**:
- ☐ Profile tab OK
- ☐ Preferences tab OK
- ☐ Privacy tab OK
- ☐ Advanced tab OK

**Observability**:
- ☐ HyperDX logs OK
- ☐ HyperDX tracing OK
- ☐ Prometheus metrics OK
- ☐ Grafana dashboards OK

**Code Quality**:
- ☐ `dotnet build` → Success
- ☐ `dotnet test` → All pass
- ☐ `pnpm build` → Success
- ☐ `pnpm test` → All pass, ≥90% coverage

---

### DECISIONE FINALE CHECKPOINT 0

**Total Checklist Items**: 50+

**✅ GO - Sistema Baseline Funzionante**:
- ☑️ ≥95% checklist items passati (48+/50)
- Zero errori critici
- Tutte funzionalità core operative
- **AZIONE**: Procedi con CHECKPOINT IMMEDIATO

**⚠️ CONDITIONAL GO - Problemi Minori**:
- ☑️ 85-94% checklist items passati (43-47/50)
- Problemi non bloccanti identificati
- **AZIONE**: Documenta issues, procedi con attenzione

**❌ NO-GO - Problemi Critici**:
- ☑️ <85% checklist items passati (<43/50)
- Servizi infrastructure down
- Funzionalità core non funzionanti
- **AZIONE**: FIX OBBLIGATORIO prima di procedere
- Re-eseguire CHECKPOINT 0 dopo fix

---

### Template Report CHECKPOINT 0

```markdown
# CHECKPOINT 0 - Baseline Verification Report

**Data**: 2025-MM-DD
**Tester**: [Nome]
**Durata**: X ore
**Environment**: Development

## Summary

**Total Checklist Items**: 50
**Passed**: XX ✅
**Failed**: XX ❌
**Skipped**: XX ⏭️

**Pass Rate**: XX%

## Infrastructure Status

| Service | Status | Notes |
|---------|--------|-------|
| PostgreSQL | ✅ UP | Version 16.x |
| Qdrant | ✅ UP | Collection count: 1 |
| Redis | ✅ UP | Memory: XX MB |
| API | ✅ UP | Health: OK |
| Web | ✅ UP | Port 3000 |
| ... | ... | ... |

## Test Results by Category

**Authentication**: X/6 tests PASS
**Core Features**: X/5 tests PASS
**Admin Features**: X/5 tests PASS
**Settings**: X/4 tests PASS
**Observability**: X/4 tests PASS
**Code Quality**: X/4 tests PASS

## Critical Issues Found

### [CRITICAL] Issue Title
- **Descrizione**: ...
- **Repro Steps**: ...
- **Impact**: MVP blocker / Minor
- **Status**: Open / Fixed
- **GitHub Issue**: #XXXX

### [MINOR] Issue Title
- **Descrizione**: ...
- **Impact**: Non-blocking
- **Status**: Documented

## Performance Observations

- Average page load time: XXms
- API response time P95: XXms
- PDF processing time: XXs
- Chat response latency: XXms

## Decision

☐ **GO** - Sistema baseline OK, procedi sviluppo
☐ **CONDITIONAL GO** - Problemi minori, monitora
☐ **NO-GO** - Fix critical issues prima

**Next Steps**: [Descrizione azioni]

---
**Approved by**: [Nome]
**Date**: [Data]
```

---

## CHECKPOINT IMMEDIATO: Critico (Week 1-2)

**Obiettivo**: Risolvere 4 issue critiche P0/P1 che bloccano lo sviluppo

**Issue Critiche**:
- #1233: SSE Error Handling (P0)
- #1193: Security & Rate Limiting (P1)
- #1255: Frontend Coverage 66% → 90% (P0)
- #1238: Web Worker Upload Tests (P1)

**Durata Test**: 4-6 ore
**Ambiente**: Development (localhost)

---

### Test C0.1: SSE Streaming Error Handling (#1233) - P0

**Prerequisiti**:
```bash
# Avvia stack completo
cd infra && docker compose up -d
cd ../apps/api/src/Api && dotnet run
cd ../../../web && pnpm dev
```

#### Scenario C0.1.1: Streaming Normale
```
1. Apri http://localhost:3000/chat
2. Login: admin@meepleai.dev / Demo123!
3. Invia domanda: "Come si gioca a Catan?"
4. ✅ VERIFICA:
   - Risposta inizia entro 2s
   - Testo appare progressivamente (streaming)
   - NO errori in console browser (F12)
   - NO errori in HyperDX (http://localhost:8180)

PASS: ☐ Streaming funziona senza errori
```

#### Scenario C0.1.2: Disconnessione Client Durante Streaming
```
1. Apri /chat, invia domanda lunga:
   "Spiegami tutte le regole di Catan in dettaglio"
2. DURANTE lo streaming (dopo 2-3s):
   - Chiudi tab browser (simula disconnessione)
3. Apri HyperDX: http://localhost:8180
4. ✅ VERIFICA:
   - Log: "Client disconnected" o "SSE connection closed"
   - NO stack trace di errori non gestiti
   - NO "Object disposed" exceptions
   - Server rimane stabile (non crash)

5. Riapri /chat, invia nuova domanda
6. ✅ VERIFICA:
   - Streaming funziona ancora
   - NO side effects da connessione precedente

PASS: ☐ Disconnessione gestita gracefully
```

#### Scenario C0.1.3: Timeout Streaming
```
1. Simula risposta lenta:
   docker compose stop meepleai-qdrant

2. Apri /chat, invia domanda: "Test timeout"
3. ✅ VERIFICA:
   - Dopo max 30s: messaggio di timeout
   - NO connessione appesa indefinitamente
   - Errore user-friendly: "La richiesta ha superato il tempo massimo"
   - NO stack trace nel UI

4. Riavvia Qdrant:
   docker compose start meepleai-qdrant

5. Retry domanda
6. ✅ VERIFICA:
   - Sistema si riprende automaticamente
   - NO restart backend necessario

PASS: ☐ Timeout gestito con fallback graceful
```

#### Scenario C0.1.4: Backend Crash Durante Streaming
```
1. Apri /chat, inizia streaming
2. DURANTE streaming (dopo 3-5s):
   - Termina backend: docker compose stop meepleai-api
   - OPPURE: Ctrl+C su terminale dotnet run

3. ✅ VERIFICA frontend:
   - Errore mostrato: "Connessione persa con il server"
   - NO "Waiting for response..." infinito
   - Pulsante "Riprova" disponibile
   - NO crash applicazione frontend

4. Riavvia backend:
   docker compose start meepleai-api

5. Click "Riprova"
6. ✅ VERIFICA:
   - Streaming riprende correttamente
   - Chat history preservata

PASS: ☐ Backend failure gestito con retry
```

**Criteri di Successo Test C0.1**:
- ✅ 4/4 scenari passano
- ✅ Zero errori non gestiti in HyperDX
- ✅ Frontend non crasha in nessuno scenario
- ✅ Recovery automatico funziona

---

### Test C0.2: Security & Rate Limiting (#1193) - P1

#### Scenario C0.2.1: Per-User Rate Limiting
```bash
# Script di test (bash)
cat > test-rate-limit.sh << 'EOF'
#!/bin/bash
SESSION_COOKIE="meepleai-session=YOUR_SESSION_COOKIE_HERE"

for i in {1..120}; do
  curl -X POST http://localhost:8080/api/v1/chat \
    -H "Content-Type: application/json" \
    -H "Cookie: $SESSION_COOKIE" \
    -d "{\"question\":\"Test $i\",\"gameId\":1}" \
    -w "\n[Request $i] Status: %{http_code}\n" \
    -s -o /dev/null
  sleep 0.5
done
EOF

chmod +x test-rate-limit.sh
```

**Esecuzione**:
```
1. Ottieni session cookie:
   - Login in browser
   - DevTools → Application → Cookies → Copia valore "meepleai-session"
   - Sostituisci YOUR_SESSION_COOKIE_HERE nello script

2. Esegui: ./test-rate-limit.sh

3. ✅ VERIFICA output:
   - Prime ~100 richieste: Status 200 (OK)
   - Successive richieste: Status 429 (Too Many Requests)
   - Header response (usa -i flag): X-RateLimit-Remaining: 0

4. Attendi 1 minuto

5. Singola richiesta:
   curl -X POST http://localhost:8080/api/v1/chat ...

6. ✅ VERIFICA:
   - Status 200 (rate limit resettato)

PASS: ☐ Per-user rate limiting attivo (100 req/min)
```

#### Scenario C0.2.2: IP-Based Rate Limiting (Unauthenticated)
```bash
for i in {1..150}; do
  curl http://localhost:8080/api/v1/games \
    -w "Status: %{http_code}\n" \
    -s -o /dev/null
done
```

**Verifica**:
```
✅ VERIFICA:
   - Prime ~100: Status 200
   - Successive: Status 429
   - Response body 429: {"error":"Rate limit exceeded. Try again later."}

PASS: ☐ IP rate limiting funziona per richieste non autenticate
```

#### Scenario C0.2.3: Session Hijacking Protection
```
1. Login utente A: admin@meepleai.dev
2. Copia session cookie da DevTools
3. Apri browser incognito
4. Tenta accesso /admin con cookie copiato manualmente
5. ✅ VERIFICA:
   - Primo accesso: Funziona (session valida)

6. Simula cambio IP (se possibile, altrimenti skip):
   - Change network
   - Riprova accesso
7. ✅ VERIFICA (se implementato IP binding):
   - Sessione invalidata se IP cambia drasticamente
   - Redirect a /login

PASS: ☐ Session security implementata
```

**Criteri di Successo Test C0.2**:
- ✅ Rate limiting per-user attivo
- ✅ Rate limiting IP attivo
- ✅ Session security funzionante
- ✅ HyperDX logs mostrano rate limit enforcement

---

### Test C0.3: Frontend Test Coverage 90%+ (#1255) - P0

#### Scenario C0.3.1: Verifica Coverage Report
```bash
cd apps/web
pnpm test:coverage
```

**Verifica**:
```
✅ VERIFICA output:
   Statements   : 90.03% (target: ≥90%)
   Branches     : 87.5%  (target: ≥85%)
   Functions    : 89.2%  (target: ≥85%)
   Lines        : 90.1%  (target: ≥90%)

✅ VERIFICA file copertura:
   - coverage/lcov-report/index.html esiste
   - Apri in browser, verifica NO file <70% coverage in critical paths

PASS: ☐ Frontend coverage ≥90%
```

#### Scenario C0.3.2: Critical Components Tested
```bash
# Verifica componenti critici hanno test
cd apps/web/__tests__

# Deve esistere:
ls -la components/
ls -la hooks/
ls -la pages/
ls -la utils/
```

**Componenti Critici Obbligatori**:
```
✅ VERIFICA file test esistono:
   - components/LoadingButton.test.tsx
   - components/ErrorBoundary.test.tsx
   - hooks/useToast.test.tsx
   - hooks/useAuth.test.tsx
   - pages/chat.test.tsx
   - pages/upload.test.tsx
   - utils/api.test.ts

PASS: ☐ Componenti critici hanno test
```

**Criteri di Successo Test C0.3**:
- ✅ Coverage complessivo ≥90%
- ✅ Tutti componenti critici testati
- ✅ CI pipeline verde (`pnpm test` passa)

---

### Test C0.4: Web Worker Upload Tests (#1238) - P1

#### Scenario C0.4.1: Unit Tests Web Worker
```bash
cd apps/web
pnpm test upload -- --coverage
```

**Verifica**:
```
✅ VERIFICA test passano:
   - Upload queue management
   - Worker communication (postMessage)
   - Progress tracking
   - Error handling

✅ VERIFICA coverage Web Worker code:
   - src/workers/uploadWorker.ts: ≥80%
   - src/hooks/useUploadQueue.tsx: ≥90%

PASS: ☐ Web Worker unit tests passano
```

#### Scenario C0.4.2: Integration Test Upload
```
1. Apri http://localhost:3000/upload
2. DevTools → Console
3. ✅ VERIFICA log: "Upload Worker initialized"

4. Seleziona 3 PDF (2-5 MB ciascuno)
5. ✅ VERIFICA:
   - 3 progress bar appaiono
   - Upload procede in parallelo
   - UI rimane responsive (digita in search box)

6. DevTools → Performance:
   - Start recording
   - Durante upload, muovi mouse, digita
   - Stop recording

7. ✅ VERIFICA:
   - NO "Long Task" warnings >50ms
   - FPS ≥30 durante upload

PASS: ☐ Web Worker upload non blocca UI
```

**Criteri di Successo Test C0.4**:
- ✅ Unit tests Web Worker passano
- ✅ Integration test upload funziona
- ✅ UI non freezes durante upload

---

### Checklist Finale CHECKPOINT IMMEDIATO

**Critiche P0/P1**:
- ☐ Test C0.1: SSE Error Handling - 4/4 scenari PASS
- ☐ Test C0.2: Security & Rate Limiting - 3/3 scenari PASS
- ☐ Test C0.3: Frontend Coverage 90% - RAGGIUNTO
- ☐ Test C0.4: Web Worker Tests - PASS

**Code Quality**:
- ☐ `dotnet test` → All pass (162+ tests)
- ☐ `pnpm test` → All pass, coverage ≥90%
- ☐ HyperDX logs: Zero errori critici

**DECISIONE**:
- ✅ **GO**: 4/4 test critici passati → Procedi a CHECKPOINT 1 (MVP Quality)
- ⚠️ **CONDITIONAL GO**: 3/4 passati → Fix issue mancante, documentare risk
- ❌ **NO-GO**: <3/4 passati → FIX OBBLIGATORIO prima di procedere

**Note per Tester**:
> Se questo checkpoint fallisce, il progetto non può procedere con Month 6 MVP tasks.
> Priorità assoluta: risolvere le 4 issue critiche.

---

## CHECKPOINT 1: MVP Quality Gate (Week 3-6)

**Obiettivo**: Completare 12 Month 6 MVP tasks per raggiungere production readiness

**Issue Month 6**:
- #1023: Phase 1A completion checklist
- #1020: Performance testing P95 <3s
- #1019: Accuracy validation 80%+
- #1018: End-to-end testing
- #1013-1017: PDF viewer, Italian i18n, game catalog
- #1012: Adversarial dataset

**Durata Test**: 8-12 ore (distribuito su 3 settimane)
**Ambiente**: Development + Staging

---

### Test MVP.1: Accuracy Validation (#1019) - BLOCKER

**Obiettivo**: ≥80% accuracy su golden dataset (target: 95%)

#### Scenario MVP.1.1: Golden Dataset Evaluation
```bash
cd apps/api/src/Api
# Esegui evaluation tool (assumendo esista)
dotnet run --project ../Tools/EvaluateAccuracy -- \
  --dataset golden-dataset-100.json \
  --output accuracy-report.json
```

**Verifica**:
```
✅ VERIFICA output:
   Evaluating 100 Q&A pairs...
   [1/100] ✓ "Quanti giocatori per Catan?" → "3-4 giocatori" (CORRECT)
   [2/100] ✓ "Come si vince a Carcassonne?" → "Massimo punti" (CORRECT)
   [3/100] ✗ "Durata Pandemic?" → "Non so" (EXPECTED: "45 min")
   ...
   [100/100] Done

   === FINAL RESULTS ===
   Accuracy: 82/100 (82%)  ← DEVE essere ≥80%
   Precision: 0.85
   Recall: 0.78
   F1 Score: 0.81
   Hallucination Rate: 7%   ← DEVE essere ≤10%

✅ CRITERIO:
   - Accuracy ≥80%: ✅ PASS
   - Accuracy <80%: ❌ FAIL → CHECKPOINT BLOCCATO

PASS: ☐ Accuracy ≥80% RAGGIUNTA
```

#### Scenario MVP.1.2: Manual Spot Check (10 Domande)
```
Test manualmente 10 domande dal golden dataset:

1. "Quanti giocatori può avere Catan?"
   ✅ Risposta attesa: "3-4 giocatori (o 3-6 con espansione)"
   ☐ PASS | ☐ FAIL

2. "Come si costruisce una strada a Catan?"
   ✅ Risposta attesa: "1 legno + 1 argilla"
   ☐ PASS | ☐ FAIL

3. "Quanto dura una partita a 7 Wonders?"
   ✅ Risposta attesa: "~30 minuti"
   ☐ PASS | ☐ FAIL

4. "Qual è l'obiettivo di Pandemic?"
   ✅ Risposta attesa: "Curare 4 malattie cooperativamente"
   ☐ PASS | ☐ FAIL

5. "Come si gioca a Monopoly?" (NON nel dataset)
   ✅ Risposta attesa: "Non ho informazioni su Monopoly nel database"
   ⚠️ NO invenzioni/hallucinations
   ☐ PASS | ☐ FAIL

6-10. [Continua con altre 5 domande dal golden dataset]

✅ CRITERIO:
   - 9-10 corrette: ✅ EXCELLENT
   - 8 corrette: ✅ PASS
   - <8 corrette: ❌ FAIL

PASS: ☐ 8+/10 risposte accurate manualmente
```

**Criteri di Successo MVP.1**:
- ✅ Accuracy automatica ≥80%
- ✅ Manual spot check ≥80%
- ✅ Hallucination rate ≤10%
- ✅ Report accuracy generato e documentato

---

### Test MVP.2: Performance Testing (#1020) - BLOCKER

**Obiettivo**: P95 latency <3s per RAG queries

#### Scenario MVP.2.1: Load Test con k6
```bash
# Installa k6: https://k6.io/docs/get-started/installation/
# macOS: brew install k6
# Linux: sudo snap install k6

cat > load-test-bgai.js << 'EOF'
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 50 },  // Ramp up to 50 users
    { duration: '5m', target: 50 },  // Stay at 50 for 5 mins
    { duration: '2m', target: 0 },   // Ramp down to 0
  ],
  thresholds: {
    http_req_duration: ['p(95)<3000'], // P95 <3s CRITICAL
    http_req_failed: ['rate<0.05'],    // <5% error rate
  },
};

export default function () {
  const payload = JSON.stringify({
    question: 'Come si gioca a Catan?',
    gameId: 1,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      // Add session cookie if needed
    },
  };

  const res = http.post('http://localhost:8080/api/v1/chat', payload, params);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time <3s': (r) => r.timings.duration < 3000,
    'has content': (r) => r.body.length > 0,
  });

  sleep(1); // Pausa 1s tra richieste
}
EOF
```

**Esecuzione**:
```bash
k6 run load-test-bgai.js
```

**Verifica Output**:
```
✅ VERIFICA metriche finali:

     ✓ http_req_duration............: avg=1250ms p(95)=2450ms max=4200ms
       ✓ P95 <3s: PASS ✅

     ✓ http_reqs....................: 15000 (50 req/s)

     ✓ checks.......................: 95.2% (14280/15000)
       ✓ >90% checks pass: PASS ✅

     ✓ http_req_failed..............: 0.8% (120/15000)
       ✓ <5% error rate: PASS ✅

     iterations.....................: 15000 (50/s)
     vus............................: 50
     vus_max........................: 50

✅ CRITERI:
   - P95 latency <3s: ✅ PASS
   - Error rate <5%: ✅ PASS
   - Throughput ≥30 req/s: ✅ PASS

PASS: ☐ Performance requirements raggiunto
```

#### Scenario MVP.2.2: Profiling Bottlenecks
```bash
# Se P95 >3s, eseguire profiling
cd apps/api/src/Api

# Con dotTrace (se disponibile):
dotnet run --no-build --configuration Release

# Durante load test k6, catturare trace
# Analizzare bottlenecks in:
# - Database queries
# - LLM API calls
# - Vector search (Qdrant)
```

**Verifica Bottlenecks Comuni**:
```
✅ VERIFICA tempi componenti (da HyperDX logs):
   - PostgreSQL query: <50ms
   - Qdrant vector search: <200ms
   - LLM API call: <1500ms
   - Total pipeline: <2500ms (P95)

☐ BOTTLENECK IDENTIFICATO: _________________
☐ OTTIMIZZAZIONE APPLICATA: _________________
```

**Criteri di Successo MVP.2**:
- ✅ P95 <3s con 50 concurrent users
- ✅ Error rate <5%
- ✅ Bottlenecks documentati (se presenti)
- ✅ Report performance salvato

---

### Test MVP.3: End-to-End Testing (#1018)

**Obiettivo**: Validare full flow: PDF upload → Processing → Chat → Citation

#### Scenario MVP.3.1: Fresh Database E2E
```bash
# Reset database completo
cd apps/api/src/Api
dotnet ef database drop --force
dotnet ef database update

# Seed demo users
dotnet run --seed-demo-users
```

**Flow Completo**:
```
1. Login: admin@meepleai.dev / Demo123!

2. Upload PDF:
   - Navigate /upload
   - Seleziona: docs/test-pdfs/catan-it.pdf (o PDF regole esistente)
   - ✅ VERIFICA: Upload success, progress 100%

3. Attendi processing:
   - Navigate /admin/documents
   - Status: "Queued" → "Processing" → "Completed" (max 60s)
   - ✅ VERIFICA:
     - Quality score ≥0.70
     - Pages count corretto (es. 12 pages)
     - Preview text visibile

4. Chat RAG:
   - Navigate /chat
   - Seleziona game: "Catan"
   - Invia domanda: "Come si costruisce una città a Catan?"

5. ✅ VERIFICA risposta:
   - Risposta corretta: "2 grano + 3 minerale" (o equivalente)
   - Citation presente: [Rulebook p.8] (pagina può variare)
   - Confidence score ≥0.70
   - Streaming funziona

6. Click citation link:
   - ✅ VERIFICA:
     - PDF viewer modal apre
     - Auto-scroll a pagina citata
     - Testo evidenziato (highlight)

7. Follow-up question (context):
   - Invia: "E quanto costa?"
   - ✅ VERIFICA:
     - Risposta contestuale: "Una città costa 2 grano e 3 minerale"
     - Context mantenuto da domanda precedente

PASS: ☐ E2E flow completo funziona
```

**Criteri di Successo MVP.3**:
- ✅ Upload → Processing pipeline funziona
- ✅ RAG query con citations accurate
- ✅ PDF viewer + jump to page funziona
- ✅ Multi-turn context preservato

---

### Test MVP.4: PDF Viewer Integration (#1013, #1014, #1015)

#### Scenario MVP.4.1: PDF Viewer UI (react-pdf)
```
1. Navigate /chat
2. Invia domanda con citation attesa
3. Click citation link [Rulebook p.X]

✅ VERIFICA PDF Viewer:
   - Modal apre con PDF rendering
   - Zoom controls funzionanti (+, -, fit)
   - Page navigation (arrows, input direct page)
   - Auto-scroll a pagina citata
   - Highlight testo rilevante (giallo/verde)
   - Close button funziona

PASS: ☐ PDF viewer UI funzionale
```

#### Scenario MVP.4.2: Playwright E2E Test
```bash
cd apps/web
pnpm test:e2e pdf-viewer.spec.ts
```

**Verifica**:
```
✅ VERIFICA test Playwright:
   - PDF viewer opens on citation click
   - Navigation works (next/prev page)
   - Zoom functionality works
   - Highlight visible on cited page
   - Modal closes properly

PASS: ☐ Playwright PDF viewer tests passano
```

**Criteri di Successo MVP.4**:
- ✅ PDF viewer rendering OK
- ✅ Navigation & zoom funzionano
- ✅ Playwright E2E tests passano

---

### Test MVP.5: Italian i18n (#1016)

#### Scenario MVP.5.1: UI Strings Coverage
```bash
cd apps/web
# Verifica file traduzioni
cat src/locales/it.json | jq '. | length'
```

**Verifica**:
```
✅ VERIFICA:
   - File it.json esiste
   - Minimo 200+ chiavi tradotte
   - NO chiavi vuote o "TODO"

✅ TEST manuale pagine:
   - /login: Tutti i testi in italiano
   - /chat: UI completamente italiana
   - /upload: Labels e messaggi in italiano
   - /settings: 4 tabs tradotte
   - /admin: Dashboard in italiano

PASS: ☐ 200+ stringhe tradotte, UI italiana completa
```

#### Scenario MVP.5.2: Language Switcher
```
1. Navigate /settings → Preferences
2. Language dropdown: Seleziona "English"
3. ✅ VERIFICA: UI passa a inglese
4. Seleziona "Italiano"
5. ✅ VERIFICA: UI torna a italiano
6. Refresh pagina
7. ✅ VERIFICA: Lingua persistita (localStorage)

PASS: ☐ Language switcher funziona
```

**Criteri di Successo MVP.5**:
- ✅ 200+ stringhe italiane
- ✅ Tutte pagine principali tradotte
- ✅ Language switcher persiste scelta

---

### Test MVP.6: Game Catalog Page (#1017)

#### Scenario MVP.6.1: Game List UI
```
1. Navigate /games

✅ VERIFICA UI:
   - Lista games mostrata (grid o table)
   - Ogni game card mostra:
     - Nome gioco
     - Numero giocatori
     - Durata partita
     - Complessità
     - Immagine cover (se disponibile)
   - Search/filter funzionante
   - Pulsante "Add Game" (se admin)

PASS: ☐ Game catalog UI completa
```

#### Scenario MVP.6.2: Game CRUD Operations
```
1. Login admin: admin@meepleai.dev
2. Click "Add Game"
3. Compila form:
   - Name: "7 Wonders"
   - Players: "2-7"
   - Duration: "30 min"
   - Complexity: "Medium"
4. Submit

✅ VERIFICA:
   - Game creato
   - Appare in lista
   - Success toast mostrato

5. Click game → Edit
6. Modifica Duration: "30-45 min"
7. Save

✅ VERIFICA:
   - Update salvato
   - Lista aggiornata

8. Delete game
✅ VERIFICA: Rimosso da lista

PASS: ☐ CRUD operations funzionano
```

**Criteri di Successo MVP.6**:
- ✅ Game catalog page responsive
- ✅ CRUD operations complete
- ✅ Search/filter funzionanti

---

### Test MVP.7: Adversarial Dataset (#1012)

#### Scenario MVP.7.1: Synthetic Queries Evaluation
```bash
cd apps/api/src/Api
# Esegui evaluation su adversarial dataset
dotnet run --project ../Tools/EvaluateAccuracy -- \
  --dataset adversarial-dataset-50.json \
  --output adversarial-report.json
```

**Verifica**:
```
Adversarial dataset contiene:
- Domande ambigue: "Come si gioca?" (senza specificare gioco)
- Domande fuori dominio: "Chi ha vinto il campionato?"
- Domande con errori ortografici: "Quanti giucatori Catan?"
- Domande in altre lingue: "How many players for Catan?"

✅ VERIFICA risposte:
   - Domande ambigue → "Per favore specifica quale gioco"
   - Fuori dominio → "Non ho informazioni su questo argomento"
   - Errori ortografici → Risposta corretta (tolleranza errori)
   - Altre lingue → Risposta corretta o "Supporto solo italiano"

✅ CRITERIO:
   - Accuracy adversarial ≥70%
   - Zero hallucinations su domande fuori dominio

PASS: ☐ Adversarial dataset accuracy ≥70%
```

**Criteri di Successo MVP.7**:
- ✅ 50 synthetic queries testate
- ✅ Accuracy ≥70% su adversarial
- ✅ Zero hallucinations

---

### Test MVP.8: Documentation Updates (#1022)

#### Scenario MVP.8.1: User Guide
```bash
# Verifica esistenza documentazione
cd docs

✅ VERIFICA file esistono:
   - docs/user-guide/getting-started.md
   - docs/user-guide/uploading-pdfs.md
   - docs/user-guide/asking-questions.md
   - docs/user-guide/managing-games.md

✅ VERIFICA contenuto:
   - Screenshots/diagrammi presenti
   - Passi chiari e numerati
   - Troubleshooting section
   - FAQ section

PASS: ☐ User guide completa
```

#### Scenario MVP.8.2: README Aggiornato
```bash
cat README.md
```

**Verifica**:
```
✅ VERIFICA README contiene:
   - Descrizione progetto aggiornata
   - Feature list (DDD, Frontend modernization, BGAI)
   - Quick start guide
   - Link a docs completa
   - Badge CI/coverage aggiornati
   - Contribuire section

PASS: ☐ README aggiornato
```

**Criteri di Successo MVP.8**:
- ✅ User guide completa (4+ guide)
- ✅ README aggiornato con feature recenti
- ✅ API docs aggiornate (se modificate)

---

### Test MVP.9: Phase 1A Completion Checklist (#1023)

**Checklist Completa**:
```
✅ DDD Migration
   ☐ 7/7 bounded contexts migrated
   ☐ 96+ CQRS handlers operational
   ☐ 5,387 lines legacy code removed
   ☐ Zero build errors

✅ Frontend Modernization
   ☐ 9/9 FE-IMP issues completate
   ☐ App Router + Providers
   ☐ TanStack Query caching
   ☐ Zustand state management
   ☐ React Hook Form + Zod
   ☐ Web Worker upload queue

✅ BGAI Validation Foundation
   ☐ 4/4 validation foundation completate
   ☐ MultiModelValidationService operational
   ☐ Consensus similarity TF-IDF
   ☐ Unit tests coverage >95% on validation

✅ Critical Issues Resolved
   ☐ #1233 SSE error handling: FIXED
   ☐ #1193 Security & rate limiting: FIXED
   ☐ #1255 Frontend coverage 90%+: FIXED
   ☐ #1238 Web Worker tests: FIXED

✅ MVP Quality Gate
   ☐ #1019 Accuracy ≥80%: VALIDATED
   ☐ #1020 Performance P95 <3s: VALIDATED
   ☐ #1018 E2E testing: PASSING
   ☐ #1013-1015 PDF viewer: FUNCTIONAL
   ☐ #1016 Italian i18n 200+: COMPLETE
   ☐ #1017 Game catalog: FUNCTIONAL
   ☐ #1012 Adversarial dataset: VALIDATED
   ☐ #1022 Documentation: UPDATED

✅ Code Quality
   ☐ Backend test coverage ≥90%
   ☐ Frontend test coverage ≥90%
   ☐ CI pipeline green
   ☐ Zero critical bugs in HyperDX logs

✅ Deployment Readiness
   ☐ Health checks operational
   ☐ Monitoring/alerting configured
   ☐ Database migrations automated
   ☐ Rollback plan tested
```

**Criteri di Successo MVP.9**:
- ✅ 100% checklist completata
- ✅ Tutte le issue Month 6 MVP chiuse
- ✅ Sistema pronto per production

---

### Checklist Finale CHECKPOINT 1 - MVP Quality Gate

**MVP Blockers (CRITICAL)**:
- ☐ MVP.1: Accuracy ≥80% - RAGGIUNTO
- ☐ MVP.2: Performance P95 <3s - RAGGIUNTO
- ☐ MVP.3: E2E testing - PASSING

**MVP Features**:
- ☐ MVP.4: PDF viewer - FUNCTIONAL
- ☐ MVP.5: Italian i18n - COMPLETE
- ☐ MVP.6: Game catalog - FUNCTIONAL
- ☐ MVP.7: Adversarial dataset - VALIDATED
- ☐ MVP.8: Documentation - UPDATED
- ☐ MVP.9: Phase 1A checklist - 100% DONE

**Code Quality**:
- ☐ `dotnet test` → All pass (162+ tests)
- ☐ `pnpm test` → All pass, coverage ≥90%
- ☐ `pnpm test:e2e` → All critical paths pass
- ☐ HyperDX logs: Zero critical errors (48h window)

**DECISIONE FINALE**:
- ✅ **GO TO PRODUCTION**: Tutti MVP blockers + 80%+ features complete
- ⚠️ **CONDITIONAL GO**: MVP blockers OK + 60-79% features → Launch beta
- ❌ **NO-GO**: MVP blockers failing → Delay 1-2 settimane, fix critical

**Report Template**:
```markdown
# CHECKPOINT 1 Report - MVP Quality Gate

**Data**: 2025-MM-DD
**Tester**: [Nome]
**Durata**: XX ore

## Results

**MVP Blockers**:
- Accuracy: XX% (target ≥80%) → ✅ PASS / ❌ FAIL
- Performance P95: XXms (target <3000ms) → ✅ PASS / ❌ FAIL
- E2E Testing: XX/XX tests pass → ✅ PASS / ❌ FAIL

**Features Completate**: XX/7 (XX%)

**Issues Found**:
1. [CRITICAL] Description
2. [MINOR] Description

## Decision
☐ GO TO PRODUCTION
☐ CONDITIONAL GO (beta launch)
☐ NO-GO (fix and retest)

**Next Steps**: [Descrizione]

---
Approved by: [Nome]
Date: [Data]
```

---

## CHECKPOINT 2: Phase 1B Integration (Week 7-9)

**Obiettivo**: Integrare 6 issue Phase 1B per completare Multi-Model Validation

**Issue Phase 1B**:
- #977: Wire all 5 validation layers
- #979: Performance optimization (parallel validation)
- #978: E2E testing validation pipeline
- #981: Accuracy baseline measurement
- #980: Bug fixes validation edge cases
- #982: Update ADRs

**Durata Test**: 6-8 ore
**Ambiente**: Development + Staging

---

### Test 1B.1: Wire All 5 Validation Layers (#977)

**Obiettivo**: Verificare integrazione completa delle 5 validation layers nel RAG pipeline

#### Scenario 1B.1.1: Validation Layers Active
```
1. Apri /chat
2. Invia domanda: "Come si vince a Catan?"

3. Apri HyperDX: http://localhost:8180
4. Filtra log: @Message contains "Validation"

✅ VERIFICA log sequenza:
   - "Layer 1: Citation verification - PASS"
   - "Layer 2: Confidence threshold (0.XX) - PASS"
   - "Layer 3: Forbidden keywords check - PASS"
   - "Layer 4: Multi-model consensus (similarity 0.XX) - PASS"
   - "Layer 5: Ambiguity detection - PASS"
   - "All 5 validation layers passed"

✅ VERIFICA risposta contiene:
   - Citation: [Rulebook p.X]
   - Confidence score ≥0.70
   - NO forbidden keywords (es. "I think", "maybe")
   - Risposta coerente (consensus tra modelli)

PASS: ☐ 5/5 validation layers operative
```

#### Scenario 1B.1.2: Validation Failure Scenarios
```
Test manuale failure scenarios:

1. Domanda senza risposta nel DB:
   "Come si gioca a Monopoly?" (non caricato)

   ✅ VERIFICA:
   - Layer 1 (Citation): FAIL (no sources)
   - Risposta: "Non ho informazioni su Monopoly"
   - NO hallucination

2. Domanda ambigua:
   "Come si vince?" (quale gioco?)

   ✅ VERIFICA:
   - Layer 5 (Ambiguity): FAIL
   - Risposta: "Per favore specifica a quale gioco ti riferisci"

3. Low confidence scenario (simula rimuovendo parte del DB):
   [Setup: Rimuovi metà chunks del PDF Catan]
   "Dettagli costruzione città Catan?"

   ✅ VERIFICA:
   - Layer 2 (Confidence): FAIL (<0.70)
   - Risposta: "Non sono abbastanza sicuro, consulta il manuale"

PASS: ☐ Validation failures gestiti correttamente
```

**Criteri di Successo 1B.1**:
- ✅ 5/5 layers operative e loggano correttamente
- ✅ Happy path: tutte layers PASS
- ✅ Failure scenarios: fallback graceful

---

### Test 1B.2: Performance Optimization Parallel Validation (#979)

**Obiettivo**: Validazione parallela riduce P95 a <2s (miglioramento da <3s)

#### Scenario 1B.2.1: Load Test Ottimizzato
```bash
# Stesso load test di MVP.2 ma con target migliorato
k6 run load-test-bgai.js
```

**Verifica**:
```
✅ VERIFICA metriche (con parallel validation):

     ✓ http_req_duration............: avg=950ms p(95)=1850ms max=2800ms
       ✓ P95 <2s: PASS ✅ (miglioramento da 2450ms → 1850ms = 24%)

     ✓ http_reqs....................: 15000 (50 req/s)

✅ VERIFICA HyperDX logs (during load test):
   - "Parallel validation: GPT-4 + Claude" (simultaneous)
   - "Validation completed in XXms"
   - Tempo validation parallela <500ms (vs >1000ms sequenziale)

PASS: ☐ Performance improvement ≥20% con parallel validation
```

**Criteri di Successo 1B.2**:
- ✅ P95 <2s (miglioramento da <3s)
- ✅ Parallel calls a GPT-4 + Claude simultanei
- ✅ Latency reduction ≥20%

---

### Test 1B.3: E2E Testing Validation Pipeline (#978)

**Obiettivo**: Test automatizzati end-to-end per validation pipeline

#### Scenario 1B.3.1: Automated E2E Tests
```bash
cd apps/api/tests/Api.Tests.Integration
dotnet test --filter "Category=ValidationE2E"
```

**Verifica**:
```
✅ VERIFICA test suite:
   - ValidationPipeline_WithValidCitation_PassesLayer1
   - ValidationPipeline_WithHighConfidence_PassesLayer2
   - ValidationPipeline_WithNoForbiddenKeywords_PassesLayer3
   - ValidationPipeline_WithConsensus_PassesLayer4
   - ValidationPipeline_WithClearQuestion_PassesLayer5
   - ValidationPipeline_FullFlow_AllLayersPass

   Running 50+ E2E validation scenarios...
   ✅ All tests passed: 50/50

PASS: ☐ E2E validation tests passano
```

**Criteri di Successo 1B.3**:
- ✅ 50+ E2E test scenarios passing
- ✅ All 5 layers tested end-to-end
- ✅ Regression suite operativa

---

### Test 1B.4: Accuracy Baseline Measurement (#981)

**Obiettivo**: Documentare accuracy baseline post-integration

#### Scenario 1B.4.1: Accuracy Measurement
```bash
cd apps/api/src/Api
dotnet run --project ../Tools/EvaluateAccuracy -- \
  --dataset golden-dataset-100.json \
  --output baseline-phase1b.json
```

**Verifica**:
```
✅ VERIFICA risultati:
   Accuracy: XX/100 (XX%)

   Confronto baseline:
   - Pre Phase 1B (MVP.1): 82%
   - Post Phase 1B: XX% (target: 85-90%)

   Miglioramento: +X%

✅ CRITERIO:
   - Accuracy ≥85%: ✅ EXCELLENT
   - Accuracy 80-84%: ✅ PASS
   - Accuracy <80%: ❌ REGRESSION → Investigate

PASS: ☐ Accuracy ≥80% mantenuta post-integration
```

**Criteri di Successo 1B.4**:
- ✅ Accuracy baseline documentata
- ✅ NO regression da MVP.1
- ✅ Idealmente miglioramento +3-5%

---

### Test 1B.5: Bug Fixes Validation Edge Cases (#980)

**Obiettivo**: Verificare fix per edge cases validation

#### Scenario 1B.5.1: Edge Case Coverage
```
Test edge cases documentati:

1. Domanda multi-lingua (mix italiano-inglese):
   "Quanti players per Catan?"
   ✅ VERIFICA: Risposta corretta "3-4 giocatori"

2. Typo tolerance:
   "Come si vince a Carcassone?" (1 's' invece di 2)
   ✅ VERIFICA: Risposta corretta (fuzzy match)

3. Domanda molto lunga (>500 chars):
   [Insert long question]
   ✅ VERIFICA: NO crash, risposta entro timeout

4. Caratteri speciali:
   "Cos'è il 'meeple' in Carcassonne?"
   ✅ VERIFICA: Parsing corretto, risposta accurata

5. Domanda con numeri:
   "Cosa succede al turno 10 in Pandemic?"
   ✅ VERIFICA: Interpreta numeri correttamente

PASS: ☐ 5/5 edge cases gestiti
```

**Criteri di Successo 1B.5**:
- ✅ Edge cases noti risolti
- ✅ NO crash su input anomali
- ✅ Known issues documentati in docs/

---

### Test 1B.6: Update ADRs (#982)

**Obiettivo**: Documentare decisioni architetturali validation

#### Scenario 1B.6.1: ADR Completeness
```bash
cd docs/01-architecture/adr/

✅ VERIFICA file ADR esistono:
   - adr-005-multi-model-validation.md
   - adr-006-validation-pipeline-architecture.md

✅ VERIFICA contenuto ADR:
   - Context: Perché multi-model validation
   - Decision: TF-IDF cosine similarity ≥0.90
   - Consequences: +24% latency overhead acceptable
   - Alternatives considered: Simple majority voting
   - Status: Accepted
   - Date: 2025-11-XX

PASS: ☐ ADR completi e aggiornati
```

**Criteri di Successo 1B.6**:
- ✅ 2 ADR creati/aggiornati
- ✅ Lessons learned documentati
- ✅ Future migration path chiaro

---

### Checklist Finale CHECKPOINT 2 - Phase 1B Integration

**Integration Tests**:
- ☐ 1B.1: 5 validation layers wired - OPERATIONAL
- ☐ 1B.2: Performance optimization - P95 <2s
- ☐ 1B.3: E2E validation tests - 50+ PASSING

**Quality Validation**:
- ☐ 1B.4: Accuracy baseline - ≥80% maintained
- ☐ 1B.5: Edge cases - FIXED
- ☐ 1B.6: ADR documentation - COMPLETE

**Code Quality**:
- ☐ Integration test suite passing
- ☐ HyperDX logs clean (no errors during tests)
- ☐ Regression suite operational

**DECISIONE**:
- ✅ **GO TO CHECKPOINT 3**: All integration tests pass
- ⚠️ **CONDITIONAL GO**: 5/6 pass → Fix and retest
- ❌ **NO-GO**: <5/6 pass → Investigate failures

---

## CHECKPOINT 3: Production Readiness (Week 10-12)

**Obiettivo**: Final polish e preparazione deployment production

**Focus Areas**:
- Security audit
- Staging smoke tests
- Deployment automation
- Monitoring/alerting
- Rollback plan

**Durata Test**: 4-6 ore
**Ambiente**: Staging → Production

---

### Test PROD.1: Security Audit

#### Scenario PROD.1.1: OWASP ZAP Scan
```bash
# Avvia ZAP proxy
docker run -t owasp/zap2docker-stable zap-baseline.py \
  -t http://staging.meepleai.dev \
  -r zap-report.html
```

**Verifica**:
```
✅ VERIFICA report ZAP:
   - High severity: 0
   - Medium severity: <5 (acceptable)
   - Low severity: <20

✅ OWASP Top 10 coverage:
   - A01 Broken Access Control: ✅ PASS
   - A02 Cryptographic Failures: ✅ PASS
   - A03 Injection: ✅ PASS
   - A07 XSS: ✅ PASS
   - ... (full list)

PASS: ☐ Zero high/critical vulnerabilities
```

#### Scenario PROD.1.2: Dependency Audit
```bash
# Backend
cd apps/api
dotnet list package --vulnerable

# Frontend
cd apps/web
pnpm audit --audit-level=high
```

**Verifica**:
```
✅ VERIFICA:
   - Backend: 0 vulnerable packages
   - Frontend: 0 high/critical vulnerabilities

PASS: ☐ Zero vulnerable dependencies
```

**Criteri di Successo PROD.1**:
- ✅ Security scan clean
- ✅ Dependencies audit passed
- ✅ Security headers A+ grade

---

### Test PROD.2: Staging Smoke Tests

**Smoke Test Suite** (run on staging.meepleai.dev):
```
1. Health check: GET /health → 200 OK
2. Login: admin credentials → Success
3. Upload PDF: 1 sample file → Processing complete
4. Chat query: 1 test question → Response received
5. Admin dashboard: Load stats → Data visible
6. API key auth: 1 API call → 200 OK

✅ VERIFICA:
   - 6/6 smoke tests pass
   - Avg response time <2s
   - Zero errors in application logs

PASS: ☐ Staging smoke tests 100% pass
```

**Criteri di Successo PROD.2**:
- ✅ All smoke tests pass on staging
- ✅ Logs clean (48h window)
- ✅ Performance acceptable

---

### Test PROD.3: Deployment Automation

#### Scenario PROD.3.1: Blue-Green Deployment
```bash
# Test deployment script
./deploy.sh staging --blue-green

✅ VERIFICA:
   - Blue environment: Current production
   - Green environment: New version deployed
   - Health check green: PASS
   - Traffic switch: Blue → Green
   - Zero downtime: Verified
   - Rollback ready: Blue environment preserved

PASS: ☐ Blue-green deployment funziona
```

**Criteri di Successo PROD.3**:
- ✅ Zero-downtime deployment tested
- ✅ Rollback plan validated
- ✅ Deployment <5 min

---

### Test PROD.4: Monitoring & Alerting

#### Scenario PROD.4.1: Grafana Dashboards
```
1. Apri Grafana: http://grafana.meepleai.dev
2. ✅ VERIFICA dashboards esistono:
   - API Performance Dashboard
   - RAG Quality Dashboard
   - Infrastructure Health
   - Business Metrics

3. ✅ VERIFICA panels:
   - Request rate, latency (P50, P95, P99)
   - Error rate
   - Accuracy score (time series)
   - Database connections
   - Cache hit ratio

PASS: ☐ Dashboards completi e funzionali
```

#### Scenario PROD.4.2: Alert Rules
```
✅ VERIFICA alert rules configurate:
   - P95 latency >3s → PagerDuty
   - Error rate >5% → Slack
   - Accuracy drops <75% → Email
   - Database down → PagerDuty
   - Disk usage >80% → Email

✅ TEST alert (simulato):
   - Trigger test alert
   - Verify notification received

PASS: ☐ Alert rules operative
```

**Criteri di Successo PROD.4**:
- ✅ Grafana dashboards complete
- ✅ Alert rules configured
- ✅ Notifications tested

---

### Checklist Finale CHECKPOINT 3 - Production Readiness

**Pre-Production**:
- ☐ PROD.1: Security audit - CLEAN
- ☐ PROD.2: Staging smoke tests - 100% PASS
- ☐ PROD.3: Deployment automation - TESTED
- ☐ PROD.4: Monitoring/alerting - OPERATIONAL

**Production Deployment**:
- ☐ Database backup created
- ☐ `./deploy.sh production` executed
- ☐ Health check /health → 200 OK
- ☐ Sample query tested → Works

**Post-Deployment (48h monitoring)**:
- ☐ Sentry: Zero critical errors
- ☐ HyperDX: Normal log patterns
- ☐ Grafana: All metrics green
- ☐ User feedback: No blockers reported

**DECISIONE FINALE**:
- ✅ **RELEASE APPROVED** 🎉: All checks pass
- ❌ **ROLLBACK**: Critical issues detected

---

## Appendice: Scenari di Test Dettagliati

### A.1: Test Matrix Accuracy

| Game | Question | Expected Answer | Accuracy Threshold |
|------|----------|-----------------|-------------------|
| Catan | Quanti giocatori? | 3-4 (base), 3-6 (exp) | EXACT |
| Catan | Come si vince? | 10 punti vittoria | EXACT |
| Pandemic | Obiettivo? | Curare 4 malattie | SEMANTIC |
| 7 Wonders | Durata? | ~30 minuti | RANGE |
| Carcassonne | Cos'è un meeple? | Pezzo giocatore | SEMANTIC |

### A.2: Performance Benchmarks

| Scenario | P50 Target | P95 Target | P99 Target |
|----------|-----------|-----------|-----------|
| Simple query (cached) | <500ms | <1s | <2s |
| Complex query (RAG) | <1s | <3s | <5s |
| PDF upload (10MB) | <3s | <10s | <20s |
| PDF processing | <30s | <60s | <120s |

### A.3: Error Messages Standard

| Error Type | User-Facing Message (IT) | Log Level |
|-----------|--------------------------|-----------|
| Low confidence | "Non sono sicuro, consulta il manuale" | INFO |
| No sources | "Non ho informazioni su questo argomento" | INFO |
| Timeout | "La richiesta ha richiesto troppo tempo" | WARN |
| Server error | "Si è verificato un errore, riprova" | ERROR |
| Hallucination detected | [Blocked, fallback to safe response] | WARN |

---

**Fine Guida Tester**

**Versione**: 2.0
**Prossimo Update**: Post Checkpoint 1 completion
**Owner**: QA Team & Engineering Lead

**Feedback**: Segnalare miglioramenti o scenari mancanti a engineering@meepleai.dev

