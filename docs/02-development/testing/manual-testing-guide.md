# Guida al Testing Manuale - MeepleAI

**Versione**: 1.0
**Data**: 2025-11-13
**Target**: Alpha/Beta Testing

---

## Indice

1. [Setup Ambiente di Test](#1-setup-ambiente-di-test)
2. [Autenticazione e Gestione Utenti](#2-autenticazione-e-gestione-utenti)
3. [Gestione Giochi](#3-gestione-giochi)
4. [Upload e Processamento PDF](#4-upload-e-processamento-pdf)
5. [Chat e RAG (Hybrid Search)](#5-chat-e-rag-hybrid-search)
6. [Amministrazione](#6-amministrazione)
7. [Configurazione Dinamica](#7-configurazione-dinamica)
8. [API Key](#8-api-key)
9. [OAuth](#9-oauth)
10. [2FA (Two-Factor Authentication)](#10-2fa-two-factor-authentication)
11. [Test di Performance](#11-test-di-performance)
12. [Test di Sicurezza](#12-test-di-sicurezza)
13. [Checklist Finale](#13-checklist-finale)

---

## 1. Setup Ambiente di Test

### 1.1 Avvio Stack Completo

**Azione**:
```bash
cd infra
docker compose up -d
cd ../apps/api/src/Api
dotnet run
# In un altro terminale
cd apps/web
pnpm dev
```

**Risultato Atteso**:
- ✅ PostgreSQL: porta 5432
- ✅ Qdrant: porta 6333 (http://localhost:6333/dashboard)
- ✅ Redis: porta 6379
- ✅ n8n: porta 5678 (http://localhost:5678)
- ✅ Seq: porta 8081 (http://localhost:8081)
- ✅ Jaeger: porta 16686 (http://localhost:16686)
- ✅ API Backend: porta 8080 (http://localhost:8080/health)
- ✅ Frontend Next.js: porta 3000 (http://localhost:3000)

**Verifica Health**:
```bash
curl http://localhost:8080/health
```

**Risultato Atteso**:
```json
{
  "status": "Healthy",
  "checks": {
    "postgres": "Healthy",
    "redis": "Healthy",
    "qdrant": "Healthy"
  }
}
```

---

## 2. Autenticazione e Gestione Utenti

### 2.1 Registrazione Nuovo Utente

**Azione**:
1. Apri http://localhost:3000
2. Clicca su "Register" o vai a `/login`
3. Compila il form:
   - Email: `tester@meepleai.dev`
   - Password: `Test123!`
   - Display Name: `Test User`
   - Role: `User` (default)
4. Clicca "Register"

**Risultato Atteso**:
- ✅ Redirect alla homepage
- ✅ Messaggio di benvenuto con nome utente
- ✅ Cookie di sessione impostato (controlla DevTools > Application > Cookies)
- ✅ Session valida per 7 giorni

**Verifica Backend**:
- Logs in Seq: `User {UserId} registered successfully`
- Database: nuovo record in `users` table

### 2.2 Login con Credenziali Esistenti

**Azione**:
1. Logout (se loggato)
2. Vai a `/login`
3. Inserisci (use your registered admin account):
   - Email: `your-admin@example.com`
   - Password: `YourPassword123!`
4. Clicca "Login"

**Nota**: Crea un admin user via `INITIAL_ADMIN_EMAIL` env var o registrazione manuale.

**Risultato Atteso**:
- ✅ Redirect alla homepage
- ✅ Session cookie valido
- ✅ Role: `Admin` visibile nel profilo

### 2.3 Logout

**Azione**:
1. Clicca sul menu utente (in alto a destra)
2. Clicca "Logout"

**Risultato Atteso**:
- ✅ Redirect a `/login`
- ✅ Cookie di sessione cancellato
- ✅ Messaggio: "Logged out successfully"

### 2.4 Gestione Password - Reset

**Azione**:
1. Vai a `/login`
2. Clicca "Forgot Password?"
3. Inserisci email: `your-email@example.com`
4. Clicca "Send Reset Link"

**Risultato Atteso**:
- ✅ Messaggio: "Reset link sent to your email"
- ✅ In produzione: email inviata
- ✅ In dev: token visibile nei logs di Seq

**Completamento Reset**:
1. Copia il token dai logs
2. Vai a `/reset-password?token={TOKEN}`
3. Inserisci nuova password: `NewPassword123!`
4. Clicca "Reset Password"

**Risultato Atteso**:
- ✅ Messaggio: "Password reset successfully"
- ✅ Redirect a `/login`
- ✅ Login con nuova password funzionante

### 2.5 Gestione Profilo Utente

**Azione**:
1. Login come `admin-test@example.com`
2. Vai a `/profile`
3. Modifica:
   - Display Name: `Admin Updated`
   - Email: lascia invariata
4. Clicca "Save Changes"

**Risultato Atteso**:
- ✅ Messaggio: "Profile updated successfully"
- ✅ Display name aggiornato nella UI
- ✅ Database: record aggiornato in `users`

---

## 3. Gestione Giochi

### 3.1 Visualizzazione Lista Giochi

**Azione**:
1. Login come qualsiasi utente
2. Vai alla homepage `/` o naviga alla sezione giochi

**Risultato Atteso**:
- ✅ Lista di giochi disponibili
- ✅ Ogni gioco mostra:
  - Titolo
  - Editore
  - Anno pubblicazione
  - Numero giocatori (min-max)
  - Durata partita (min-max)

**API Endpoint**:
```bash
curl -X GET http://localhost:8080/api/v1/games \
  -H "Cookie: MeepleAI.Session={SESSION_TOKEN}"
```

### 3.2 Creazione Nuovo Gioco (Admin/Editor)

**Azione**:
1. Login come `admin-test@example.com` o `editor-test@example.com`
2. Vai alla sezione Admin o Game Management
3. Clicca "Add New Game"
4. Compila:
   - Title: `Catan`
   - Publisher: `Catan Studio`
   - Year Published: `1995`
   - Min Players: `3`
   - Max Players: `4`
   - Min Play Time: `60`
   - Max Play Time: `120`
5. Clicca "Create Game"

**Risultato Atteso**:
- ✅ Messaggio: "Game created successfully"
- ✅ Gioco appare nella lista
- ✅ ID gioco generato (GUID)

**Verifica Permessi**:
- ❌ User normale: bottone "Add Game" NON visibile
- ✅ Admin/Editor: bottone visibile e funzionante

### 3.3 Modifica Gioco Esistente (Admin/Editor)

**Azione**:
1. Login come Admin/Editor
2. Seleziona un gioco dalla lista
3. Clicca "Edit"
4. Modifica campo "Year Published": `1996`
5. Clicca "Save"

**Risultato Atteso**:
- ✅ Messaggio: "Game updated successfully"
- ✅ Modifiche visibili nella lista
- ✅ Timestamp `UpdatedAt` aggiornato

### 3.4 Avvio Game Session

**Azione**:
1. Login come qualsiasi utente
2. Seleziona un gioco
3. Clicca "Start Game Session"
4. Inserisci giocatori:
   - Player 1: `Alice`
   - Player 2: `Bob`
   - Player 3: `Charlie`
5. Clicca "Start Session"

**Risultato Atteso**:
- ✅ Session ID generato
- ✅ Stato: `Active`
- ✅ Timestamp `StartedAt` registrato
- ✅ UI mostra sessione attiva

**API Endpoint**:
```bash
curl -X POST http://localhost:8080/api/v1/sessions \
  -H "Content-Type: application/json" \
  -H "Cookie: MeepleAI.Session={SESSION_TOKEN}" \
  -d '{
    "gameId": "{GAME_ID}",
    "players": ["Alice", "Bob", "Charlie"]
  }'
```

### 3.5 Completamento Game Session

**Azione**:
1. Con una sessione attiva
2. Clicca "Complete Session"
3. Opzionale: inserisci vincitore `Alice`
4. Clicca "Complete"

**Risultato Atteso**:
- ✅ Stato: `Completed`
- ✅ Timestamp `CompletedAt` registrato
- ✅ Winner registrato: `Alice`
- ✅ Sessione non più modificabile

### 3.6 Abbandono Game Session

**Azione**:
1. Con una sessione attiva
2. Clicca "Abandon Session"
3. Conferma azione

**Risultato Atteso**:
- ✅ Stato: `Abandoned`
- ✅ Timestamp `AbandonedAt` registrato
- ✅ Sessione archiviata

---

## 4. Upload e Processamento PDF

### 4.1 Upload PDF con Successo (3-Stage Pipeline)

**Azione**:
1. Login come `admin-test@example.com` o `editor-test@example.com`
2. Vai a `/upload`
3. Seleziona gioco: `Catan`
4. Carica PDF: `catan_rules.pdf` (file di test, <50MB)
5. Clicca "Upload"

**Risultato Atteso**:
- ✅ Upload progress bar visibile
- ✅ **Stage 1 (Unstructured)**: Processing inizia
  - Se qualità ≥0.80: ✅ Success
  - Tempo medio: ~1.3s
- ✅ Se Stage 1 fallisce → **Stage 2 (SmolDocling VLM)**:
  - Se qualità ≥0.70: ✅ Success
  - Tempo medio: 3-5s
- ✅ Se Stage 2 fallisce → **Stage 3 (Docnet)**:
  - Best effort extraction
  - Tempo: <1s
- ✅ Messaggio: "PDF processed successfully"
- ✅ Quality Report generato:
  - Text Coverage: %
  - Structure Detection: %
  - Table Detection: %
  - Page Coverage: %
  - **Overall Score**: 0.00-1.00

**Quality Metrics da Verificare**:
```json
{
  "extractorUsed": "Unstructured",  // o "SmolDocling" o "Docnet"
  "qualityScore": 0.85,
  "metrics": {
    "textCoverage": 0.90,        // 40% weight
    "structureDetection": 0.80,  // 20% weight
    "tableDetection": 0.85,      // 20% weight
    "pageCoverage": 1.00         // 20% weight
  },
  "recommendations": [
    "Good text coverage",
    "All pages processed successfully"
  ]
}
```

**Verifica Backend**:
- Logs in Seq:
  - `PDF uploaded successfully: {PdfId}`
  - `Stage 1 (Unstructured) extraction completed with quality 0.85`
- Database:
  - Nuovo record in `pdf_documents` table
  - `ExtractedText` popolato
  - `QualityScore` e `QualityMetrics` salvati

### 4.2 Upload PDF - Validazione Fallita

**Test Case A: File troppo grande**

**Azione**:
1. Carica PDF >50MB

**Risultato Atteso**:
- ❌ Errore: "File size exceeds 50MB limit"
- ❌ Upload bloccato prima del backend

**Test Case B: File non-PDF**

**Azione**:
1. Carica file `.txt` o `.docx`

**Risultato Atteso**:
- ❌ Errore: "Invalid file type. Only PDF files are allowed"
- ❌ Magic bytes validation fallita

**Test Case C: PDF corrotto**

**Azione**:
1. Carica PDF con header corrotto

**Risultato Atteso**:
- ❌ Errore: "Invalid PDF file"
- ❌ Dettagli validazione in risposta

### 4.3 Feature Flag - PDF Upload Disabilitato

**Azione**:
1. Admin: vai a `/admin/configuration`
2. Cerca `Features.PdfUpload`
3. Imposta `Value`: `false`
4. Clicca "Save"
5. Tenta upload PDF

**Risultato Atteso**:
- ❌ HTTP 403
- ❌ Messaggio: "PDF uploads are currently disabled"
- ❌ `featureName`: "Features.PdfUpload"

**Ripristino**:
1. Imposta `Features.PdfUpload` = `true`
2. Upload riprende a funzionare

### 4.4 Visualizzazione PDF Processati

**Azione**:
1. Vai alla lista PDF per un gioco
2. Visualizza dettagli documento

**Risultato Atteso**:
- ✅ Nome file
- ✅ Data upload
- ✅ Utente che ha caricato
- ✅ Quality Score
- ✅ Extractor utilizzato (Unstructured/SmolDocling/Docnet)
- ✅ Numero di pagine
- ✅ Preview testo estratto (primi 500 caratteri)

### 4.5 Eliminazione PDF (Admin/Editor)

**Azione**:
1. Seleziona un PDF dalla lista
2. Clicca "Delete"
3. Conferma eliminazione

**Risultato Atteso**:
- ✅ Messaggio: "PDF deleted successfully"
- ✅ File rimosso dal filesystem
- ✅ Record database soft-deleted
- ✅ Vettori Qdrant rimossi

---

## 5. Chat e RAG (Hybrid Search)

### 5.1 Creazione Nuova Chat

**Azione**:
1. Login come qualsiasi utente
2. Vai a `/chat`
3. Seleziona gioco: `Catan`
4. Clicca "New Chat"

**Risultato Atteso**:
- ✅ Chat ID generato
- ✅ Agent associato al gioco
- ✅ Interfaccia chat vuota pronta
- ✅ Timestamp `StartedAt` registrato

### 5.2 Domanda Semplice (Vector Search)

**Azione**:
1. In una chat attiva per `Catan`
2. Scrivi: "Come si piazzano gli insediamenti iniziali?"
3. Invia messaggio

**Risultato Atteso**:
- ✅ Messaggio utente appare immediatamente
- ✅ Indicatore "AI is typing..."
- ✅ **Hybrid Search (RRF)** eseguito:
  - Vector search su Qdrant
  - Keyword search su PostgreSQL FTS
  - Fusion con RRF (70% vector, 30% keyword)
- ✅ Risposta AI appare dopo 2-5s
- ✅ Risposta include:
  - Testo della regola
  - **Citations** con riferimenti PDF (pagina, documento)
  - Confidence score ≥0.70
- ✅ **Follow-up questions** suggerite (2-3)

**Verifica Backend**:
- Logs: `Hybrid search completed with 5 results (RRF fusion)`
- Logs: `AI response generated with confidence 0.85`
- Risposta streaming (SSE) se supportato

### 5.3 Domanda Complessa (Multi-Model Consensus)

**Azione**:
1. Scrivi: "Qual è la strategia migliore per vincere con 3 giocatori considerando la scarsità di legno?"

**Risultato Atteso**:
- ✅ **Multi-model generation**:
  - GPT-4 risponde
  - Claude Sonnet risponde
  - Sistema seleziona risposta con confidence maggiore
- ✅ Risposta più lunga (3-5 paragrafi)
- ✅ Citations multiple
- ✅ Tempo: 5-10s
- ✅ Confidence score ≥0.70 richiesto per pubblicare

### 5.4 Validazione 5-Layer

**Verifica che il sistema applichi le validazioni**:

**Test A: Confidence troppo bassa**
- Sistema genera risposta con confidence <0.70
- ❌ Risposta NON pubblicata
- ✅ Messaggio: "I'm not confident enough to answer. Please rephrase or ask something else."

**Test B: Citation mancante**
- Domanda richiede fonte specifica
- Risposta non include citations
- ❌ Risposta invalidata
- ✅ Richiesta rigenerazione

**Test C: Forbidden keywords**
- Risposta contiene: "I don't know", "I'm not sure", "maybe"
- ❌ Confidence score penalizzato
- ✅ Possibile rigenerazione

**Test D: Hallucination check**
- Citations verificate contro documenti reali
- ❌ Se citation non esiste: risposta invalidata

**Test E: Consistency check**
- Due risposte multi-model troppo diverse
- ✅ Sistema richiede chiarimento o ulteriore elaborazione

### 5.5 Export Chat

**Azione**:
1. Con una chat contenente ≥5 messaggi
2. Clicca "Export Chat"
3. Seleziona formato: `JSON`
4. Clicca "Download"

**Risultato Atteso**:
- ✅ File scaricato: `chat_{CHAT_ID}_{DATE}.json`
- ✅ Contenuto:
  ```json
  {
    "chatId": "...",
    "gameTitle": "Catan",
    "exportedAt": "2025-11-13T10:30:00Z",
    "messages": [
      {
        "role": "user",
        "content": "...",
        "timestamp": "..."
      },
      {
        "role": "assistant",
        "content": "...",
        "citations": [...],
        "confidence": 0.85,
        "timestamp": "..."
      }
    ]
  }
  ```

**Formati supportati**:
- ✅ JSON
- ✅ Markdown (`.md`)
- ✅ Plain text (`.txt`)

### 5.6 Edit Messaggio Utente (CHAT-06)

**Prerequisito**: Feature `Features.MessageEditDelete` = `true`

**Azione**:
1. Hover su un messaggio utente inviato
2. Clicca icona "Edit" (matita)
3. Modifica testo: "Come si costruisce una città?" → "Come si costruisce una città e quanto costa?"
4. Clicca "Save"

**Risultato Atteso**:
- ✅ Messaggio aggiornato visibile
- ✅ Timestamp `UpdatedAt` registrato
- ✅ **Tutti i messaggi AI successivi invalidati**
- ✅ Indicatore "edited" sul messaggio
- ✅ Chat continua da questo punto

**Limitazione**:
- ❌ Solo l'autore può editare i propri messaggi

### 5.7 Delete Messaggio (CHAT-06)

**Azione**:
1. Hover su un messaggio
2. Clicca icona "Delete" (cestino)
3. Conferma eliminazione

**Risultato Atteso**:
- ✅ Messaggio soft-deleted (non rimosso fisicamente)
- ✅ UI mostra: "[Message deleted]"
- ✅ `IsDeleted` = true, `DeletedAt` registrato
- ✅ Messaggi AI successivi invalidati

**Permessi**:
- ✅ User: può eliminare solo i propri messaggi
- ✅ Admin: può eliminare qualsiasi messaggio

### 5.8 Feature Flag - Chat Export Disabilitato

**Azione**:
1. Admin: disabilita `Features.ChatExport` = `false`
2. Tenta export chat

**Risultato Atteso**:
- ❌ HTTP 403
- ❌ Messaggio: "Chat export is currently unavailable"
- ❌ Bottone "Export" disabilitato o nascosto in UI

---

## 6. Amministrazione

### 6.1 Dashboard Admin

**Azione**:
1. Login come `admin-test@example.com`
2. Vai a `/admin`

**Risultato Atteso**:
- ✅ Statistiche overview:
  - Total Users
  - Total Games
  - Total PDF Documents
  - Total Chat Sessions
  - Active Sessions (oggi)
- ✅ Grafici:
  - User registrations (ultimi 30 giorni)
  - PDF uploads (ultimi 30 giorni)
  - Chat activity (ultimi 7 giorni)
- ✅ Recent Activity feed

**Permessi**:
- ❌ User/Editor: Accesso negato (403)
- ✅ Admin: Full access

### 6.2 Gestione Utenti

**Azione**:
1. Vai a `/admin/users`
2. Visualizza lista utenti

**Risultato Atteso**:
- ✅ Tabella con:
  - Email
  - Display Name
  - Role
  - Created At
  - Last Login
  - Status (Active/Disabled)
- ✅ Paginazione (50 per pagina)
- ✅ Ricerca per email/nome
- ✅ Filtro per role

**Modifica Role**:
1. Seleziona utente `tester@meepleai.dev`
2. Clicca "Edit"
3. Cambia Role: `User` → `Editor`
4. Clicca "Save"

**Risultato Atteso**:
- ✅ Role aggiornato
- ✅ Utente ora ha permessi Editor (create/edit games, upload PDF)

**Disabilita Utente**:
1. Clicca "Disable" su un utente
2. Conferma azione

**Risultato Atteso**:
- ✅ Utente non può più fare login
- ✅ Sessioni esistenti invalidate
- ✅ Status: `Disabled`

### 6.3 Gestione Alerts

**Azione**:
1. Vai a `/admin` o sezione alerts
2. Visualizza alerts recenti

**Risultato Atteso**:
- ✅ Lista alerts con:
  - Type (Info, Warning, Error, Critical)
  - Message
  - Timestamp
  - Source (API, Frontend, Background Job)
  - Status (New, Acknowledged, Resolved)

**Notifiche Alert** (OPS-07):
- ✅ Email inviata per severity ≥Warning
- ✅ Slack webhook chiamato per Critical
- ✅ PagerDuty integrazione (se configurato)

**Acknowledge Alert**:
1. Clicca su un alert
2. Clicca "Acknowledge"
3. Opzionale: aggiungi note

**Risultato Atteso**:
- ✅ Status: `Acknowledged`
- ✅ Timestamp e user registrati

### 6.4 Analytics

**Azione**:
1. Vai a `/admin/analytics`
2. Seleziona range: "Last 7 days"

**Risultato Atteso**:
- ✅ **User Activity**:
  - Logins per day
  - Active users (DAU)
  - Retention rate
- ✅ **Chat Analytics**:
  - Total questions asked
  - Average response time
  - Confidence distribution
  - Most asked questions
- ✅ **RAG Performance**:
  - P@10 (Precision at 10)
  - MRR (Mean Reciprocal Rank)
  - Hybrid search vs pure vector
  - Cache hit rate
- ✅ **PDF Processing**:
  - Upload success rate
  - Stage 1/2/3 distribution
  - Average quality score
  - Processing time per stage

**Export Analytics**:
1. Clicca "Export Report"
2. Seleziona formato: CSV o JSON

**Risultato Atteso**:
- ✅ File scaricato con dati completi

---

## 7. Configurazione Dinamica

### 7.1 Visualizzazione Configurazioni

**Azione**:
1. Login come Admin
2. Vai a `/admin/configuration`

**Risultato Atteso**:
- ✅ Categorizzazione configurazioni:
  - **Features**: Feature flags
  - **RateLimit**: Throttling settings
  - **AI/LLM**: Model configs, API keys
  - **RAG**: Hybrid search weights, thresholds
  - **PDF**: Upload limits, quality thresholds
  - **System**: General settings
- ✅ Ogni config mostra:
  - Key
  - Value (attuale)
  - Default Value
  - Description
  - Last Updated
  - Updated By

### 7.2 Modifica Configurazione

**Azione**:
1. Cerca config: `RAG.HybridSearch.VectorWeight`
2. Valore attuale: `0.70`
3. Clicca "Edit"
4. Modifica: `0.80`
5. Clicca "Save"

**Risultato Atteso**:
- ✅ Messaggio: "Configuration updated successfully"
- ✅ **Nessun restart richiesto** (hot reload)
- ✅ Version control: nuovo record in `configuration_history`
- ✅ Timestamp e user registrati

**Verifica Effetto**:
- Prossima query hybrid search usa 80% vector, 20% keyword

### 7.3 Rollback Configurazione

**Azione**:
1. Visualizza history config `RAG.HybridSearch.VectorWeight`
2. Vedi tutte le versioni precedenti
3. Seleziona versione: `0.70` (timestamp: 2025-11-10)
4. Clicca "Rollback to this version"

**Risultato Atteso**:
- ✅ Config ripristinata a `0.70`
- ✅ Nuovo record in history (rollback è un update)
- ✅ Note: "Rolled back from version X"

### 7.4 Bulk Configuration Update

**Azione**:
1. Clicca "Bulk Operations"
2. Seleziona categoria: "Features"
3. Seleziona multiple configs:
   - `Features.PdfUpload`
   - `Features.ChatExport`
   - `Features.MessageEditDelete`
4. Azione: "Enable All"
5. Clicca "Apply"

**Risultato Atteso**:
- ✅ Tutte le features selezionate = `true`
- ✅ Messaggio: "3 configurations updated successfully"
- ✅ Audit trail per ogni modifica

### 7.5 3-Tier Fallback

**Test Fallback Logic**:

**Scenario**: Config non esiste in DB

**Verifica**:
1. Backend cerca in DB: ❌ Non trovato
2. Backend cerca in `appsettings.json`: ✅ Trovato
3. Usa valore da JSON

**Scenario**: Config non in DB né JSON

**Verifica**:
1. Backend cerca in DB: ❌
2. Backend cerca in JSON: ❌
3. Usa valore di default hardcoded in codice

**Test**:
```bash
# Elimina config da DB
DELETE FROM configuration WHERE key = 'RAG.Validation.MinConfidence';

# Richiesta API
# Risultato: usa valore da appsettings.json (0.70) o default (0.70)
```

---

## 8. API Key

### 8.1 Generazione API Key

**Azione**:
1. Login come qualsiasi utente
2. Vai a `/settings` o `/profile`
3. Sezione "API Keys"
4. Clicca "Generate New API Key"
5. Inserisci nome: "Test Key"
6. Seleziona scopes:
   - `read:games`
   - `read:chat`
   - `write:chat`
7. Clicca "Generate"

**Risultato Atteso**:
- ✅ API Key mostrata **una sola volta**: `mpl_dev_abc123def456...`
- ✅ Warning: "Save this key, you won't see it again"
- ✅ Formato: `mpl_{env}_{base64}`
- ✅ Key hash salvato in DB (PBKDF2, 210k iter)
- ✅ Data creazione e scopes registrati

### 8.2 Uso API Key per Autenticazione

**Azione**:
```bash
curl -X GET http://localhost:8080/api/v1/games \
  -H "Authorization: Bearer mpl_dev_abc123def456..."
```

**Risultato Atteso**:
- ✅ HTTP 200
- ✅ Risposta JSON con lista giochi
- ✅ Autenticazione via API key (no cookie)

**Test Scope**:
```bash
# Scope consentito: read:games
curl -X GET http://localhost:8080/api/v1/games \
  -H "Authorization: Bearer {API_KEY}"
# ✅ 200 OK

# Scope NON consentito: write:games (non selezionato)
curl -X POST http://localhost:8080/api/v1/games \
  -H "Authorization: Bearer {API_KEY}" \
  -H "Content-Type: application/json" \
  -d '{"title": "New Game", ...}'
# ❌ 403 Forbidden - Scope insufficient
```

### 8.3 Revoca API Key

**Azione**:
1. Vai a `/settings`
2. Lista API Keys attive
3. Seleziona "Test Key"
4. Clicca "Revoke"
5. Conferma

**Risultato Atteso**:
- ✅ Key marcata come revocata
- ✅ `RevokedAt` timestamp registrato
- ✅ Uso futuro della key: ❌ 401 Unauthorized

**Verifica**:
```bash
curl -X GET http://localhost:8080/api/v1/games \
  -H "Authorization: Bearer {REVOKED_KEY}"
# ❌ 401 Unauthorized: "API key has been revoked"
```

### 8.4 API Key Expiration (Opzionale)

**Azione**:
1. Genera key con expiration: 7 giorni
2. Attendi scadenza (o simula cambiando data DB)

**Risultato Atteso**:
- ❌ Dopo expiration: 401 Unauthorized
- ❌ Messaggio: "API key has expired"

---

## 9. OAuth

### 9.1 Login con Google

**Azione**:
1. Vai a `/login`
2. Clicca "Sign in with Google"
3. Redirect a Google OAuth
4. Seleziona account Google
5. Autorizza MeepleAI
6. Redirect a `/auth/callback`

**Risultato Atteso**:
- ✅ Redirect alla homepage
- ✅ Se utente esistente: login automatico
- ✅ Se nuovo utente: account creato automaticamente
  - Email da Google
  - Display name da Google
  - Role: `User` (default)
  - Password: NON impostata (OAuth only)
- ✅ Token OAuth criptato e salvato in `oauth_accounts`
- ✅ Session cookie impostato

### 9.2 Login con Discord

**Azione**:
1. Clicca "Sign in with Discord"
2. Autorizza su Discord
3. Callback

**Risultato Atteso**:
- ✅ Comportamento identico a Google
- ✅ Avatar Discord salvato (opzionale)
- ✅ Discord ID linkato a account MeepleAI

### 9.3 Login con GitHub

**Azione**:
1. Clicca "Sign in with GitHub"
2. Autorizza su GitHub
3. Callback

**Risultato Atteso**:
- ✅ Login funzionante
- ✅ GitHub username come display name

### 9.4 Link OAuth a Account Esistente

**Azione**:
1. Login con email/password: `admin-test@example.com`
2. Vai a `/settings`
3. Sezione "Connected Accounts"
4. Clicca "Connect Google"
5. Autorizza Google

**Risultato Atteso**:
- ✅ Account Google linkato
- ✅ Possibile login sia con email/pwd sia con Google
- ✅ Record in `oauth_accounts` con `user_id` linkato

### 9.5 Unlink OAuth Account

**Azione**:
1. Vai a `/settings`
2. Sezione "Connected Accounts"
3. Seleziona "Google" (linked)
4. Clicca "Disconnect"

**Risultato Atteso**:
- ✅ Link rimosso
- ✅ Login con Google non più possibile per questo account
- ✅ Login con email/password ancora funzionante

---

## 10. 2FA (Two-Factor Authentication)

### 10.1 Attivazione 2FA

**Azione**:
1. Login come `admin-test@example.com`
2. Vai a `/settings` > "Security"
3. Sezione "Two-Factor Authentication"
4. Clicca "Enable 2FA"
5. Scansiona QR code con app authenticator (Google Authenticator, Authy, etc.)
6. Inserisci codice 6-digit di verifica
7. Clicca "Verify and Enable"

**Risultato Atteso**:
- ✅ 2FA attivato
- ✅ **Backup codes** generati (10 codes)
- ✅ Warning: "Save these codes securely"
- ✅ Display backup codes una sola volta
- ✅ Database: `TwoFactorEnabled` = true, `TwoFactorSecret` criptato

### 10.2 Login con 2FA

**Azione**:
1. Logout
2. Login con email/password: `admin-test@example.com` / `TestPassword123!`
3. Redirect a `/login/2fa`
4. Inserisci codice 6-digit da authenticator app
5. Clicca "Verify"

**Risultato Atteso**:
- ✅ Codice validato (TOTP, window ±30s)
- ✅ Session cookie impostato
- ✅ Redirect alla homepage
- ✅ Session valida per 7 giorni (no 2FA richiesto ad ogni login se cookie valido)

**Test Codice Errato**:
- Inserisci codice: `000000`
- ❌ Errore: "Invalid 2FA code"
- ❌ Session NON creata
- ✅ Max 3 tentativi, poi lockout temporaneo (5 min)

### 10.3 Login con Backup Code

**Azione**:
1. Login email/password
2. Schermata 2FA
3. Clicca "Use backup code"
4. Inserisci uno dei 10 backup codes salvati
5. Clicca "Verify"

**Risultato Atteso**:
- ✅ Backup code validato
- ✅ Codice **consumato** (singolo uso)
- ✅ Session creata
- ✅ Warning: "You have X backup codes remaining"

**Se tutti i backup codes usati**:
- ✅ Warning critico: "Generate new backup codes"

### 10.4 Generazione Nuovi Backup Codes

**Azione**:
1. Vai a `/settings` > "Security"
2. Sezione "2FA Backup Codes"
3. Clicca "Regenerate Backup Codes"
4. Conferma (invalida codici vecchi)

**Risultato Atteso**:
- ✅ 10 nuovi backup codes generati
- ✅ Vecchi codes invalidati
- ✅ Display una sola volta

### 10.5 Disattivazione 2FA

**Azione**:
1. Vai a `/settings` > "Security"
2. Clicca "Disable 2FA"
3. Conferma con password
4. Inserisci codice 2FA corrente (verifica)
5. Clicca "Disable"

**Risultato Atteso**:
- ✅ 2FA disattivato
- ✅ `TwoFactorEnabled` = false
- ✅ `TwoFactorSecret` eliminato
- ✅ Backup codes invalidati
- ✅ Login successivi: no 2FA richiesto

### 10.6 Recovery 2FA (Senza Accesso)

**Scenario**: Utente perde telefono con authenticator app

**Azione**:
1. Contatta admin
2. Admin va a `/admin/users`
3. Seleziona utente
4. Clicca "Reset 2FA"

**Risultato Atteso**:
- ✅ 2FA disattivato per utente
- ✅ Utente può fare login con solo email/password
- ✅ Utente può ri-attivare 2FA da `/settings`

---

## 11. Test di Performance

### 11.1 Cache Efficacy (HybridCache L1+L2)

**Test**:
1. Query RAG: "Come si vince a Catan?"
2. Nota tempo risposta: ~5s (prima chiamata, cache miss)
3. Ripeti identica query entro 5 minuti
4. Nota tempo risposta: ~200ms (cache hit)

**Risultato Atteso**:
- ✅ Cache L1 (memoria): <100ms
- ✅ Cache L2 (Redis): 100-300ms
- ✅ Cache TTL: 5 minuti
- ✅ Logs: `Cache hit for key: hybrid_search_{hash}`

**Verifica Cache**:
```bash
# Redis CLI
redis-cli
> KEYS hybrid_search:*
> TTL hybrid_search:{KEY}
# Risultato: ~300 secondi rimanenti
```

### 11.2 Connection Pooling

**Test**:
1. Simula 50 richieste simultanee:
   ```bash
   seq 1 50 | xargs -P50 -I{} curl http://localhost:8080/api/v1/games
   ```

**Risultato Atteso**:
- ✅ Tutte le richieste completate
- ✅ Nessun errore "connection pool exhausted"
- ✅ PostgreSQL pool: min 10, max 100 connections
- ✅ Redis pool: 3 retries su failure

### 11.3 Compression (Brotli/Gzip)

**Test**:
```bash
curl -H "Accept-Encoding: br" http://localhost:8080/api/v1/games -v
```

**Risultato Atteso**:
- ✅ Response header: `Content-Encoding: br`
- ✅ Dimensione risposta ridotta del 60-80%
- ✅ Fallback a Gzip se Brotli non supportato

### 11.4 Query Performance (AsNoTracking)

**Verifica nei logs**:
- Query read-only usano `AsNoTracking()`
- Tempo esecuzione query: 30% più veloce vs tracking abilitato

**Test**:
```bash
# Benchmark
time curl http://localhost:8080/api/v1/games
# Tempo atteso: <200ms per 100 giochi
```

---

## 12. Test di Sicurezza

### 12.1 SQL Injection (Prevented)

**Test**:
```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin-test@example.com'' OR ''1''=''1",
    "password": "anything"
  }'
```

**Risultato Atteso**:
- ❌ Login fallito
- ✅ EF Core parametrized queries prevengono injection
- ✅ Nessun errore SQL esposto

### 12.2 XSS Prevention

**Test**:
1. In chat, inserisci messaggio: `<script>alert('XSS')</script>`
2. Invia

**Risultato Atteso**:
- ✅ Messaggio salvato come plain text
- ✅ UI rendering: escaped HTML
- ✅ Nessuno script eseguito
- ✅ React automaticamente sanitizza output

### 12.3 CORS Policy

**Test**:
```bash
curl -X GET http://localhost:8080/api/v1/games \
  -H "Origin: http://malicious-site.com"
```

**Risultato Atteso**:
- ❌ CORS block
- ✅ Solo `http://localhost:3000` consentito in dev
- ✅ In prod: solo domini whitelisted

### 12.4 Rate Limiting

**Test**:
```bash
for i in {1..100}; do
  curl -X POST http://localhost:8080/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@test.com","password":"wrong"}'
done
```

**Risultato Atteso**:
- ✅ Prime 10 richieste: 401 Unauthorized
- ❌ Richiesta 11+: 429 Too Many Requests
- ✅ Header: `Retry-After: 60` (1 minuto)
- ✅ Rate limit: 10 tentativi login per IP per minuto

### 12.5 Session Security

**Verifica Cookie**:
- ✅ `HttpOnly`: true (no JavaScript access)
- ✅ `Secure`: true (solo HTTPS in prod)
- ✅ `SameSite`: Strict o Lax
- ✅ Session ID: random, non-guessable (256-bit)

**Test Session Hijacking Prevention**:
1. Copia session cookie
2. Usa cookie da altro browser con diverso User-Agent
3. Richiesta API

**Risultato Atteso**:
- ❌ 401 Unauthorized (User-Agent mismatch detection opzionale)
- ✅ IP address tracking (optional strict mode)

### 12.6 Password Policy

**Test**:
```bash
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "weak@test.com",
    "password": "12345"
  }'
```

**Risultato Atteso**:
- ❌ 400 Bad Request
- ❌ Errore: "Password must be at least 8 characters with uppercase, lowercase, number, and special character"

**Policy**:
- Minimo 8 caratteri
- Almeno 1 maiuscola
- Almeno 1 minuscola
- Almeno 1 numero
- Almeno 1 carattere speciale

### 12.7 API Key Security

**Verifica Storage**:
- ✅ Key hash: PBKDF2 con 210,000 iterazioni
- ✅ Salt: random per ogni key
- ✅ Key plain-text **MAI** salvata in DB

**Test Brute Force Prevention**:
```bash
for i in {1..100}; do
  curl -H "Authorization: Bearer mpl_dev_wrong_key" \
    http://localhost:8080/api/v1/games
done
```

**Risultato Atteso**:
- ❌ 401 per ogni richiesta
- ✅ Rate limit: 100 tentativi per IP per ora
- ❌ Dopo limit: 429 Too Many Requests

---

## 13. Checklist Finale

### 13.1 Functionality

- [ ] Registrazione e login funzionanti
- [ ] OAuth (Google, Discord, GitHub) funzionante
- [ ] 2FA setup e login funzionanti
- [ ] Password reset funzionante
- [ ] Gestione profilo funzionante
- [ ] API Key generation e uso funzionanti
- [ ] CRUD giochi funzionante (Admin/Editor)
- [ ] Game sessions (start/complete/abandon)
- [ ] Upload PDF con 3-stage pipeline
- [ ] PDF quality validation e reporting
- [ ] Chat creation e messaging
- [ ] Hybrid search (vector + keyword RRF)
- [ ] Multi-model AI responses
- [ ] 5-layer validation
- [ ] Citations e confidence scores
- [ ] Follow-up questions
- [ ] Message edit/delete (CHAT-06)
- [ ] Chat export (JSON/Markdown/TXT)
- [ ] Admin dashboard e statistiche
- [ ] User management (Admin)
- [ ] Alerts e notifiche
- [ ] Analytics e reporting
- [ ] Configurazione dinamica
- [ ] Configuration rollback
- [ ] Feature flags funzionanti

### 13.2 Performance

- [ ] Cache L1+L2 funzionante (hit rate >70%)
- [ ] Query response time <200ms per lista
- [ ] RAG query time <5s (first), <500ms (cached)
- [ ] PDF processing: Stage 1 <2s, Stage 2 <5s
- [ ] Connection pooling: 50 concurrent requests OK
- [ ] Compression: 60-80% riduzione payload
- [ ] No memory leaks (test 1h continuo)

### 13.3 Security

- [ ] SQL injection prevenuta
- [ ] XSS prevenuta
- [ ] CORS policy applicata
- [ ] Rate limiting funzionante
- [ ] Session cookies sicuri (HttpOnly, Secure, SameSite)
- [ ] Password policy applicata
- [ ] API Key hashing (PBKDF2 210k)
- [ ] OAuth token encryption
- [ ] 2FA TOTP sicuro
- [ ] No sensitive data nei logs
- [ ] HTTPS in produzione
- [ ] Security headers (CSP, X-Frame-Options, etc.)

### 13.4 Observability

- [ ] Health endpoint funzionante
- [ ] Logs strutturati in Seq
- [ ] Correlation IDs in tutti i logs
- [ ] OpenTelemetry traces in Jaeger
- [ ] Metrics Prometheus `/metrics`
- [ ] Grafana dashboards (opzionale)
- [ ] Alerting via email/Slack
- [ ] Audit trail per azioni admin

### 13.5 User Experience

- [ ] UI responsive (mobile, tablet, desktop)
- [ ] Loading states visibili
- [ ] Error messages chiari e actionable
- [ ] Success confirmations
- [ ] Tooltips e help text dove necessario
- [ ] Accessibility (keyboard navigation, screen readers)
- [ ] Tema scuro/chiaro funzionante (se implementato)
- [ ] Localizzazione italiana corretta

### 13.6 Deployment

- [ ] Docker Compose up senza errori
- [ ] Migrations applicate automaticamente
- [ ] Seed data caricati
- [ ] Tutti i servizi health check OK
- [ ] Nessun port conflict
- [ ] Volume persistence funzionante
- [ ] Logs accessibili (docker logs)
- [ ] Restart policies applicate

---

## 14. Reporting Issues

### Formato Report Bug

**Titolo**: [COMPONENT] Breve descrizione

**Descrizione**:
- **Steps to reproduce**: 1, 2, 3...
- **Expected result**: ...
- **Actual result**: ...
- **Environment**: Browser, OS, versione
- **Severity**: Critical / High / Medium / Low
- **Screenshots**: (se applicabile)
- **Logs**: (copia da Seq se disponibile)

**Esempio**:
```
[AUTH] Login con 2FA fallisce con codice corretto

Steps:
1. Login con admin-test@example.com / TestPassword123!
2. Inserisco codice 2FA corretto da Google Authenticator: 123456
3. Clicca "Verify"

Expected: Login successful, redirect to homepage
Actual: Errore "Invalid 2FA code" anche con codice corretto

Environment: Chrome 120, Windows 11
Severity: High
Logs: [2025-11-13 10:30:45] ERROR: TOTP validation failed for user {UserId}
```

---

## 15. Note Finali

- **Tempo stimato test completo**: 4-6 ore
- **Priorità**: Security > Functionality > Performance > UX
- **Automated tests**: 4,225 tests run automaticamente in CI (non sostituiscono test manuali)
- **Coverage target**: 90%+ (attuale: 90.03% frontend, 90%+ backend)
- **Supporto**: Contatta il team di sviluppo per chiarimenti

**Buon testing!** 🎲
