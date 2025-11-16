# 🧪 Guida Tester - Checkpoint di Verifica Manuale
**MeepleAI Monorepo - Testing Guide per Checkpoint**

**Versione**: 1.0
**Data**: 2025-11-16
**Owner**: QA Team

---

## 📋 Indice

- [Checkpoint 0: Baseline Verification](#checkpoint-0-baseline-verification)
- [Checkpoint 1: Critical Fixes & Security](#checkpoint-1-critical-fixes--security)
- [Checkpoint 2: Frontend Modernization](#checkpoint-2-frontend-modernization)
- [Checkpoint 3: BGAI Quality Gate](#checkpoint-3-bgai-quality-gate)
- [Checkpoint 4: Final Release](#checkpoint-4-final-release)

---

## CHECKPOINT 0: Baseline Verification

**Timeline**: PRIMA di qualsiasi modifica al codice
**Durata Test**: ~4-6 ore (test completo di tutte le funzionalità)
**Obiettivo**: Verificare che il sistema esistente sia completamente funzionante

⚠️ **CRITICO**: Questo checkpoint deve essere eseguito PRIMA di iniziare qualsiasi sviluppo.
Serve come baseline di riferimento per identificare regressioni future.

### Pre-requisiti

```bash
# 1. Clone repository (se non fatto)
git clone https://github.com/DegrassiAaron/meepleai-monorepo
cd meepleai-monorepo

# 2. Verifica branch
git checkout main
git pull origin main

# 3. Setup environment
cp infra/env/.env.dev infra/env/.env

# 4. Avvia TUTTI i servizi
cd infra
docker compose up -d

# 5. Verifica tutti i servizi sono UP
docker compose ps

# Output atteso (15 servizi):
# postgres        Up      5432/tcp
# qdrant          Up      6333/tcp, 6334/tcp
# redis           Up      6379/tcp
# ollama          Up      11434/tcp
# embedding       Up      8000/tcp
# unstructured    Up      8001/tcp
# smoldocling     Up      8002/tcp
# seq             Up      8081/tcp
# jaeger          Up      16686/tcp, 14268/tcp
# prometheus      Up      9090/tcp
# alertmanager    Up      9093/tcp
# grafana         Up      3001/tcp
# n8n             Up      5678/tcp
# api             Up      8080/tcp
# web             Up      3000/tcp

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

### Test B0.1: Verifica Servizi Infrastructure

**Obiettivo**: Tutti i 15 servizi Docker devono essere healthy

#### Scenario B0.1.1: Health Checks
```
1. Verifica PostgreSQL:
   docker exec -it postgres psql -U meepleai -d meepleai -c "SELECT version();"
   ✅ VERIFICA: Output mostra PostgreSQL version

2. Verifica Qdrant:
   curl http://localhost:6333/healthz
   ✅ VERIFICA: Response "OK"

3. Verifica Redis:
   docker exec -it redis redis-cli PING
   ✅ VERIFICA: Response "PONG"

4. Verifica Seq (logs):
   curl http://localhost:8081/api
   ✅ VERIFICA: HTTP 200

5. Verifica Jaeger (tracing):
   curl http://localhost:16686
   ✅ VERIFICA: HTML page loads

6. Verifica Prometheus:
   curl http://localhost:9090/-/healthy
   ✅ VERIFICA: Response "Prometheus is Healthy"

7. Verifica Grafana:
   curl http://localhost:3001/api/health
   ✅ VERIFICA: {"commit":"...","database":"ok","version":"..."}

8. Verifica n8n:
   curl http://localhost:5678/healthz
   ✅ VERIFICA: {"status":"ok"}

PASS: ☐ Tutti i servizi infrastruttura sono healthy
```

---

### Test B0.2: Authentication - Completa

**Obiettivo**: Verificare tutte le modalità di autenticazione

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
     - Cookie ha flags: HttpOnly=true, Secure=true (se HTTPS), SameSite=Lax

5. Verifica sessione persistente:
   - Refresh pagina (F5)
   ✅ VERIFICA: Rimani autenticato (no redirect a /login)

6. Logout:
   - Click su pulsante Logout
   ✅ VERIFICA:
     - Redirect a /login
     - Cookie rimosso
     - Tentativo accesso /admin → Redirect a /login

PASS: ☐ Cookie-based authentication funzionante
```

#### Scenario B0.2.2: API Key Authentication
```
1. Login come admin (cookie-based)
2. Naviga a: http://localhost:3000/settings
3. Tab "Advanced" → Sezione "API Keys"
4. Click "Generate API Key"
5. ✅ VERIFICA:
   - API key generata (formato: mpl_dev_xxxxxxxx...)
   - Mostrato una sola volta (avviso di copiare)

6. Copia API key

7. Test API key con curl:
   curl -X GET http://localhost:8080/api/v1/games \
     -H "X-API-Key: [YOUR_API_KEY]"

8. ✅ VERIFICA:
   - HTTP 200
   - Response JSON con lista games
   - Seq logs mostrano richiesta autenticata via API key

9. Test API key invalida:
   curl -X GET http://localhost:8080/api/v1/games \
     -H "X-API-Key: invalid_key"

   ✅ VERIFICA: HTTP 401 Unauthorized

PASS: ☐ API Key authentication funzionante
```

#### Scenario B0.2.3: OAuth (Google/Discord/GitHub)
```
1. Logout da sessione corrente
2. Apri /login
3. ✅ VERIFICA: Pulsanti OAuth presenti:
   - "Continue with Google"
   - "Continue with Discord"
   - "Continue with GitHub"

4. Click "Continue with Google":
   ✅ VERIFICA: Redirect a Google OAuth consent screen

5. [MANUAL STEP] Autorizza con account Google test
6. ✅ VERIFICA dopo callback:
   - Redirect a /games o /dashboard
   - Utente autenticato
   - Profile info popolata da Google (nome, email, avatar)

7. Naviga a /settings → Tab "Privacy" → Sezione "OAuth Accounts"
8. ✅ VERIFICA:
   - Google account mostrato come "Connected"
   - Pulsante "Disconnect" disponibile

9. Click "Disconnect"
10. ✅ VERIFICA:
    - Account scollegato
    - Messaggio conferma
    - Riappare pulsante "Connect with Google"

PASS: ☐ OAuth authentication funzionante
```

#### Scenario B0.2.4: 2FA (Two-Factor Authentication)
```
1. Login come admin
2. Naviga a /settings → Tab "Privacy" → Sezione "Two-Factor Authentication"
3. ✅ VERIFICA: Sezione 2FA presente, status "Disabled"

4. Click "Enable 2FA"
5. ✅ VERIFICA:
   - QR code mostrato
   - Setup key testuale mostrato (per copia manuale)

6. Scansiona QR con app TOTP (Google Authenticator, Authy, etc.)
7. Inserisci codice 6-digit generato dall'app
8. Click "Verify and Enable"
9. ✅ VERIFICA:
   - 2FA abilitato
   - Backup codes mostrati (8-10 codici)
   - Messaggio: "Save these codes in a safe place"

10. Logout
11. Login nuovamente con admin@meepleai.dev / Demo123!
12. ✅ VERIFICA:
    - Primo step: password accepted
    - Secondo step: richiesta codice 2FA (6 digit)

13. Inserisci codice da TOTP app
14. ✅ VERIFICA: Login completato

15. Test backup code:
    - Logout
    - Login con password
    - Click "Use backup code"
    - Inserisci uno dei backup codes salvati
    ✅ VERIFICA: Login completato

PASS: ☐ 2FA authentication funzionante
```

---

### Test B0.3: Games Management (CRUD)

#### Scenario B0.3.1: List Games
```
1. Login come admin
2. Naviga a /games
3. ✅ VERIFICA:
   - Lista games mostrata (potrebbe essere vuota se fresh DB)
   - Pulsante "Add Game" presente
   - Search/filter UI presente

PASS: ☐ Games list funzionante
```

#### Scenario B0.3.2: Create Game
```
1. Click "Add Game"
2. Form "New Game" mostrato
3. Compila:
   - Name: "Catan"
   - Players: "3-4"
   - Duration: "60-120 min"
   - Complexity: "Medium"
   - Published: true
4. Submit
5. ✅ VERIFICA:
   - Redirect a /games
   - "Catan" appare in lista
   - Success message mostrato

PASS: ☐ Create game funzionante
```

#### Scenario B0.3.3: Update Game
```
1. Nella lista games, click su "Catan"
2. Click "Edit"
3. Modifica Duration: "90 min"
4. Save
5. ✅ VERIFICA:
   - Update salvato
   - Durata aggiornata nella lista

PASS: ☐ Update game funzionante
```

#### Scenario B0.3.4: Delete Game
```
1. Click su game "Catan" → Menu → "Delete"
2. Conferma delete
3. ✅ VERIFICA:
   - Game rimosso dalla lista
   - Success message

PASS: ☐ Delete game funzionante
```

---

### Test B0.4: PDF Upload & Processing (3-Stage Pipeline)

#### Scenario B0.4.1: Upload PDF
```
1. Naviga a /upload
2. ✅ VERIFICA: Upload UI presente

3. Seleziona PDF test: docs/test-pdfs/catan-it.pdf
   (se non esiste, usa qualsiasi PDF delle regole ~1-5 MB)

4. Durante upload:
   ✅ VERIFICA:
     - Progress bar visibile
     - Percentuale aumenta
     - No errori console

5. Upload completato:
   ✅ VERIFICA:
     - Success message
     - File appare in /documents list
     - Status: "Processing..." o "Queued"

PASS: ☐ Upload funzionante
```

#### Scenario B0.4.2: PDF Processing Pipeline (3-Stage)
```
1. Dopo upload, naviga a /admin/documents
2. Trova il PDF appena caricato
3. ✅ VERIFICA status progression:
   - "Queued" → "Processing" → "Completed"
   - Può richiedere 30-60 secondi

4. Apri Seq logs: http://localhost:8081
5. Cerca log del PDF processing
6. ✅ VERIFICA sequenza:
   - "Starting PDF processing for [filename]"
   - "Stage 1: Attempting Unstructured extraction"
   - Successo Stage 1 OPPURE:
     - "Stage 1 failed, trying Stage 2: SmolDocling"
     - Successo Stage 2 OPPURE:
       - "Stage 2 failed, trying Stage 3: Docnet"

7. Quando status = "Completed":
   ✅ VERIFICA nel documento:
     - Quality score presente (0.0-1.0)
     - Text preview disponibile
     - Pages count corretto
     - No errori

PASS: ☐ PDF processing 3-stage pipeline funzionante
```

#### Scenario B0.4.3: Quality Validation
```
1. Nel documento completato, verifica Quality Report
2. ✅ VERIFICA 4 metriche presenti:
   - Text coverage: % (chars/page ratio)
   - Structure detection: % (titles, headers, lists)
   - Table detection: % (game rules tables)
   - Page coverage: % (all pages processed)

3. Overall quality score: ≥0.70 (threshold)
4. Se <0.70:
   ✅ VERIFICA: Recommendation mostrata (es. "Consider re-scanning PDF")

PASS: ☐ Quality validation funzionante
```

---

### Test B0.5: RAG / Chat con Streaming

#### Scenario B0.5.1: Ask Question (Streaming SSE)
```
1. Assicurati che almeno 1 PDF sia processato (status: Completed)
2. Naviga a /chat
3. ✅ VERIFICA: Chat interface presente

4. Seleziona game: "Catan" (dropdown)
5. Invia domanda: "Come si costruisce una strada?"
6. ✅ VERIFICA:
   - Risposta inizia entro 2-3 secondi
   - Testo appare progressivamente (streaming)
   - Icona "AI typing..." durante streaming
   - Risposta completa entro 10 secondi

7. ✅ VERIFICA contenuto risposta:
   - Risposta in italiano
   - Info corretta: "1 legno + 1 argilla" (se documento è Catan)
   - Citation presente: [Rulebook p.X] (link cliccabile)

PASS: ☐ Chat streaming funzionante
```

#### Scenario B0.5.2: Hybrid Search (Vector + Keyword)
```
1. In chat, invia domanda semantica:
   "come vincere" (non match esatto keyword)

2. ✅ VERIFICA:
   - Risposta parla di punti vittoria
   - Semantic match funzionante (non solo keyword)

3. Invia domanda keyword-specifica:
   "punti vittoria" (match esatto)

4. ✅ VERIFICA:
   - Risposta menziona "punti vittoria" explicitamente
   - Keyword match incluso

5. Apri Seq, cerca log "RRF fusion"
6. ✅ VERIFICA log mostra:
   - Vector search results: X documents
   - Keyword search results: Y documents
   - RRF fusion score calculated

PASS: ☐ Hybrid search (vector+keyword) funzionante
```

#### Scenario B0.5.3: Citations
```
1. Dalla risposta chat, click su citation link [Rulebook p.X]
2. ✅ VERIFICA:
   - PDF viewer modal apre
   - Auto-scroll a pagina citata
   - Testo rilevante evidenziato (highlight giallo/verde)

3. Test zoom PDF:
   - Click "+", click "-"
   ✅ VERIFICA: Zoom funziona

4. Test navigazione pagine:
   - Arrow keys, page input
   ✅ VERIFICA: Navigazione funziona

5. Chiudi modal, ritorna a chat
   ✅ VERIFICA: Modal chiude, chat history intatta

PASS: ☐ Citations con PDF viewer funzionanti
```

#### Scenario B0.5.4: Multi-Turn Conversation
```
1. Chat aperta, invia domanda: "Come si gioca a Catan?"
2. Ricevi risposta
3. Follow-up (senza contesto): "E quanto dura?"
4. ✅ VERIFICA:
   - Risposta contestuale: "Una partita a Catan dura 60-120 minuti"
   - Context mantenuto (capisce "quanto dura" = durata partita Catan)

5. Invia terza domanda: "Quanti giocatori?"
6. ✅ VERIFICA:
   - Risposta: "Catan si gioca con 3-4 giocatori"
   - Thread context completo

PASS: ☐ Multi-turn conversation funzionante
```

---

### Test B0.6: Admin Features

#### Scenario B0.6.1: User Management
```
1. Login come admin
2. Naviga a /admin/users
3. ✅ VERIFICA:
   - Lista utenti mostrata
   - Almeno 3 demo users: admin@, editor@, user@meepleai.dev

4. Click "Create User"
5. Compila:
   - Email: testuser@example.com
   - Password: Test123!
   - Role: User
6. Submit
7. ✅ VERIFICA:
   - Utente creato
   - Appare in lista

8. Click su testuser → "Edit"
9. Cambia Role: User → Editor
10. Save
11. ✅ VERIFICA: Role aggiornato

12. Click "Delete" su testuser
13. Conferma
14. ✅ VERIFICA: Utente rimosso

PASS: ☐ User management CRUD funzionante
```

#### Scenario B0.6.2: API Keys Management
```
1. Naviga a /admin/api-keys
2. ✅ VERIFICA: Lista API keys

3. Click "Generate API Key"
4. Seleziona user: admin@meepleai.dev
5. Generate
6. ✅ VERIFICA:
   - API key creata (mpl_dev_...)
   - Mostrata una volta con warning

7. Copia key, testa:
   curl -H "X-API-Key: [KEY]" http://localhost:8080/api/v1/games
   ✅ VERIFICA: HTTP 200

8. In /admin/api-keys, click "Revoke" sulla key appena creata
9. Conferma
10. Test key revocata:
    curl -H "X-API-Key: [KEY]" http://localhost:8080/api/v1/games
    ✅ VERIFICA: HTTP 401 Unauthorized

PASS: ☐ API Keys management funzionante
```

#### Scenario B0.6.3: Configuration Management
```
1. Naviga a /admin/configuration
2. ✅ VERIFICA: Categorie config presenti:
   - Features
   - RateLimit
   - AI/LLM
   - RAG
   - PDF

3. Espandi "Features":
   ✅ VERIFICA config entries (es.):
     - Features:BGAI:Enabled = true/false
     - Features:Cache:TtlMinutes = 5

4. Click "Edit" su Features:Cache:TtlMinutes
5. Cambia valore: 5 → 10
6. Save
7. ✅ VERIFICA:
   - Valore aggiornato
   - Success message
   - Seq log: "Configuration updated"

8. Test rollback:
   - Click "History" su config appena modificata
   - ✅ VERIFICA: Mostra versioni precedenti
   - Click "Rollback" a versione precedente
   - ✅ VERIFICA: Valore ritorna a 5

PASS: ☐ Configuration management funzionante
```

#### Scenario B0.6.4: System Health & Stats
```
1. Naviga a /admin/health
2. ✅ VERIFICA componenti health:
   - PostgreSQL: Healthy ✓
   - Redis: Healthy ✓
   - Qdrant: Healthy ✓
   - Overall: Healthy ✓

3. Simula problema: docker compose stop redis
4. Refresh /admin/health
5. ✅ VERIFICA:
   - Redis: Unhealthy ✗
   - Overall: Degraded ⚠️

6. Riavvia: docker compose start redis
7. Refresh → ✅ VERIFICA: Tutto Healthy

8. Naviga a /admin/stats
9. ✅ VERIFICA dashboard stats:
   - Total users
   - Total games
   - Total documents
   - Total chat messages
   - Chart/graphs rendering

PASS: ☐ Health & stats funzionanti
```

---

### Test B0.7: Settings Page (4 Tabs - SPRINT-1 Complete)

#### Scenario B0.7.1: Profile Tab
```
1. Naviga a /settings
2. Tab "Profile" (default)
3. ✅ VERIFICA campi presenti:
   - Display Name (input)
   - Email (readonly o editable)
   - Change Password section

4. Cambia Display Name: "Test User"
5. Save
6. ✅ VERIFICA:
   - Success message
   - Nome aggiornato in header/profile

7. Change Password:
   - Current: Demo123!
   - New: NewPass123!
   - Confirm: NewPass123!
8. Save
9. ✅ VERIFICA:
   - Success message
   - Logout
   - Login con new password funziona

PASS: ☐ Profile tab funzionante
```

#### Scenario B0.7.2: Preferences Tab
```
1. Tab "Preferences"
2. ✅ VERIFICA opzioni presenti:
   - Language (dropdown): Italiano, English
   - Theme (toggle): Light / Dark / System
   - Notifications (toggle)
   - Data retention (dropdown)

3. Test theme toggle:
   - Seleziona "Dark"
   ✅ VERIFICA: UI passa a dark mode

4. Test language:
   - Seleziona "English"
   ✅ VERIFICA: UI labels in inglese

5. Save preferences
6. Refresh pagina
   ✅ VERIFICA: Preferences persistite

PASS: ☐ Preferences tab funzionante
```

#### Scenario B0.7.3: Privacy Tab
```
1. Tab "Privacy"
2. ✅ VERIFICA sezioni:
   - Two-Factor Authentication (status + enable/disable)
   - OAuth Accounts (Google, Discord, GitHub links)

3. [Già testato in B0.2.4 e B0.2.3]

PASS: ☐ Privacy tab funzionante
```

#### Scenario B0.7.4: Advanced Tab
```
1. Tab "Advanced"
2. ✅ VERIFICA sezioni:
   - API Keys (list + generate)
   - Active Sessions (list + revoke)
   - Account Deletion (danger zone)

3. [API Keys già testato in B0.2.2]

4. Active Sessions:
   ✅ VERIFICA:
     - Sessione corrente mostrata
     - Info: IP, browser, last active
     - Pulsante "Revoke" per altre sessioni

5. Account Deletion:
   - Click "Delete Account"
   ✅ VERIFICA:
     - Confirmation modal
     - Warning message
     - [NON eseguire delete effettivo, solo test UI]

PASS: ☐ Advanced tab funzionante
```

---

### Test B0.8: Observability Stack

#### Scenario B0.8.1: Seq Logs
```
1. Apri Seq: http://localhost:8081
2. ✅ VERIFICA:
   - Dashboard carica
   - Log entries presenti
   - Search funzionante

3. Filtra log: @Level = 'Error'
4. ✅ VERIFICA:
   - NO errori critici recenti (ultime 24h)
   - Se presenti errori, investigare

5. Cerca log: "Chat message received"
6. ✅ VERIFICA:
   - Log di chat messages presenti (se chat testata)
   - Correlation ID presente

PASS: ☐ Seq logging funzionante
```

#### Scenario B0.8.2: Jaeger Tracing
```
1. Apri Jaeger: http://localhost:16686
2. Service dropdown: Seleziona "meepleai-api"
3. Click "Find Traces"
4. ✅ VERIFICA:
   - Traces presenti per richieste recenti
   - Click su trace → Spans dettagliati
   - Timing info presente

PASS: ☐ Jaeger tracing funzionante
```

#### Scenario B0.8.3: Prometheus Metrics
```
1. Apri Prometheus: http://localhost:9090
2. Query: up
3. Execute
4. ✅ VERIFICA:
   - Target "meepleai-api" = 1 (up)
   - Scrape interval working

5. Query: http_requests_total
6. ✅ VERIFICA: Metrics presenti

PASS: ☐ Prometheus metrics funzionanti
```

#### Scenario B0.8.4: Grafana Dashboards
```
1. Apri Grafana: http://localhost:3001
2. Login: admin / admin (default)
3. Naviga a Dashboards
4. ✅ VERIFICA:
   - Dashboard "MeepleAI Overview" presente (se configurato)
   - Panels rendering
   - Metrics data visible

PASS: ☐ Grafana dashboards funzionanti
```

---

### Checklist Finale CHECKPOINT 0 - BASELINE

**Infrastructure (15 servizi)**:
- ☐ PostgreSQL healthy
- ☐ Qdrant healthy
- ☐ Redis healthy
- ☐ Ollama running
- ☐ Embedding service running
- ☐ Unstructured API running
- ☐ SmolDocling running
- ☐ Seq logs collecting
- ☐ Jaeger traces collecting
- ☐ Prometheus scraping
- ☐ Alertmanager configured
- ☐ Grafana dashboards visible
- ☐ n8n workflows ready
- ☐ API (backend) running
- ☐ Web (frontend) running

**Authentication**:
- ☐ Cookie-based login OK
- ☐ API Key auth OK
- ☐ OAuth (Google/Discord/GitHub) OK
- ☐ 2FA (TOTP + backup codes) OK
- ☐ Session management OK
- ☐ Logout OK

**Games Management**:
- ☐ List games OK
- ☐ Create game OK
- ☐ Update game OK
- ☐ Delete game OK

**PDF Processing**:
- ☐ Upload PDF OK
- ☐ 3-stage pipeline OK (Unstructured → SmolDocling → Docnet)
- ☐ Quality validation OK
- ☐ Document list OK

**RAG / Chat**:
- ☐ Ask question OK
- ☐ Streaming SSE OK
- ☐ Hybrid search (vector+keyword) OK
- ☐ Citations OK
- ☐ PDF viewer OK
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
- ☐ Seq logs OK
- ☐ Jaeger tracing OK
- ☐ Prometheus metrics OK
- ☐ Grafana dashboards OK

**Code Quality**:
- ☐ `dotnet build` → Success (zero errors)
- ☐ `dotnet test` → All pass
- ☐ `pnpm build` → Success
- ☐ `pnpm test` → All pass, coverage ≥90%

---

## DECISIONE FINALE CHECKPOINT 0

**✅ GO - Sistema Baseline Completamente Funzionante**:
- Tutti i test passati (☑️ ≥95% checklist items)
- Zero errori critici
- Tutte le funzionalità core operative
- Documentazione baseline creata
- **PROCEDI** con sviluppo issue

**⚠️ CONDITIONAL GO - Problemi Minori**:
- Alcuni test falliti (<5% checklist items)
- Problemi non bloccanti identificati
- Documentare issues in GitHub
- **PROCEDI** con attenzione, monitor regressions

**❌ NO-GO - Problemi Critici**:
- Test critici falliti (>5% checklist items)
- Servizi infrastructure down
- Funzionalità core non funzionanti
- **FIX** prima di iniziare qualsiasi sviluppo
- Investigare root cause
- Re-eseguire CHECKPOINT 0

---

### Template Report CHECKPOINT 0

```markdown
# CHECKPOINT 0 - Baseline Verification Report

**Data**: 2025-MM-DD
**Tester**: [Nome]
**Durata**: X ore
**Environment**: Development / Staging

## Summary

- **Total Tests**: 50+
- **Passed**: XX ✅
- **Failed**: XX ❌
- **Skipped**: XX ⏭️

## Infrastructure Status

| Service | Status | Notes |
|---------|--------|-------|
| PostgreSQL | ✅ UP | Version 16.x |
| Qdrant | ✅ UP | Collection count: 1 |
| Redis | ✅ UP | Memory usage: XX MB |
| ... | ... | ... |

## Authentication Tests

- Cookie-based: ✅ PASS
- API Key: ✅ PASS
- OAuth Google: ✅ PASS
- OAuth Discord: ⏭️ SKIP (no test account)
- 2FA: ✅ PASS

## Critical Issues Found

1. **[CRITICAL]** PDF upload fails for files >50MB
   - Issue: #XXXX
   - Workaround: None
   - Blocker: YES

2. **[MINOR]** Dark mode flickering on theme toggle
   - Issue: #XXXX
   - Blocker: NO

## Decision

☐ **GO** - All critical tests passed, proceed
☐ **CONDITIONAL GO** - Minor issues documented
☐ **NO-GO** - Critical blockers, must fix

**Next Steps**: [Descrizione]

---
Approved by: [Nome]
Date: [Data]
```

---

## CHECKPOINT 1: Critical Fixes & Security

**Timeline**: Fine Settimana 2
**Issues**: #1233, #1193
**Durata Test**: ~2 ore

### Pre-requisiti

```bash
# Avvia l'ambiente completo
cd infra
docker compose up -d

# Verifica servizi attivi
docker compose ps
# Devono essere UP: postgres, redis, qdrant, api, web, seq

# Backend
cd ../apps/api/src/Api
dotnet run

# Frontend
cd ../../../web
pnpm dev
```

---

### Test 1: SSE Error Handling (#1233)

**Obiettivo**: Verificare che gli endpoint streaming gestiscano errori senza bloccare connessioni

#### Scenario 1.1: Streaming Normale
```
1. Apri browser: http://localhost:3000/chat
2. Login come: admin@meepleai.dev / Demo123!
3. Invia domanda: "Come si gioca a Catan?"
4. ✅ VERIFICA:
   - Risposta inizia entro 2 secondi
   - Testo appare progressivamente (streaming)
   - No errori in console browser (F12)
   - No errori in Seq (http://localhost:8081)

PASS: ☐ Streaming funziona correttamente
```

#### Scenario 1.2: Disconnessione Durante Streaming
```
1. Apri chat: http://localhost:3000/chat
2. Invia domanda lunga: "Spiegami tutte le regole di Catan in dettaglio"
3. DURANTE lo streaming (dopo 2-3 secondi):
   - Chiudi tab del browser (simula disconnessione)
4. Apri Seq logs: http://localhost:8081
5. ✅ VERIFICA:
   - Log contiene "Client disconnected" o simile
   - NO stack trace di errori non gestiti
   - Server rimane stabile (non crash)

6. Riapri chat, invia nuova domanda
7. ✅ VERIFICA:
   - Streaming funziona ancora (no side effects)

PASS: ☐ Disconnessione gestita gracefully
```

#### Scenario 1.3: Timeout Streaming
```
1. Simula risposta lenta:
   - Ferma servizio Qdrant: docker compose stop qdrant
2. Apri chat, invia domanda: "Test timeout"
3. ✅ VERIFICA:
   - Dopo max 30 secondi: messaggio di timeout
   - NO connessione appesa indefinitamente
   - Errore user-friendly (non stack trace)

4. Riavvia Qdrant: docker compose start qdrant
5. Retry domanda
6. ✅ VERIFICA:
   - Sistema si riprende automaticamente

PASS: ☐ Timeout gestito correttamente
```

#### Scenario 1.4: Errore Backend Durante Streaming
```
1. Apri chat, inizia streaming
2. DURANTE streaming:
   - Ferma backend: Ctrl+C su terminale dotnet run
3. ✅ VERIFICA:
   - Frontend mostra errore: "Connessione persa" o simile
   - NO "Waiting for response..." infinito
   - Pulsante "Retry" disponibile

4. Riavvia backend
5. Click su "Retry"
6. ✅ VERIFICA:
   - Streaming riprende

PASS: ☐ Errori backend gestiti con retry
```

**CHECKPOINT 1 - Test 1 COMPLETATO**: ☐ Tutti gli scenari passati

---

### Test 2: Session Authorization & Rate Limiting (#1193)

#### Scenario 2.1: Session Validation
```
1. Login: admin@meepleai.dev / Demo123!
2. Apri DevTools → Application → Cookies
3. ✅ VERIFICA cookie presente:
   - Nome: meepleai-session
   - HttpOnly: ✓
   - Secure: ✓ (se HTTPS)
   - SameSite: Strict o Lax

4. Copia valore cookie
5. Apri browser in incognito
6. Vai su http://localhost:3000/admin
7. ✅ VERIFICA: Redirect a /login (no accesso)

8. DevTools → Application → Cookies → Aggiungi manualmente:
   - Nome: meepleai-session
   - Valore: [cookie copiato]
9. Refresh /admin
10. ✅ VERIFICA: Accesso consentito (session valida)

PASS: ☐ Session validation funziona
```

#### Scenario 2.2: Rate Limiting - Endpoint Chat
```
1. Apri terminale, prepara script rate limit:

cat > test-rate-limit.sh << 'EOF'
#!/bin/bash
for i in {1..150}; do
  curl -X POST http://localhost:8080/api/v1/chat \
    -H "Content-Type: application/json" \
    -d '{"question":"Test '$i'","gameId":1}' \
    -b "meepleai-session=YOUR_SESSION_COOKIE" \
    -w "\nStatus: %{http_code}\n" &
done
wait
EOF

2. Sostituisci YOUR_SESSION_COOKIE con cookie reale
3. Esegui: bash test-rate-limit.sh
4. ✅ VERIFICA nei response:
   - Prime ~100 richieste: HTTP 200 (OK)
   - Successive richieste: HTTP 429 (Too Many Requests)
   - Response body 429 contiene: "Rate limit exceeded"

5. Attendi 1 minuto
6. Singola richiesta:
   curl -X POST http://localhost:8080/api/v1/chat ...
7. ✅ VERIFICA: HTTP 200 (rate limit resettato)

PASS: ☐ Rate limiting attivo (100 req/min)
```

#### Scenario 2.3: IP-Based Rate Limiting
```
1. Esci da tutti i browser (logout)
2. Esegui 200 richieste NON autenticate:

for i in {1..200}; do
  curl http://localhost:8080/api/v1/games -w "%{http_code}\n"
done

3. ✅ VERIFICA:
   - Prime ~100: HTTP 200
   - Successive: HTTP 429
   - Header response: X-RateLimit-Remaining: 0

PASS: ☐ IP rate limiting funziona
```

#### Scenario 2.4: Session Expiration
```
1. Login: admin@meepleai.dev
2. Naviga a /admin/users (richiede auth)
3. ✅ VERIFICA: Pagina carica

4. Apri Seq: http://localhost:8081
5. Cerca log: "Session validation"
6. ✅ VERIFICA: Log mostra session valida

7. Modifica cookie expiration:
   - DevTools → Application → Cookies
   - Doppio click su meepleai-session
   - Expires: [imposta data passata]
8. Refresh /admin/users
9. ✅ VERIFICA: Redirect a /login (session expired)

PASS: ☐ Session expiration gestita
```

**CHECKPOINT 1 - Test 2 COMPLETATO**: ☐ Tutti gli scenari passati

---

### Checklist Finale Checkpoint 1

**Automated Tests**:
- ☐ `dotnet test` → All pass
- ☐ `pnpm test` → Coverage ≥90%

**Manual Tests**:
- ☐ Test 1.1: Streaming normale OK
- ☐ Test 1.2: Disconnessione gestita
- ☐ Test 1.3: Timeout gestito
- ☐ Test 1.4: Errori backend gestiti
- ☐ Test 2.1: Session validation OK
- ☐ Test 2.2: Rate limiting chat OK
- ☐ Test 2.3: IP rate limiting OK
- ☐ Test 2.4: Session expiration OK

**Logs & Monitoring**:
- ☐ Seq logs: Zero errori non gestiti
- ☐ Browser console: Zero errori
- ☐ Performance: P95 latency <5s

**DECISIONE**: ☐ GO per Checkpoint 2 | ☐ NO-GO (fix issues)

---

## CHECKPOINT 2: Frontend Modernization

**Timeline**: Fine Settimana 4
**Issues**: #1236, #1083-#1077
**Durata Test**: ~3 ore

### Test 3: Web Worker Upload Queue (#1236)

#### Scenario 3.1: Upload Singolo con Web Worker
```
1. Apri http://localhost:3000/upload
2. Apri DevTools → Console
3. ✅ VERIFICA log: "Upload Worker initialized" o simile

4. Seleziona PDF (5-10 MB): es. Catan_Rules_IT.pdf
5. Durante upload:
   - Apri DevTools → Performance → Start recording
   - Muovi mouse sulla pagina
   - Digita nel search box
6. ✅ VERIFICA:
   - UI rimane responsive (no freeze)
   - FPS ≥30 durante upload (Performance tab)
   - No "Long Task" warnings

7. ✅ VERIFICA upload completato:
   - Progress bar arriva a 100%
   - File appare in lista documenti
   - Console: "Upload completed via Worker"

PASS: ☐ Upload su Worker non blocca UI
```

#### Scenario 3.2: Upload Multipli Simultanei
```
1. Apri /upload
2. Seleziona 5 PDF (2-5 MB ciascuno)
3. ✅ VERIFICA:
   - 5 progress bar separate
   - Tutte procedono in parallelo
   - UI rimane responsive

4. DevTools → Application → Service Workers
5. ✅ VERIFICA: Upload Worker attivo

6. Durante upload, naviga ad altra tab del browser
7. Ritorna a /upload dopo 10 secondi
8. ✅ VERIFICA: Upload continuati in background

PASS: ☐ Upload multipli paralleli OK
```

---

### Test 4: Zustand Chat Store (#1083)

#### Scenario 4.1: State Management con Zustand
```
1. Apri /chat
2. DevTools → Console
3. Digita: window.__ZUSTAND_STORE__
4. ✅ VERIFICA: Store Zustand presente (oppure usa Redux DevTools Extension)

5. Invia messaggio: "Test 1"
6. Inspect store state
7. ✅ VERIFICA:
   - messages array contiene messaggio
   - isStreaming: false dopo risposta
   - activeThreadId presente

PASS: ☐ Zustand store funzionante
```

#### Scenario 4.2: Persistence con LocalStorage
```
1. Chat aperta, invia 3 messaggi
2. DevTools → Application → Local Storage
3. ✅ VERIFICA: Key "meepleai-chat-store" presente

4. Refresh pagina (F5)
5. ✅ VERIFICA:
   - 3 messaggi ancora visibili
   - Thread history mantenuta

6. Cancella localStorage manualmente
7. Refresh → Verifica: Chat vuota (fresh state)

PASS: ☐ Persistence funziona
```

---

### Test 5: React Hook Form + Zod (#1082)

#### Scenario 5.1: Form Validation
```
1. Apri /settings (o qualsiasi form)
2. Campo "Email": inserisci "invalid-email"
3. Submit form
4. ✅ VERIFICA:
   - Errore mostrato: "Email non valida"
   - Form NON inviato al backend
   - Focus su campo errato

5. Correggi: "test@example.com"
6. Submit
7. ✅ VERIFICA: Form inviato

PASS: ☐ Validation Zod funziona
```

---

### Test 6: TanStack Query (#1079)

#### Scenario 6.1: Caching
```
1. Apri /games
2. DevTools → Network → Clear
3. Pagina carica → ✅ VERIFICA: 1 richiesta GET /api/v1/games

4. Naviga a /chat
5. Ritorna a /games (entro 5 min)
6. ✅ VERIFICA Network:
   - NO nuova richiesta (data da cache)
   - Pagina carica istantaneamente

7. Attendi 6 minuti (oltre cache TTL)
8. Ritorna a /games
9. ✅ VERIFICA: Nuova richiesta (cache expired)

PASS: ☐ Caching TanStack Query OK
```

---

### Test 7: Lighthouse Performance

```
1. Apri /games in Incognito
2. DevTools → Lighthouse → Run
3. ✅ VERIFICA scores:
   - Performance: ≥90
   - Accessibility: ≥95
   - Best Practices: ≥90
   - SEO: ≥90

4. Verifica metriche Core Web Vitals:
   - LCP (Largest Contentful Paint): <2.5s
   - FID (First Input Delay): <100ms
   - CLS (Cumulative Layout Shift): <0.1

PASS: ☐ Lighthouse ≥90 su tutte le metriche
```

---

### Checklist Finale Checkpoint 2

**Frontend Tests**:
- ☐ Web Worker upload non blocca UI
- ☐ Upload multipli OK
- ☐ Zustand store funzionante
- ☐ Persistence localStorage OK
- ☐ Form validation Zod OK
- ☐ TanStack Query caching OK
- ☐ Lighthouse Performance ≥90

**Automated**:
- ☐ `pnpm test` → All pass
- ☐ `pnpm build` → Success

**DECISIONE**: ☐ GO per Checkpoint 3 | ☐ NO-GO

---

## CHECKPOINT 3: BGAI Quality Gate

**Timeline**: Fine Settimana 6
**Issues**: #1023, #1022, #1021, #1020, #1019, #1018
**Durata Test**: ~4 ore (CRITICAL)

### Test 8: Accuracy Validation (#1019) - MVP BLOCKER

#### Scenario 8.1: Golden Dataset Evaluation
```
1. Backend terminale:
cd apps/api/src/Api
dotnet run --project ../Tools/EvaluateGoldenDataset

2. ✅ VERIFICA output:
   Evaluating 100 Q&A pairs...
   [1/100] ✓ Question: "Quanti giocatori per Catan?" → Correct
   [2/100] ✓ Question: "Come si vince a Carcassonne?" → Correct
   ...
   [100/100] ✓

   === FINAL RESULTS ===
   Accuracy: 82/100 (82%)  ← DEVE essere ≥80%
   Hallucination Rate: 8%   ← DEVE essere ≤10%

3. Se Accuracy <80%:
   - ❌ CHECKPOINT FAILED
   - Analizzare failure cases
   - Migliorare dataset/modello
   - Re-test

PASS: ☐ Accuracy ≥80% RAGGIUNTA
```

#### Scenario 8.2: Manual Spot Check (10 Domande)
```
Testa manualmente 10 domande dal dataset:

1. "Quanti giocatori può avere Catan?"
   ✅ Risposta corretta: 3-4 (o 3-6 con espansione)

2. "Come si costruisce una strada a Catan?"
   ✅ Risposta corretta: 1 legno + 1 argilla

3. "Quanto dura una partita a 7 Wonders?"
   ✅ Risposta corretta: ~30 minuti

4. "Qual è l'obiettivo di Pandemic?"
   ✅ Risposta corretta: Curare 4 malattie cooperativamente

5. "Come si gioca a Monopoly?" (NON nel dataset)
   ✅ Risposta corretta: "Non ho informazioni su Monopoly"
   ⚠️ NO invenzioni/hallucinations

[Continua con altre 5...]

PASS: ☐ 10/10 risposte accurate manualmente
```

---

### Test 9: Performance Testing (#1020) - MVP BLOCKER

#### Scenario 9.1: Load Test con k6
```
1. Installa k6: https://k6.io/docs/get-started/installation/

2. Crea test script:
cat > load-test.js << 'EOF'
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '1m', target: 50 },  // Ramp up
    { duration: '3m', target: 50 },  // Steady
    { duration: '1m', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<3000'], // P95 <3s
  },
};

export default function () {
  const res = http.post('http://localhost:8080/api/v1/chat', JSON.stringify({
    question: 'Come si gioca a Catan?',
    gameId: 1
  }), { headers: { 'Content-Type': 'application/json' } });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time <3s': (r) => r.timings.duration < 3000,
  });

  sleep(1);
}
EOF

3. Esegui: k6 run load-test.js

4. ✅ VERIFICA output finale:
   ✓ http_req_duration............: p(95)=2450ms  ← DEVE essere <3000ms
   ✓ http_reqs....................: 15000
   ✓ checks.......................: 95% ← DEVE essere >90%

5. Se P95 >3s:
   - Profiling con dotTrace
   - Ottimizzare query lente
   - Aumentare cache
   - Re-test

PASS: ☐ P95 <3s RAGGIUNTO
```

---

### Test 10: End-to-End Workflow (#1018)

#### Scenario 10.1: Upload → Chat → Citation (Full Flow)
```
1. Fresh database:
   cd apps/api/src/Api
   dotnet ef database drop --force
   dotnet ef database update

2. Seed demo user:
   dotnet run --seed-demo-users

3. Login: admin@meepleai.dev / Demo123!

4. Upload rulebook:
   - Navigate: /upload
   - Select: docs/test-pdfs/catan-it.pdf
   - ✅ VERIFICA upload success

5. Attendi processing (check /admin/documents):
   - Status: "Processing..." → "Completed"
   - Quality score: ≥0.70
   - ✅ VERIFICA: Processing completato

6. Navigate: /chat
7. Ask: "Come si costruisce una città a Catan?"
8. ✅ VERIFICA risposta:
   - Contiene info corretta: "2 grano + 3 minerale"
   - Citation presente: [Rulebook p.8] (o pagina corretta)
   - Confidence score: ≥0.70

9. Click su citation link
10. ✅ VERIFICA:
    - PDF viewer opens
    - Auto-scroll a pagina citata
    - Testo evidenziato (highlight)

11. Test follow-up:
    Ask: "E quanto costa?"
12. ✅ VERIFICA:
    - Risposta contestuale: "Una città costa 2 grano e 3 minerale"
    - Context mantenuto dalla domanda precedente

PASS: ☐ E2E workflow completo funziona
```

---

### Checklist Finale Checkpoint 3 - MVP GATE

**CRITICAL - MVP BLOCKERS**:
- ☐ Accuracy ≥80% su golden dataset (100 Q&A)
- ☐ Hallucination rate ≤10%
- ☐ P95 latency <3s (load test)
- ☐ E2E workflow passa (upload → chat → citation)

**Automated Tests**:
- ☐ `dotnet test` → All pass (162 tests)
- ☐ E2E tests Playwright → All pass

**Documentation**:
- ☐ User guide completa (#1022)
- ☐ API docs aggiornate

**DECISIONE FINALE**:
- ☐ **GO**: Tutti i MVP blockers passati → Proceed to production
- ☐ **CONDITIONAL GO**: 75-79% accuracy → Launch with disclaimer
- ☐ **NO-GO**: <75% accuracy OR P95 >5s → Delay 1-2 settimane

---

## CHECKPOINT 4: Final Release

**Timeline**: Fine Settimana 8
**Durata Test**: ~6 ore (Smoke + Security)

### Test 11: Staging Smoke Test

```
1. Deploy su staging:
   ./deploy.sh staging

2. Run smoke test suite:
   - Login/Logout
   - Upload PDF
   - Chat query
   - Admin dashboard
   - API key auth

3. ✅ VERIFICA: All smoke tests pass

PASS: ☐ Staging smoke test OK
```

---

### Test 12: Security Audit

```
1. OWASP ZAP scan:
   - Run automated scan su staging
   - ✅ VERIFICA: Zero High/Critical vulnerabilities

2. Security headers:
   - Check https://securityheaders.com
   - ✅ VERIFICA: Grade A+ o A

3. Dependency audit:
   cd apps/api && dotnet list package --vulnerable
   cd apps/web && pnpm audit
   - ✅ VERIFICA: Zero vulnerabilities

PASS: ☐ Security audit passed
```

---

### Checklist Finale Checkpoint 4 - RELEASE

**Pre-Production**:
- ☐ Staging smoke test passed
- ☐ Security audit clean
- ☐ Performance benchmarks validated
- ☐ Rollback plan tested
- ☐ Monitoring/alerting configured

**Production Deployment**:
- ☐ Database backup created
- ☐ ./deploy.sh production executed
- ☐ Health check: /health returns 200
- ☐ Sample query tested

**Post-Deployment (48h monitoring)**:
- ☐ Sentry: Zero critical errors
- ☐ Seq: Normal log patterns
- ☐ Grafana: All metrics green
- ☐ User reports: No critical issues

**DECISIONE FINALE**: ☐ RELEASE APPROVED 🎉 | ☐ ROLLBACK

---

## 📊 Template Report Checkpoint

Usa questo template per documentare ogni checkpoint:

```markdown
# Checkpoint X Report

**Data**: YYYY-MM-DD
**Tester**: [Nome]
**Durata**: X ore

## Test Results

| Test | Scenario | Status | Notes |
|------|----------|--------|-------|
| Test 1.1 | Streaming normale | ✅ PASS | Latency 1.2s |
| Test 1.2 | Disconnessione | ✅ PASS | Graceful |
| ... | ... | ... | ... |

## Issues Found

1. **[CRITICAL]** Upload worker crashes con file >50MB
   - Repro: Upload large PDF
   - Fix: Issue #XXXX created
   - Status: Blocking checkpoint

2. **[MINOR]** Tooltip flickering in dark mode
   - Non-blocking
   - Deferred to next sprint

## Metrics

- Accuracy: 82% ✅ (target ≥80%)
- P95 Latency: 2.8s ✅ (target <3s)
- Test Coverage: 91% ✅ (target ≥90%)

## Decision

☐ GO - All critical tests passed, proceed to next checkpoint
☐ CONDITIONAL GO - Minor issues, can proceed with monitoring
☐ NO-GO - Critical issues found, must fix before proceeding

**Next Steps**: [Descrizione]

---
Approved by: [Nome]
Date: [Data]
```

---

**Fine Guida Testing**

**Versione**: 1.0
**Prossimo Update**: Dopo Checkpoint 1
**Owner**: QA Team
