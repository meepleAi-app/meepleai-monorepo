# MeepleAI API - Postman Collection

Guida completa per testare le API di MeepleAI utilizzando Postman Desktop.

## 📋 Indice

- [Panoramica](#panoramica)
- [Prerequisiti](#prerequisiti)
- [Installazione](#installazione)
- [Configurazione](#configurazione)
- [Autenticazione](#autenticazione)
- [Esecuzione dei Test](#esecuzione-dei-test)
- [Struttura della Collection](#struttura-della-collection)
- [Endpoint Principali](#endpoint-principali)
- [Variabili d'Ambiente](#variabili-dambiente)
- [Test Automatici](#test-automatici)
- [Troubleshooting](#troubleshooting)

---

## 🎯 Panoramica

Questa collection Postman fornisce **70+ richieste** pre-configurate per testare tutte le funzionalità dell'API MeepleAI:

- ✅ **Health Check**: Verifica stato API e dipendenze
- 🔐 **Authentication**: Login, registro, sessioni, API keys, OAuth, 2FA
- 🎲 **Games**: Catalogo giochi e dettagli
- 🎮 **Game Sessions**: Tracking partite e statistiche
- 🤖 **AI & RAG**: Q&A, spiegazioni, setup guide, chess agent
- 🎮 **BoardGameGeek**: Ricerca giochi e metadata
- 👨‍💼 **Admin**: Logs e gestione utenti

**Architettura**: ASP.NET 9, DDD/CQRS, 7 bounded contexts

---

## 📦 Prerequisiti

### 1. Postman Desktop

Scarica e installa [Postman Desktop](https://www.postman.com/downloads/) (versione 10.0+).

> ⚠️ **Nota**: La collection è ottimizzata per Postman Desktop. Postman Web potrebbe avere limitazioni con i cookie e gli streaming endpoints.

### 2. MeepleAI Backend in esecuzione

**Opzione A - Docker Compose (consigliato):**

```bash
cd infra
docker compose up -d
```

**Opzione B - Esecuzione locale:**

```bash
# Terminal 1: Servizi infrastrutturali
cd infra
docker compose up meepleai-postgres meepleai-qdrant meepleai-redis

# Terminal 2: API
cd apps/api/src/Api
dotnet run

# Terminal 3 (opzionale): Frontend
cd apps/web
pnpm dev
```

**Verifica:**

```bash
curl http://localhost:5080/health
```

Dovresti ricevere: `{"status":"Healthy"}`

---

## 🚀 Installazione

### 1. Importa la Collection

1. Apri **Postman Desktop**
2. Clicca su **Import** (in alto a sinistra)
3. Seleziona i seguenti file:
   - `tests/postman/MeepleAI-API.postman_collection.json`
   - `tests/postman/MeepleAI-Local.postman_environment.json`
   - `tests/postman/MeepleAI-Production.postman_environment.json` (opzionale)

### 2. Seleziona l'Environment

1. In alto a destra, nel dropdown **Environment**
2. Seleziona **"MeepleAI - Local Development"**
3. Clicca sull'icona 👁️ per vedere le variabili

---

## ⚙️ Configurazione

### Variabili d'Ambiente

La collection utilizza queste variabili (auto-popolate durante i test):

| Variabile | Descrizione | Esempio |
|-----------|-------------|---------|
| `base_url` | URL base API | `http://localhost:5080` |
| `api_key` | API key per autenticazione | `mpl_dev_abc123...` |
| `user_id` | ID utente corrente | `550e8400-e29b-41d4-a716-446655440000` |
| `game_id` | ID gioco di test | `550e8400-e29b-41d4-a716-446655440001` |
| `session_id` | ID sessione di gioco | `550e8400-e29b-41d4-a716-446655440002` |
| `totp_secret` | Secret per 2FA TOTP | `JBSWY3DPEHPK3PXP` |
| `timestamp` | Timestamp corrente | Auto-generato |

> 💡 **Nota**: Le variabili `user_id`, `game_id`, `session_id` vengono popolate automaticamente dopo il login e la creazione di risorse.

---

## 🔐 Autenticazione

MeepleAI supporta **3 metodi di autenticazione**:

### 1. Cookie-based Session (consigliato per test manuali)

**Procedura:**

1. Esegui **"Authentication > Login"**
   - Email: `demo@meepleai.dev`
   - Password: `Demo123!`
2. Postman salva automaticamente il cookie `meepleai_session`
3. Tutte le richieste successive useranno questo cookie

**Credenziali Demo:**

| Email | Password | Ruolo |
|-------|----------|-------|
| `admin@meepleai.dev` | `Demo123!` | Admin |
| `editor@meepleai.dev` | `Demo123!` | Editor |
| `demo@meepleai.dev` | `Demo123!` | User |

### 2. API Key (consigliato per automazione)

**Creazione API Key:**

1. Login come utente (vedi sopra)
2. Vai su `/settings` → **Advanced** tab
3. Clicca **"Generate New API Key"**
4. Copia la chiave (formato: `mpl_dev_...`)
5. Salva in Postman:
   - Clicca sull'environment attivo (in alto a destra)
   - Modifica `api_key` con la chiave copiata

**Utilizzo:**

- Metodo 1: Aggiungi header manualmente
  ```
  Authorization: ApiKey {{api_key}}
  ```

- Metodo 2: Usa endpoint dedicato
  - Esegui **"Authentication > API Key Login"**
  - Questo setta un cookie httpOnly per le richieste successive

### 3. OAuth 2.0 (Google, Discord, GitHub)

**Nota**: OAuth richiede un browser per il flusso di autorizzazione. Non è completamente automatizzabile in Postman.

**Procedura:**

1. Esegui **"Health Check"** per verificare la connessione
2. Esegui **"Authentication > Login"** con credenziali demo
3. Verifica autenticazione con **"Authentication > Get Current User"**

---

## 🧪 Esecuzione dei Test

### Test Singolo

1. Naviga alla cartella desiderata (es. **"Games"**)
2. Clicca su una richiesta (es. **"Get All Games"**)
3. Clicca **Send** (o `Ctrl+Enter` / `Cmd+Enter`)
4. Verifica la risposta e i test automatici nella tab **"Test Results"**

### Test di una Cartella

1. Clicca sui `...` accanto alla cartella (es. **"Authentication"**)
2. Seleziona **"Run folder"**
3. Configura le opzioni:
   - **Iterations**: 1 (o più per load testing)
   - **Delay**: 0ms (o aggiungi delay tra richieste)
4. Clicca **"Run MeepleAI API Collection"**

### Test dell'Intera Collection

1. Clicca sui `...` accanto alla collection
2. Seleziona **"Run collection"**
3. Configura e avvia (come sopra)

### Esecuzione da CLI (Newman)

Installa Newman:

```bash
npm install -g newman
```

Esegui la collection:

```bash
# Esecuzione base
newman run tests/postman/MeepleAI-API.postman_collection.json \
  -e tests/postman/MeepleAI-Local.postman_environment.json

# Con report HTML
newman run tests/postman/MeepleAI-API.postman_collection.json \
  -e tests/postman/MeepleAI-Local.postman_environment.json \
  -r html --reporter-html-export report.html

# Con reporter dettagliato
newman run tests/postman/MeepleAI-API.postman_collection.json \
  -e tests/postman/MeepleAI-Local.postman_environment.json \
  -r cli,json --reporter-json-export results.json
```

---

## 📂 Struttura della Collection

```
MeepleAI API Collection/
├── Health Check/
│   └── Health Check                    # GET /health
├── Authentication/
│   ├── Register User                   # POST /api/v1/auth/register
│   ├── Login                           # POST /api/v1/auth/login
│   ├── Get Current User                # GET /api/v1/auth/me
│   ├── Logout                          # POST /api/v1/auth/logout
│   ├── API Key Login                   # POST /api/v1/auth/apikey/login
│   ├── Get Session Status              # GET /api/v1/auth/session/status
│   ├── Extend Session                  # POST /api/v1/auth/session/extend
│   └── Get User Sessions               # GET /api/v1/users/me/sessions
├── Two-Factor Authentication (2FA)/
│   ├── Setup 2FA                       # POST /api/v1/auth/2fa/setup
│   ├── Enable 2FA                      # POST /api/v1/auth/2fa/enable
│   ├── Get 2FA Status                  # GET /api/v1/users/me/2fa/status
│   └── Disable 2FA                     # POST /api/v1/auth/2fa/disable
├── Games/
│   ├── Get All Games                   # GET /api/v1/games
│   ├── Get Game by ID                  # GET /api/v1/games/{id}
│   ├── Get Game Details                # GET /api/v1/games/{id}/details
│   ├── Get Game Rules                  # GET /api/v1/games/{id}/rules
│   └── Create Game (Admin/Editor)      # POST /api/v1/games
├── Game Sessions/
│   ├── Start Game Session              # POST /api/v1/sessions
│   ├── Get Session by ID               # GET /api/v1/sessions/{id}
│   ├── Add Player to Session           # POST /api/v1/sessions/{id}/players
│   ├── Pause Session                   # POST /api/v1/sessions/{id}/pause
│   ├── Resume Session                  # POST /api/v1/sessions/{id}/resume
│   ├── Complete Session                # POST /api/v1/sessions/{id}/complete
│   ├── Get Active Sessions             # GET /api/v1/sessions/active
│   └── Get Session Statistics          # GET /api/v1/sessions/statistics
├── AI & RAG/
│   ├── QA Agent                        # POST /api/v1/agents/qa
│   ├── QA Agent (Streaming)            # POST /api/v1/agents/qa/stream (SSE)
│   ├── Explain Agent                   # POST /api/v1/agents/explain
│   ├── Setup Guide Agent (Streaming)   # POST /api/v1/agents/setup (SSE)
│   ├── Chess Agent                     # POST /api/v1/agents/chess
│   └── Provide Agent Feedback          # POST /api/v1/agents/feedback
├── BoardGameGeek (BGG) API/
│   ├── Search BGG Games                # GET /api/v1/bgg/search
│   └── Get BGG Game Details            # GET /api/v1/bgg/games/{bggId}
└── Admin/
    ├── Get Logs                        # GET /api/v1/logs
    └── Search Users                    # GET /api/v1/users/search
```

---

## 🔍 Endpoint Principali

### Health Check

```http
GET /health
```

**Descrizione**: Verifica lo stato dell'API e delle dipendenze (PostgreSQL, Redis, Qdrant).

**Risposta**:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567"
}
```

### Authentication

#### Login

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "demo@meepleai.dev",
  "password": "Demo123!"
}
```

**Risposta**:
```json
{
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "demo@meepleai.dev",
    "displayName": "Demo User",
    "role": "User"
  },
  "expiresAt": "2025-12-19T00:00:00Z"
}
```

**Con 2FA abilitato**:
```json
{
  "requiresTwoFactor": true,
  "sessionToken": "temp_token_123",
  "message": "Two-factor authentication required"
}
```

#### Get Current User

```http
GET /api/v1/auth/me
```

**Risposta**:
```json
{
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "demo@meepleai.dev",
    "displayName": "Demo User",
    "role": "User"
  },
  "expiresAt": "2025-12-19T00:00:00Z"
}
```

### Games

#### Get All Games

```http
GET /api/v1/games
```

**Risposta**:
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440001",
    "title": "Catan",
    "publisher": "Catan Studio",
    "yearPublished": 1995,
    "minPlayers": 3,
    "maxPlayers": 4,
    "minPlayTimeMinutes": 60,
    "maxPlayTimeMinutes": 120
  }
]
```

### AI & RAG

#### QA Agent

```http
POST /api/v1/agents/qa?generateFollowUps=true
Content-Type: application/json

{
  "gameId": "550e8400-e29b-41d4-a716-446655440001",
  "query": "How do you win the game?",
  "searchMode": "Hybrid"
}
```

**Risposta**:
```json
{
  "answer": "To win Catan, you need to be the first player to reach 10 victory points...",
  "snippets": [
    {
      "text": "The first player to reach 10 victory points wins the game.",
      "page": 5,
      "score": 0.92
    }
  ],
  "confidence": 0.92,
  "totalTokens": 1250,
  "followUpQuestions": [
    "What are the different ways to earn victory points?",
    "Can you trade development cards for victory points?"
  ]
}
```

#### QA Agent (Streaming SSE)

```http
POST /api/v1/agents/qa/stream?generateFollowUps=true
Content-Type: application/json

{
  "gameId": "550e8400-e29b-41d4-a716-446655440001",
  "query": "How do you win the game?",
  "searchMode": "Hybrid"
}
```

**Risposta (Server-Sent Events)**:
```
data: {"type":"Token","data":{"token":"To"},"timestamp":"2025-11-19T12:00:00Z"}

data: {"type":"Token","data":{"token":" win"},"timestamp":"2025-11-19T12:00:00Z"}

data: {"type":"Citations","data":{"citations":[...]},"timestamp":"2025-11-19T12:00:01Z"}

data: {"type":"Complete","data":{"totalTokens":1250,"confidence":0.92},"timestamp":"2025-11-19T12:00:02Z"}

data: {"type":"FollowUpQuestions","data":{"questions":["..."]}}
```

> ⚠️ **Nota**: Gli endpoint streaming SSE mostrano solo il raw response in Postman. Per vedere gli eventi in tempo reale, usa un client SSE dedicato o il frontend.

---

## 🔧 Variabili d'Ambiente

### Automatiche (popolate dai test)

Queste variabili vengono automaticamente impostate durante l'esecuzione:

| Variabile | Quando viene impostata | Esempio |
|-----------|------------------------|---------|
| `user_id` | Dopo login o registrazione | Script nel test "Login" |
| `game_id` | Dopo "Get All Games" | Script nel test "Get All Games" |
| `session_id` | Dopo "Start Game Session" | Script nel test "Start Game Session" |
| `totp_secret` | Dopo "Setup 2FA" | Script nel test "Setup 2FA" |

**Esempio di script di test** (già incluso nella collection):

```javascript
// Test: Authentication > Login
pm.test('Response contains user data', function() {
    const jsonData = pm.response.json();
    pm.expect(jsonData.user).to.exist;

    // Salva user_id per richieste successive
    pm.environment.set('user_id', jsonData.user.id);
});
```

### Manuali

Queste variabili devono essere configurate manualmente:

| Variabile | Configurazione | Note |
|-----------|----------------|------|
| `base_url` | Modifica nell'environment | Default: `http://localhost:5080` |
| `api_key` | Genera da `/settings` o usa endpoint "API Key Login" | Formato: `mpl_dev_...` |

**Come modificare una variabile:**

1. Clicca sull'icona 👁️ accanto all'environment attivo
2. Clicca **Edit**
3. Modifica i valori nella colonna **CURRENT VALUE**
4. Clicca **Save**

---

## ✅ Test Automatici

Ogni richiesta include **test automatici** che verificano:

- ✅ Status code corretto (200, 201, 401, etc.)
- ✅ Presenza campi obbligatori nella risposta
- ✅ Tipi di dati corretti
- ✅ Valori attesi (es. email, role)
- ✅ Cookie di sessione impostati
- ✅ Performance (response time < 5s)

**Esempio di test inclusi** (già nella collection):

```javascript
// Test per "Authentication > Login"
pm.test('Status code is 200', function() {
    pm.response.to.have.status(200);
});

pm.test('Response contains user data', function() {
    const jsonData = pm.response.json();
    pm.expect(jsonData.user).to.exist;
    pm.expect(jsonData.user.email).to.eql('demo@meepleai.dev');
});

pm.test('Session cookie is set', function() {
    pm.expect(pm.cookies.has('meepleai_session')).to.be.true;
});
```

**Visualizzare i risultati:**

1. Dopo aver eseguito una richiesta
2. Clicca sulla tab **"Test Results"** (sotto la risposta)
3. Verrai i test passati (✅ verde) o falliti (❌ rosso)

---

## 📊 Workflow Comuni

### 1. Test Completo Autenticazione

**Ordine di esecuzione:**

1. ✅ **Health Check** - Verifica API attiva
2. 🔐 **Login** - Ottieni sessione (salva `user_id`)
3. 👤 **Get Current User** - Verifica autenticazione
4. 📋 **Get User Sessions** - Lista sessioni attive
5. ⏱️ **Get Session Status** - Controlla scadenza
6. ⏭️ **Extend Session** - Estendi sessione
7. 🚪 **Logout** - Termina sessione

### 2. Test Completo Giochi e Sessioni

**Ordine di esecuzione:**

1. 🔐 **Login**
2. 🎲 **Get All Games** - Lista giochi (salva `game_id`)
3. 🎮 **Get Game by ID** - Dettagli gioco
4. 📜 **Get Game Rules** - Regole gioco
5. 🎯 **Start Game Session** - Inizia partita (salva `session_id`)
6. ➕ **Add Player to Session** - Aggiungi giocatore
7. ⏸️ **Pause Session** - Metti in pausa
8. ▶️ **Resume Session** - Riprendi
9. ✅ **Complete Session** - Termina partita

### 3. Test AI & RAG

**Ordine di esecuzione:**

1. 🔐 **Login**
2. 🎲 **Get All Games** - Ottieni `game_id`
3. 🤖 **QA Agent** - Fai una domanda
4. 📖 **Explain Agent** - Richiedi spiegazione
5. 📋 **Setup Guide Agent** - Genera guida setup
6. ♟️ **Chess Agent** - Domanda su scacchi
7. 👍 **Provide Agent Feedback** - Invia feedback

### 4. Test 2FA Completo

**Ordine di esecuzione:**

1. 🔐 **Login** - Con account senza 2FA
2. 🔒 **Setup 2FA** - Genera TOTP secret + QR code
3. 📱 **Enable 2FA** - Abilita con codice TOTP (usa app authenticator)
4. ✅ **Get 2FA Status** - Verifica abilitato
5. 🚪 **Logout**
6. 🔐 **Login** (ricevi `requiresTwoFactor: true`)
7. 🔓 **Verify 2FA** - Verifica con codice TOTP
8. 🔓 **Disable 2FA** - Disabilita (richiede password + codice)

> 💡 **Nota 2FA**: Per testare 2FA, avrai bisogno di un'app authenticator (Google Authenticator, Authy, etc.) sul tuo smartphone per generare i codici TOTP dal QR code ricevuto.

---

## 🐛 Troubleshooting

### Problema: "Unauthorized" (401) dopo Login

**Causa**: Cookie non salvato o scaduto.

**Soluzioni**:

1. Verifica di aver eseguito **Login** con successo
2. Controlla che Postman abbia salvato il cookie:
   - Vai su **Cookies** (sotto l'URL bar)
   - Verifica presenza di `meepleai_session` per `localhost:5080`
3. Se manca, disabilita/riabilita **"Automatically follow redirects"** nelle impostazioni della richiesta
4. Prova con **API Key** invece del cookie:
   - Genera chiave da `/settings`
   - Aggiungi header: `Authorization: ApiKey {{api_key}}`

### Problema: "Connection Refused" o "ECONNREFUSED"

**Causa**: API non in esecuzione.

**Soluzioni**:

1. Verifica che Docker Compose sia attivo:
   ```bash
   docker compose ps
   ```
2. Controlla logs API:
   ```bash
   docker compose logs meepleai-api
   ```
3. Verifica porta corretta:
   - API: `http://localhost:5080`
   - Frontend: `http://localhost:3000`
4. Esegui health check manuale:
   ```bash
   curl http://localhost:5080/health
   ```

### Problema: "Game not found" o "Session not found"

**Causa**: Variabile `game_id` o `session_id` non impostata.

**Soluzioni**:

1. Esegui **"Get All Games"** prima per popolare `game_id`
2. Esegui **"Start Game Session"** prima per popolare `session_id`
3. Verifica variabili nell'environment (icona 👁️)
4. Se mancano, impostale manualmente con un ID valido

### Problema: "Feature disabled" (403) per endpoint streaming

**Causa**: Feature flag disabilitato.

**Soluzioni**:

1. Verifica configurazione in `appsettings.json`:
   ```json
   {
     "Features": {
       "StreamingResponses": true,
       "SetupGuideGeneration": true
     }
   }
   ```
2. Riavvia API dopo modifica
3. Contatta admin se in produzione

### Problema: Test falliti per performance (response time)

**Causa**: Sistema sovraccarico o rete lenta.

**Soluzioni**:

1. Aumenta il threshold nei test:
   ```javascript
   pm.test('Response time is acceptable', function() {
       pm.expect(pm.response.responseTime).to.be.below(10000); // 10s invece di 5s
   });
   ```
2. Disabilita altri servizi Docker non necessari
3. Controlla CPU/RAM del sistema:
   ```bash
   docker stats
   ```

### Problema: 2FA non funziona

**Causa**: Clock non sincronizzato o codice scaduto.

**Soluzioni**:

1. Verifica sincronizzazione orario:
   - TOTP usa timestamp, richiede clock accurato (±30s)
2. Genera nuovo codice TOTP dall'app (si aggiorna ogni 30s)
3. Usa backup code se disponibile
4. Rigenera secret con **"Setup 2FA"**

### Problema: "CORS error" in Postman Web

**Causa**: Postman Web ha limitazioni CORS.

**Soluzioni**:

1. Usa **Postman Desktop** (consigliato)
2. Abilita [Postman Interceptor](https://learning.postman.com/docs/sending-requests/capturing-request-data/interceptor/) nel browser
3. Configura proxy Postman

---

## 📚 Risorse Aggiuntive

### Documentazione

- **API Specification**: `docs/03-api/board-game-ai-api-specification.md`
- **Architecture Overview**: `docs/01-architecture/overview/system-architecture.md`
- **Testing Guide**: `docs/02-development/testing/testing-guide.md`
- **Main Documentation Index**: `docs/INDEX.md` (115+ docs)

### Link Utili

- **Postman Learning Center**: [https://learning.postman.com/](https://learning.postman.com/)
- **Newman CLI**: [https://learning.postman.com/docs/collections/using-newman-cli/command-line-integration-with-newman/](https://learning.postman.com/docs/collections/using-newman-cli/command-line-integration-with-newman/)
- **Server-Sent Events (SSE)**: [https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)

### Comandi Rapidi

```bash
# Avvia stack completo
cd infra && docker compose up -d

# Solo servizi essenziali
cd infra && docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-api

# Health check
curl http://localhost:5080/health

# Logs API
docker compose logs -f meepleai-api

# Fermati tutto
docker compose down

# Test Newman
newman run tests/postman/MeepleAI-API.postman_collection.json \
  -e tests/postman/MeepleAI-Local.postman_environment.json
```

---

## 🤝 Contribuire

Hai trovato un bug o vuoi aggiungere nuovi test?

1. **Issue**: Apri una issue su GitHub con il tag `postman` o `testing`
2. **Pull Request**: Modifica i file JSON e invia una PR con:
   - Descrizione delle modifiche
   - Test eseguiti
   - Screenshots (se rilevante)

**Best Practices**:

- ✅ Aggiungi test automatici per ogni nuova richiesta
- ✅ Usa variabili d'ambiente invece di valori hardcoded
- ✅ Aggiungi descrizioni chiare per ogni endpoint
- ✅ Testa con Newman CLI prima di committare

---

## 📝 Changelog

### 2025-11-19 - v1.0.0

- ✅ Collection completa con 70+ endpoint
- ✅ Test automatici per tutti gli endpoint
- ✅ Environment per Local e Production
- ✅ Documentazione completa in italiano
- ✅ Supporto autenticazione: Cookie, API Key, OAuth, 2FA
- ✅ Endpoint AI/RAG con streaming SSE
- ✅ Variabili auto-popolate

---

## 📄 Licenza

Questo progetto è parte di MeepleAI e segue la stessa licenza del repository principale.

---

## 📞 Supporto

- **Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
- **Docs**: `docs/INDEX.md` (115+ documenti)
- **Email**: support@meepleai.dev

---

**MeepleAI** - AI board game rules assistant 🎲🤖

Versione Collection: **1.0.0** | Ultima modifica: **2025-11-19**
