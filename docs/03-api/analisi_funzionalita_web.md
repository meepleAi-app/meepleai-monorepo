# Analisi Funzionalità Web - MeepleAI Application

## Panoramica Architettura

L' applicazione utilizza un'architettura **DDD (Domain-Driven Design)** con **CQRS (Command Query Responsibility Segregation)** e **MediatR** per gestire le operazioni.

### Stack Tecnologico

- **Backend**: ASP.NET Core 8+ con Minimal APIs
- **Frontend**: Next.js 14+ (App Router) con TypeScript
- **Pattern**: DDD/CQRS con MediatR
- **Database**: PostgreSQL
- **Storage**: MinIO (blob storage per PDF)
- **Vector DB**: Qdrant (semantic search)
- **Cache**: Redis

### Bounded Contexts Identificati

1. **Authentication** - Gestione autenticazione e sessioni
2. **DocumentProcessing** - Upload e processing di PDF
3. **GameManagement** - CRUD giochi e sessioni di gioco
4. **KnowledgeBase** - RAG (Retrieval-Augmented Generation) e AI
5. **Administration** - Amministrazione sistema e telemetria
6. **SystemConfiguration** - Gestione configurazioni e feature flags
7. **UserNotifications** - Sistema di notifiche
8. **WorkflowIntegration** - Integrazione con n8n

---

## 1. AUTHENTICATION - Gestione Autenticazione e Sessioni

### 1.1 Registrazione Utente

**Descrizione**: Registrazione di un nuovo utente con email e password.

**Flusso**:

1. L'utente invia email, password, displayName e role
2. Il sistema valida i dati
3. Viene creato l'utente nel database
4. Viene generata una nuova sessione
5. Viene impostato il cookie di sessione httpOnly
6. Ritorna i dati utente e token

**Endpoint API**: `POST /api/v1/auth/register`

**Backend**:

- **File**: [AuthenticationEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AuthenticationEndpoints.cs)
- **Handler**: `RegisterCommandHandler` (CQRS)
- **Command**: `Api.BoundedContexts.Authentication.Application.Commands.RegisterCommand`
- **Services**: `PasswordHasher`, `SessionService`, `MeepleAiDbContext`
- **Entities**: `User`, `UserSession`

**Frontend**:

- **Pagina**: [apps/web/src/app/register/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/register/page.tsx)
- **Componenti**: Form di registrazione con validazione
- **State**: React Hook Form per gestione form

---

### 1.2 Login Utente

**Descrizione**: Autenticazione utente con email e password, supporta 2FA.

**Flusso**:

1. Utente invia credenziali (email + password)
2. Sistema valida credenziali contro database
3. Se 2FA attivo → ritorna temp session per verificaverifica OTP
4. Se no 2FA → crea sessione completa
5. Imposta cookie httpOnly con session token
6. Ritorna dati utente e expiration time

**Endpoint API**: `POST /api/v1/auth/login`

**Backend**:

- **File**: [AuthenticationEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AuthenticationEndpoints.cs#L77-L128)
- **Handler**: `LoginCommandHandler`
- **Command**: `Api.BoundedContexts.Authentication.Application.Commands.LoginCommand`
- **Services**: `PasswordHasher`, `SessionService`, `TwoFactorService`
- **Helpers**: `CookieHelpers`

**Frontend**:

- **Pagina**: [apps/web/src/app/login/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/login/page.tsx)
- **Componenti**: LoginForm con supporto 2FA
- **API Client**: fetch to `/api/v1/auth/login`

---

### 1.3 Logout

**Descrizione**: Termina la sessione utente corrente.

**Flusso**:

1. Sistema legge session token dal cookie
2. Marca la sessione come revocata nel DB
3. Rimuove i cookie di sessione
4. Ritorna conferma

**Endpoint API**: `POST /api/v1/auth/logout`

**Backend**:

- **File**: [AuthenticationEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AuthenticationEndpoints.cs#L130-L148)
- **Handler**: `LogoutCommandHandler`
- **Command**: `Api.BoundedContexts.Authentication.Application.Commands.LogoutCommand`

**Frontend**:

- **Hook**: Logout button in navigation/header
- **Redirect**: Reindirizza a `/login` dopo logout

---

### 1.4 API Key Authentication

**Descrizione**: Autenticazione tramite API Key per accesso programmatico.

**Flusso**:

1. Client invia API key
2. Sistema valida key contro database
3. Se valida → imposta cookie protetto con API key
4. Ritorna profilo utente associato

**Endpoint API**: `POST /api/v1/auth/apikey/login`

**Backend**:

- **File**: [AuthenticationEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AuthenticationEndpoints.cs#L152-L211)
- **Handler**: `LoginWithApiKeyCommandHandler`
- **Command**: `Api.BoundedContexts.Authentication.Application.Commands.LoginWithApiKeyCommand`
- **Service**: `ApiKeyCookieService` (data protection)

**Frontend**:

- **Pagina**: Sezione in [settings/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/settings/page.tsx) per gestire API keys

---

### 1.5 Session Management

**Descrizione**: Gestione completa delle sessioniutente (estendi, revoca, lista).

**Flussi**:

#### 1.5.1 Get Session Status

- **Endpoint**: `GET /api/v1/auth/session/status`
- **Ritorna**: ExpiresAt, LastSeenAt, RemainingMinutes

#### 1.5.2 Extend Session

- **Endpoint**: `POST /api/v1/auth/session/extend`
- **Azione**: Estende la sessione di 30 giorni (default)
- **Handler**: `ExtendSessionCommandHandler`

#### 1.5.3 Get User Sessions

- **Endpoint**: `GET /api/v1/users/me/sessions`
- **Ritorna**: Lista di tutte le sessioni attive dell'utente
- **Query**: `GetUserSessionsQuery`

#### 1.5.4 Revoke Session

- **Endpoint**: `POST /api/v1/auth/sessions/{sessionId}/revoke`
- **Azione**: Revoca una singola sessione
- **Command**: `RevokeSessionCommand`

#### 1.5.5 Logout All Devices

- **Endpoint**: `POST /api/v1/auth/sessions/revoke-all`
- **Azione**: Revoca tutte le sessioni (opzionalmente esclusa la corrente)
- **Command**: `LogoutAllDevicesCommand`
- **Payload**: `{ includeCurrentSession: boolean, password?: string }`

**Backend**:

- **File**: [AuthenticationEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AuthenticationEndpoints.cs#L249-L546)
- **Handlers**: In `Api/BoundedContexts/Authentication/Application/`
- **Entities**: `UserSession` table in PostgreSQL

**Frontend**:

- **Pagina**: [sessions/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/sessions/page.tsx)
- **Componenti**: Lista sessioni con azioni revoca/extend

---

### 1.6 Two-Factor Authentication (2FA)

**Descrizione**: Gestione autenticazione a due fattori (setup, verify, disable).

**Endpoints**:

- `POST /api/v1/auth/2fa/setup` - Genera QR code per setup
- `POST /api/v1/auth/2fa/verify` - Verifica codice OTP
- `POST /api/v1/auth/2fa/disable` - Disabilita 2FA

**Backend**:

- **File**: [TwoFactorEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/TwoFactorEndpoints.cs)
- **Services**: `TwoFactorService`, OTP generation library

**Frontend**:

- **Componenti**: Modal per setup 2FA con QR code scanner

---

### 1.7 Password Reset

**Descrizione**: Flusso di reset password via email.

**Flusso**:

1. Utente richiede reset con email
2. Sistema genera token univoco
3. Invia email con link di reset
4. Utente clicca link e inserisce nuova password
5. Sistema valida token e aggiorna password

**Endpoints**:

- `POST /api/v1/auth/password/reset-request` - Richiede reset
- `POST /api/v1/auth/password/reset` - Completa reset con token

**Backend**:

- **File**: [PasswordEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/PasswordEndpoints.cs)
- **Services**: `EmailService`, `PasswordHasher`
- **Entities**: `PasswordResetToken`

**Frontend**:

- **Pagina**: [reset-password/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/reset-password/page.tsx)

---

### 1.8 OAuth Integration

**Descrizione**: Login tramite provider OAuth (Google, GitHub, etc.).

**Flusso**:

1. Redirect a provider OAuth
2. Callback con authorization code
3. Sistema valida e crea/aggiorna utente
4. Crea sessione e imposta cookie

**Endpoints**:

- OAuth providers configurabili

**Backend**:

- **File**: [OAuthEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/OAuthEndpoints.cs)
- **Callback**: [oauth-callback/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/oauth-callback/page.tsx)

---

## 2. DOCUMENT PROCESSING - Gestione PDF

### 2.1 Upload PDF Standard

**Descrizione**: Upload di un PDF tramite form multipart.

**Flusso**:

1. Utente seleziona file PDF e metadata (gameId, language, version)
2. Frontend invia multipart/form-data
3. Backend valida file (tipo, dimensione)
4. Salva file in MinIO blob storage
5. Crea record `PdfDocument` in database
6. Triggera processing asincrono (estrazione testo + indexing)
7. Ritorna documentId

**Endpoint API**: `POST /api/v1/ingest/pdf`

**Backend**:

- **File**: [PdfEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/PdfEndpoints.cs#L198-L247)
- **Handler**: `UploadPdfCommandHandler`
- **Command**: `Api.BoundedContexts.DocumentProcessing.Application.Commands.UploadPdfCommand`
- **Services**:
  - `IBlobStorageService` (MinIO)
  - `IFeatureFlagService` (CONFIG-05: verifica se upload abilitato)
  - `IBackgroundTaskService` (processing asincrono)
- **Entities**: `PdfDocument`, `Game`

**Frontend**:

- **Pagina**: [upload/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/upload/page.tsx)
- **Componenti**: Drag-and-drop file uploader con progress bar
- **Validazione**: Client-side validation per file type e size

---

### 2.2 Chunked Upload (Large Files)

**Descrizione**: Upload di file grandi tramite chunks per affidabilità.

**Flusso**:

1. **Init**: Client chiama `/init` con metadata → riceve sessionId e chunk configuration
2. **Upload Chunks**: Client carica chunks sequenziali via `/chunk`
3. **Status**: Opzionale, client può monitorare progress via `/status`
4. **Complete**: Client chiama `/complete` → backend assembla file e triggera processing

**Endpoints**:

- `POST /api/v1/ingest/pdf/chunked/init`
- `POST /api/v1/ingest/pdf/chunked/chunk`
- `GET /api/v1/ingest/pdf/chunked/{sessionId}/status`
- `POST /api/v1/ingest/pdf/chunked/complete`

**Backend**:

- **File**: [PdfEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/PdfEndpoints.cs#L55-L398)
- **Commands**: `InitChunkedUploadCommand`, `UploadChunkCommand`, `CompleteChunkedUploadCommand`
- **Query**: `GetChunkedUploadStatusQuery`
- **Storage**: Chunks temporanei in MinIO, assemblati al complete

**Frontend**:

- Stesso componente upload con logica chunking per file > 10MB

---

### 2.3 List PDFs per Game

**Descrizione**: Recupera tutti i PDF associati a un gioco.

**Endpoint API**: `GET /api/v1/games/{gameId}/pdfs`

**Backend**:

- **Query**: `GetPdfDocumentsByGameQuery`
- **Ritorna**: Array di PDF documents con metadata

**Frontend**:

- **Pagina**: Sezione in [games/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/games/page.tsx) o game detail page

---

### 2.4 Get PDF Text

**Descrizione**: Recupera il testo estratto da un PDF.

**Endpoint API**: `GET /api/v1/pdfs/{pdfId}/text`

**Backend**:

- **Query**: `GetPdfTextQuery`
- **Ritorna**: Testo estratto con metadata

---

### 2.5 Download PDF

**Descrizione**: Download/view del file PDF originale.

**Flusso**:

1. Client richiede PDF
2. Sistema verifica autorizzazione (owner o admin)
3. Recupera file da MinIO
4. Stream file al client con range support

**Endpoint API**: `GET /api/v1/pdfs/{pdfId}/download`

**Backend**:

- **File**: [PdfEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/PdfEndpoints.cs#L460-L498)
- **Authorization**: Row-Level Security (RLS) - solo owner o admin
- **Service**: `IBlobStorageService`

**Frontend**:

- Link download in lista PDF

---

### 2.6 Delete PDF

**Descrizione**: Eliminazione PDF con audit log.

**Flusso**:

1. Verifica autorizzazione (owner o admin)
2. Elimina file da MinIO
3. Elimina chunks da Qdrant (se indicizzato)
4. Elimina record da database
5. Log audit dell'operazione

**Endpoint API**: `DELETE /api/v1/pdf/{pdfId}`

**Backend**:

- **File**: [PdfEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/PdfEndpoints.cs#L500-L548)
- **Command**: `DeletePdfCommand`
- **Services**: `AuditService`, `IBlobStorageService`, `IVectorDbService`
- **RLS**: Row-Level Security enforcement

---

### 2.7 PDF Processing

#### 2.7.1 Get Processing Progress

**Endpoint**: `GET /api/v1/pdfs/{pdfId}/progress`
**Ritorna**: Status, percentage, current phase

#### 2.7.2 Cancel Processing

**Endpoint**: `DELETE /api/v1/pdfs/{pdfId}/processing`
**Azione**: Cancella processing in corso

#### 2.7.3 Index PDF (Manual)

**Endpoint**: `POST /api/v1/ingest/pdf/{pdfId}/index`
**Azione**: Forza re-indexing per semantic search
**Permessi**: Admin/Editor only

#### 2.7.4 Extract Text (Manual)

**Endpoint**: `POST /api/v1/ingest/pdf/{pdfId}/extract`
**Azione**: Ri-estrae testo da PDF bloccato
**Permessi**: Admin/Editor only

**Backend**:

- **File**: [PdfEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/PdfEndpoints.cs#L655-L832)
- **Services**: `EnhancedPdfProcessingOrchestrator`, `IBackgroundTaskService`

---

### 2.8 Set PDF Visibility

**Descrizione**: Imposta visibilità PDF nella libreria pubblica.

**Endpoint API**: `PATCH /api/v1/pdfs/{pdfId}/visibility`

**Payload**: `{ "isPublic": boolean }`

**Backend**:

- **Command**: `SetPdfVisibilityCommand`
- **Permessi**: Admin/Owner only

---

## 3. GAME MANAGEMENT - Gestione Giochi

### 3.1 List Games

**Descrizione**: Lista paginata di tutti i giochi con ricerca.

**Endpoint API**: `GET /api/v1/games?search={query}&page={n}&pageSize={size}`

**Backend**:

- **File**: [GameEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/GameEndpoints.cs#L145-L160)
- **Query**: `GetAllGamesQuery`
- **Parametri**: search (optional), page, pageSize

**Frontend**:

- **Pagina**: [games/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/games/page.tsx)
- **Componenti**: Search bar, lista games con pagination, card per ogni gioco

---

### 3.2 Get Game Details

**Descrizione**: Dettagli completi di un gioco con metadata e statistiche.

**Endpoint API**: `GET /api/v1/games/{id}/details`

**Backend**:

- **Query**: `GetGameDetailsQuery`
- **Ritorna**: Game metadata, PDFs count, sessions count, rules count

**Frontend**:

- **Pagina**: Game detail page con tabs (Info, Rules, PDFs, Sessions)

---

### 3.3 Get Game Rules

**Descrizione**: Recupera le regole (RuleSpec) associate a un gioco.

**Endpoint API**: `GET /api/v1/games/{id}/rules`

**Backend**:

- **Query**: `GetRuleSpecsQuery`

**Frontend**:

- Tab "Rules" nella game detail page

---

### 3.4 Create Game

**Descrizione**: Creazione nuovo gioco (Admin/Editor only).

**Endpoint API**: `POST /api/v1/games`

**Payload**:

```json
{
  "title": "string",
  "publisher": "string",
  "yearPublished": 2024,
  "minPlayers": 2,
  "maxPlayers": 4,
  "minPlayTimeMinutes": 30,
  "maxPlayTimeMinutes": 60,
  "iconUrl": "string",
  "imageUrl": "string",
  "bggId": 12345
}
```

**Backend**:

- **File**: [GameEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/GameEndpoints.cs#L208-L235)
- **Command**: `CreateGameCommand`
- **Permessi**: RequireAdminOrEditorSession

**Frontend**:

- **Pagina**: [editor/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/editor/page.tsx) (Admin panel)

---

### 3.5 Update Game

**Descrizione**: Aggiornamento metadata gioco.

**Endpoint API**: `PUT /api/v1/games/{id}`

**Backend**:

- **Command**: `UpdateGameCommand`
- **Permessi**: Admin/Editor only

---

### 3.6 Game Sessions Management

#### 3.6.1 Start Game Session

**Endpoint**: `POST /api/v1/sessions`
**Payload**: `{ gameId, players: [{ playerName, color, order }] }`
**Command**: `StartGameSessionCommand`

#### 3.6.2 Add Player to Session

**Endpoint**: `POST /api/v1/sessions/{id}/players`
**Command**: `AddPlayerToSessionCommand`

#### 3.6.3 Complete Session

**Endpoint**: `POST /api/v1/sessions/{id}/complete`
**Payload**: `{ winnerName?: string }`
**Command**: `CompleteGameSessionCommand`

#### 3.6.4 Pause/Resume Session

**Endpoints**:

- `POST /api/v1/sessions/{id}/pause`
- `POST /api/v1/sessions/{id}/resume`

#### 3.6.5 Abandon Session

**Endpoint**: `POST /api/v1/sessions/{id}/abandon`
**Command**: `AbandonGameSessionCommand`

#### 3.6.6 Get Session History

**Endpoint**: `GET /api/v1/sessions/history?gameId={id}&startDate={date}&endDate={date}`
**Query**: `GetSessionHistoryQuery`

#### 3.6.7 Get Session Statistics

**Endpoint**: `GET /api/v1/sessions/statistics?gameId={id}&topPlayersLimit={n}`
**Query**: `GetSessionStatsQuery`

**Backend**:

- **File**: [GameEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/GameEndpoints.cs#L63-L442)
- **Entities**: `GameSession`, `GameSessionPlayer`

**Frontend**:

- **Pagina**: [sessions/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/sessions/page.tsx)

---

### 3.7 Game FAQs

#### 3.7.1 Get FAQs for Game

**Endpoint**: `GET /api/v1/games/{gameId}/faqs?limit={n}&offset={n}`
**Query**: `GetGameFAQsQuery`

#### 3.7.2 Create FAQ

**Endpoint**: `POST /api/v1/games/{gameId}/faqs`
**Payload**: `{ question, answer }`
**Command**: `CreateGameFAQCommand`
**Permessi**: Admin/Editor

#### 3.7.3 Update FAQ

**Endpoint**: `PUT /api/v1/faqs/{id}`
**Command**: `UpdateGameFAQCommand`

#### 3.7.4 Delete FAQ

**Endpoint**: `DELETE /api/v1/faqs/{id}`
**Command**: `DeleteGameFAQCommand`

#### 3.7.5 Upvote FAQ

**Endpoint**: `POST /api/v1/faqs/{id}/upvote`
**Command**: `UpvoteGameFAQCommand`

**Backend**:

- **File**: [GameEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/GameEndpoints.cs#L444-L530)

---

### 3.8 BoardGameGeek (BGG) Integration

#### 3.8.1 Search BGG Games

**Endpoint**: `GET /api/v1/bgg/search?q={query}&exact={boolean}`
**Query**: `SearchBggGamesQuery`

#### 3.8.2 Get BGG Game Details

**Endpoint**: `GET /api/v1/bgg/games/{bggId}`
**Query**: `GetBggGameDetailsQuery`

**Backend**:

- **File**: [PdfEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/PdfEndpoints.cs#L83-L93) e [AiEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs#L70-L77)
- **Service**: `IBggApiService`

**Frontend**:

- Search BGG integrato in creazione giochi

---

## 4. KNOWLEDGE BASE & AI AGENTS

### 4.1 QA Agent (RAG Question Answering)

**Descrizione**: Agente AI per rispondere a domande sulle regole di gioco usando RAG.

**Flusso**:

1. User invia domanda per un game specifico
2. Sistema cerca chunks rilevanti in Qdrant (hybrid search)
3. Costruisce prompt con contesto retrieved
4. Chiama LLM per generare risposta
5. Calcola confidence score quality
6. Opzionalmente genera follow-up questions
7. Logga request in telemetry

**Endpoints**:

- `POST /api/v1/agents/qa` - Risposta sincrona
- `POST /api/v1/agents/qa/stream` - Risposta SSE streaming

**Payload**:

```json
{
  "gameId": "uuid",
  "query": "string",
  "searchMode": "Hybrid|Semantic|Keyword",
  "chatId": "uuid?",
  "documentIds": ["uuid"]?
}
```

**Backend**:

- **File**: [AiEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs#L51-L406)
- **Services**:
  - `IRagService` - Core RAG logic
  - `IResponseQualityService` - Quality scoring
  - `IVectorDbService` (Qdrant)
  - `ILlmService` (chiamate a LLM providers)
- **Queries**: `StreamQaQuery` (streaming), direct service call (sync)
- **Commands**: `LogAiRequestCommand` (telemetry)

**Frontend**:

- **Pagina**: [board-game-ai/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/board-game-ai/page.tsx) o [chat/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/chat/page.tsx)
- **Componenti**: Chat interface con streaming updates
- **EventSource**: SSE client per streaming endpoint

---

### 4.2 Explain Agent

**Descrizione**: Genera spiegazioni dettagliate su topics specifici.

**Flusso**:

1. User richiede spiegazione di un topic
2. Sistema cerca contesto rilevante
3. Genera script completo con LLM
4. Calcola estimated reading time
5. Ritorna script formattato

**Endpoints**:

- `POST /api/v1/agents/explain` - Sync
- `POST /api/v1/agents/explain/stream` - SSE streaming

**Backend**:

- **File**: [AiEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs#L408-L452)
- **Service**: `IRagService.ExplainAsync()`
- **Query**: `StreamExplainQuery`

---

### 4.3 Setup Guide Agent

**Descrizione**: Genera guida passo-passo per il setup di un gioco.

**Flusso**:

1. User richiede setup guide per gameId
2. Sistema recupera regole e setup info
3. LLM genera steps sequenziali
4. Stream steps al client via SSE
5. Include estimated time per step

**Endpoint**: `POST /api/v1/agents/setup` (SSE streaming)

**Backend**:

- **File**: [AiEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs#L603-L685)
- **Query**: `StreamSetupGuideQuery`
- **Feature Flag**: `Features.SetupGuideGeneration`

**Frontend**:

- **Pagina**: [setup/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/setup/page.tsx)
- **UI**: Step-by-step wizard con progress indicator

---

### 4.4 Chess Agent

**Descrizione**: Agente conversazionale per scacchi con analisi posizioni FEN.

**Flusso**:

1. User fa domanda sugli scacchi (opzionalmente con posizione FEN)
2. Sistema cerca knowledge base scacchi
3. Se FEN presente → analizza posizione
4. Genera risposta con suggerimenti di mosse
5. Mantiene context tramite chatId

**Endpoint**: `POST /api/v1/agents/chess`

**Payload**:

```json
{
  "question": "string",
  "fenPosition": "string?",
  "chatId": "uuid?"
}
```

**Backend**:

- **File**: [AiEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs#L722-L757)
- **Command**: `InvokeChessAgentCommand`

**Frontend**:

- **Pagina**: [chess/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/chess/page.tsx)
- **Componenti**: Chessboard component con chat interface

---

### 4.5 Chess Knowledge Management

#### 4.5.1 Index Chess Knowledge

**Endpoint**: `POST /api/v1/chess/index`
**Command**: `IndexChessKnowledgeCommand`
**Permessi**: Admin only
**Azione**: Indicizza knowledge base scacchi in Qdrant

#### 4.5.2 Search Chess Knowledge

**Endpoint**: `GET /api/v1/chess/search?q={query}&limit={n}`
**Query**: `SearchChessKnowledgeQuery`

#### 4.5.3 Delete Chess Knowledge

**Endpoint**: `DELETE /api/v1/chess/index`
**Command**: `DeleteChessKnowledgeCommand`
**Permessi**: Admin only

**Backend**:

- **File**: [AiEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs#L515-L601)

---

### 4.6 Agent Feedback

**Descrizione**: Raccolta feedback su risposte AI (thumbs up/down).

**Endpoint**: `POST /api/v1/agents/feedback`

**Payload**:

```json
{
  "userId": "uuid",
  "messageId": "uuid",
  "endpoint": "qa|explain|chess|setup",
  "outcome": "positive|negative|null",
  "gameId": "uuid?"
}
```

**Backend**:

- **File**: [AiEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AiEndpoints.cs#L687-L720)
- **Command**: `ProvideAgentFeedbackCommand`

**Frontend**:

- Thumbs up/down buttons in chat messages

---

## 5. ADMINISTRATION

### 5.1 Analytics & Telemetry

**Descrizione**: Visualizzazione metriche uso AI e sistema.

**Endpoints definiti in**:

- [AnalyticsEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AnalyticsEndpoints.cs)
- [LlmAnalyticsEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/LlmAnalyticsEndpoints.cs)

**Funzionalità**:

- Usage stats per utente/game/endpoint
- Token consumption tracking
- Quality metrics (confidence scores)
- Cost estimation

**Frontend**:

- **Pagina**: [admin/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/admin/page.tsx)
- **Charts**: Dashboard con grafici analitici

---

### 5.2 User Management

**Endpoints in**: [AdminUserEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AdminUserEndpoints.cs)

**Funzionalità**:

- Lista utenti con filtri
- Modifica ruoli utente
- Disabilitazione utenti
- Reset quote

---

### 5.3 API Key Management

**Endpoints in**: [ApiKeyEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/ApiKeyEndpoints.cs)

**Funzionalit**:

- Creazione API keys con scopes
- Lista keys per utente
- Revoca keys
- Rotation

---

### 5.4 Audit Log

**Endpoints in**: [AuditEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AuditEndpoints.cs)

**Funzionalità**:

- Query audit logs con filtri
- Export logs
- Retention policies

---

## 6. SYSTEM CONFIGURATION

### 6.1 Feature Flags

**Endpoints in**: [FeatureFlagEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/FeatureFlagEndpoints.cs)

**Funzionalità**:

- Get all feature flags
- Update feature flag state
- Feature-specific toggles

**Feature Flags Chiave**:

- `Features.PdfUpload`
- `Features.StreamingResponses`
- `Features.SetupGuideGeneration`

---

### 6.2 Configuration Management

**Endpoints in**: [ConfigurationEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/ConfigurationEndpoints.cs)

**Funzionalità**:

- Get/Set configuration values
- Dynamic config reload
- Environment-specific configs

---

### 6.3 Cache Management

**Endpoints in**: [CacheEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/CacheEndpoints.cs)

**Funzionalità**:

- Clear cache (per key pattern)
- Cache statistics
- TTL management

---

## 7. USER NOTIFICATIONS

### 7.1 Alerts & Notifications

**Endpoints in**:

- [AlertEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AlertEndpoints.cs)
- [AlertConfigEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/AlertConfigEndpoints.cs)
- [NotificationEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/NotificationEndpoints.cs)

**Funzionalità**:

- Sistema notifiche real-time
- Configurazione alert per eventi
- Preference utente per notifiche

---

## 8. WORKFLOW INTEGRATION

### 8.1 n8n Workflow Integration

**Endpoints in**: [WorkflowEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/WorkflowEndpoints.cs)

**Funzionalità**:

- Trigger workflows da eventi app
- Webhook handlers per n8n
- Template management

**Frontend**:

- **Pagina**: [n8n/page.tsx](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/web/src/app/n8n/page.tsx)

---

## 9. MONITORING & REPORTING

### 9.1 Monitoring Endpoints

**File**: [MonitoringEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/MonitoringEndpoints.cs)

**Endpoints**:

- Health checks
- System metrics (CPU, memory, DB connections)
- Service status

---

### 9.2 Reporting

**File**: [ReportingEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/ReportingEndpoints.cs)

**Funzionalità**:

- Generate reports (usage, performance, costs)
- Scheduled reports
- Export formats (PDF, CSV, JSON)

---

## 10. MISCELLANEOUS

### 10.1 Share Links

**File**: [ShareLinkEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/ShareLinkEndpoints.cs)

**Funzionalità**:

- Creazione link condivisibili per chat/risposte
- Gestione permessi share link
- Tracking accessi

---

### 10.2 Rule Spec Management

**File**: [RuleSpecEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/RuleSpecEndpoints.cs)

**Funzionalità**:

- CRUD regole gioco strutturate
- Generazione RuleSpec da PDF via AI
- Validazione regole

**Endpoint Chiave**: `POST /api/v1/ingest/pdf/{pdfId}/rulespec` - Genera RuleSpec da PDF

---

### 10.3 Test & Demo Endpoints

**Files**:

- [TestEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/TestEndpoints.cs)
- [TelemetryTestEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/TelemetryTestEndpoints.cs)
- [TestTelemetryEndpoints.cs](file:///d:/Repositories/meepleai-monorepo-frontend/meepleai-monorepo/apps/api/src/Api/Routing/TestTelemetryEndpoints.cs)

**Uso**: Development e testing only

---

## ARCHITETTURA FRONTEND (Next.js)

### Pagine Principali

| Pagina        | Path             | Descrizione                |
| ------------- | ---------------- | -------------------------- |
| Home          | `/`              | Landing page               |
| Login         | `/login`         | Autenticazione             |
| Register      | `/register`      | Registrazione              |
| Dashboard     | `/dashboard`     | Home utente autenticato    |
| Games         | `/games`         | Lista giochi               |
| Upload        | `/upload`        | Upload PDF                 |
| Chat          | `/chat`          | Chat AI/QA                 |
| Board Game AI | `/board-game-ai` | Interfaccia RAG principale |
| Chess         | `/chess`         | Scacchi AI agent           |
| Setup         | `/setup`         | Setup guide wizard         |
| Sessions      | `/sessions`      | Gestione sessioni gioco    |
| Settings      | `/settings`      | Impostazioni utente        |
| Admin         | `/admin`         | Admin panel                |
| Editor        | `/editor`        | Game editor                |
| n8n           | `/n8n`           | Workflow integration       |

### Componenti Chiave

Organizzati in `apps/web/src/components/`:

- **Layout**: Header, Footer, Navigation
- **Auth**: LoginForm, RegisterForm, SessionManager
- **Games**: GameCard, GameList, GameDetail
- **PDF**: PdfUploader, PdfList, ProcessingStatus
- **Chat**: ChatInterface, MessageList, StreamingHandler
- **Admin**: Analytics Charts, User Management, System Status

### State Management

- **React Context**: User authentication state
- **SWR / React Query**: Server state caching
- **Local State**: Component-specific con useState/useReducer

### API Client

Pattern utilizzato:

```typescript
// Esempio chiamata API
const response = await fetch("/api/v1/agents/qa", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ gameId, query }),
  credentials: "include", // Important per cookies
});
```

---

## PATTERN DI SICUREZZA

### Autenticazione

- **Session-based**: HttpOnly cookies con secure flag
- **API Key**: Per accesso programmatico
- **Dual auth**: Endpoints supportano session OR API key

### Autorizzazione

- **Role-based**: `User`, `Editor`, `Admin`
- **Row-Level Security**: Owner-based access control
- **Middleware**: `RequireSession()`, `RequireAdminSession()`, etc.

### Audit

- Tutte le operazioni sensibili loggano in `AuditLog`
- Tracking: UserId, Action, Entity, Status, Timestamp

---

## PATTERN TELEMETRY

Ogni richiesta AI logga:

- `UserId`, `GameId`, `Endpoint`, `Query`
- `LatencyMs`, `TokenCount` (prompt + completion)
- `Confidence`, `Status`, `ErrorMessage`
- `IpAddress`, `UserAgent`
- Quality scores (RAG confidence, LLM confidence, citation quality)

Command: `LogAiRequestCommand`

---

## FEATURE FLAGS CRITICI

| Flag                            | Scope                | Default |
| ------------------------------- | -------------------- | ------- |
| `Features.PdfUpload`            | Abilita upload PDF   | true    |
| `Features.StreamingResponses`   | SSE streaming per AI | true    |
| `Features.SetupGuideGeneration` | Setup guide agent    | true    |

---

## BOUNDED CONTEXTS - RIEPILOGO FILES

### Backend

```
apps/api/src/Api/
├── Routing/                           # Endpoints (35 files)
│   ├── AuthenticationEndpoints.cs     # Auth, Sessions, 2FA
│   ├── PdfEndpoints.cs                # PDF upload, processing
│   ├── GameEndpoints.cs               # Games, Sessions, FAQs
│   ├── AiEndpoints.cs                 # AI Agents (QA, Explain, Chess, Setup)
│   ├── AdminUserEndpoints.cs          # User management
│   ├── ApiKeyEndpoints.cs             # API keys
│   ├── AnalyticsEndpoints.cs          # Analytics
│   └── ...                            # Altri 28 endpoints
│
└── BoundedContexts/                   # DDD Bounded Contexts
    ├── Authentication/
    │   ├── Application/
    │   │   ├── Commands/              # RegisterCommand, LoginCommand, etc.
    │   │   ├── Queries/               # GetSessionStatusQuery, etc.
    │   │   └── DTOs/                  # UserDto, SessionStatusDto
    │   ├── Domain/                    # Entities, ValueObjects
    │   └── Infrastructure/            # Repositories, Services
    │
    ├── DocumentProcessing/
    │   ├── Application/
    │   │   ├── Commands/              # UploadPdfCommand, IndexPdfCommand
    │   │   └── Queries/               # GetPdfDocumentsByGameQuery
    │   └── Infrastructure/
    │       └── External/              # MinIO, Unstructured service clients
    │
    ├── GameManagement/
    │   ├── Application/
    │   │   ├── Commands/              # CreateGameCommand, StartGameSessionCommand
    │   │   └── Queries/               # GetAllGamesQuery, GetGameDetailsQuery
    │   └── Domain/                    # Game, GameSession entities
    │
    ├── KnowledgeBase/
    │   ├── Application/
    │   │   ├── Commands/              # InvokeChessAgentCommand, IndexChessKnowledgeCommand
    │   │   └── Queries/               # StreamQaQuery, StreamExplainQuery
    │   └── Infrastructure/
    │       └── Services/              # RAGService, VectorDbService
    │
    ├── Administration/                # Analytics, Audit, Telemetry
    ├── SystemConfiguration/           # Feature flags, Config
    ├── UserNotifications/             # Alerts, Notifications
    └── WorkflowIntegration/           # n8n workflows
```

### Frontend

```
apps/web/src/
├── app/                               # Next.js App Router pages (23 pages)
│   ├── login/page.tsx
│   ├── register/page.tsx
│   ├── games/page.tsx
│   ├── upload/page.tsx
│   ├── board-game-ai/page.tsx
│   ├── chat/page.tsx
│   ├── chess/page.tsx
│   ├── setup/page.tsx
│   ├── sessions/page.tsx
│   ├── admin/page.tsx
│   └── ...
│
└── components/                        # Componenti riutilizzabili
    ├── layout/                        # Header, Footer, Nav
    ├── auth/                          # Login, Register forms
    ├── games/                         # Game cards, lists
    ├── pdf/                           # Upload, processing UI
    ├── chat/                          # Chat interface, streaming
    └── admin/                         # Admin components
```

---

## CONCLUSIONE

L'applicazione MeepleAI è una piattaforma complessa per la gestione di giochi da tavolo con forte integrazione AI tramite RAG. L'architettura DDD/CQRS garantisce:

✅ **Separazione delle responsabilità** tramite bounded contexts  
✅ **Scalabilità** tramite CQRS e processing asincrono  
✅ **Testabilità** con handlers isolati  
✅ **Sicurezza** con RLS, audit log, e dual authentication  
✅ **Monitoring** completo con telemetria AI e analytics

Tutte le funzionalità web sono mappate con:

- **Descrizione** chiara
- **Flusso** di esecuzione
- **Endpoints** API
- **Classi backend** (Commands, Queries, Handlers, Services)
- **Componenti frontend** (Pages, Components)
