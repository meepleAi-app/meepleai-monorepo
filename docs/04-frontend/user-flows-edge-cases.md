# User Flows - Edge Cases & Error States

**Versione**: 1.0
**Data**: 2025-12-14
**Parte di**: user-flows-comprehensive.md

---

## Indice

1. [Error States Autenticazione](#1-error-states-autenticazione)
2. [Error States Chat RAG](#2-error-states-chat-rag)
3. [Error States Document Processing](#3-error-states-document-processing)
4. [Network Errors](#4-network-errors)
5. [Permission Errors](#5-permission-errors)
6. [Rate Limiting](#6-rate-limiting)
7. [Recovery Strategies](#7-recovery-strategies)

---

## 1. Error States Autenticazione

### 1.1 Login Errors

```
Login Flow → Error Scenarios:

┌────────────────────────────────┐
│  POST /api/v1/auth/login       │
└────────┬───────────────────────┘
         │
    ┌────┴────┐
    │  Error? │
    └────┬────┘
         │
    ┌────┴──────────────────────────────────────────────┐
    │                                                    │
    ├── 401 Unauthorized                                │
    │   ├── Email not found → "Email non trovata"       │
    │   ├── Wrong password → "Password errata"          │
    │   └── Account locked → "Account bloccato"         │
    │                                                    │
    ├── 403 Forbidden                                   │
    │   └── Email not verified → "Verifica email prima" │
    │                                                    │
    ├── 429 Too Many Requests                           │
    │   └── Rate limit exceeded → "Troppi tentativi"    │
    │                                                    │
    ├── 500 Internal Server Error                       │
    │   └── Server error → "Errore server, riprova"     │
    │                                                    │
    └── Network Error                                   │
        └── Offline → "Connessione assente"             │
```

**UI Feedback**:
```typescript
// Error display in LoginForm
<Alert variant="destructive">
  <AlertCircle className="h-4 w-4" />
  <AlertTitle>Errore di accesso</AlertTitle>
  <AlertDescription>{errorMessage}</AlertDescription>
</Alert>
```

**Retry Logic**:
- Automatic retry: NO (user must manually retry)
- Exponential backoff: N/A
- Max retries: N/A (user-controlled)

### 1.2 Session Expiry

```
Scenario: User session expires during active use

┌────────────────────────────┐
│  User makes API call       │
│  (any authenticated route) │
└────────┬───────────────────┘
         │
         │ Session cookie expired
         ↓
┌────────────────────────────────────┐
│  Backend: 401 Unauthorized         │
│  { error: "Session expired" }      │
└────────┬───────────────────────────┘
         │
         │ Frontend intercepts
         ↓
┌────────────────────────────────────┐
│  httpClient middleware:            │
│  1. Clear auth state               │
│  2. Show session expiry modal      │
│  3. Redirect to /login after 5s    │
└────────────────────────────────────┘

Modal:
┌─────────────────────────────────┐
│  ⏱️  Sessione Scaduta          │
│                                 │
│  La tua sessione è scaduta.     │
│  Effettua nuovamente il login.  │
│                                 │
│  Redirect in: 5s                │
│  [Login Now]                    │
└─────────────────────────────────┘
```

**Implementation**:
```typescript
// httpClient.ts
if (response.status === 401) {
  // Clear auth state
  queryClient.setQueryData(['currentUser'], null);
  clearStoredApiKey();

  // Show modal
  toast.error('Sessione scaduta');

  // Redirect after delay
  setTimeout(() => {
    window.location.href = '/login?reason=session-expired';
  }, 5000);
}
```

### 1.3 2FA Verification Errors

```
2FA Verification → Error Scenarios:

┌────────────────────────────────┐
│  POST /api/v1/auth/2fa/verify  │
│  { tempToken, code }           │
└────────┬───────────────────────┘
         │
    ┌────┴────┐
    │  Error? │
    └────┬────┘
         │
    ┌────┴──────────────────────────────────────────────┐
    │                                                    │
    ├── 400 Bad Request                                 │
    │   ├── Invalid code → "Codice non valido"          │
    │   ├── Expired token → "Token scaduto"             │
    │   └── Code already used → "Codice già utilizzato" │
    │                                                    │
    ├── 401 Unauthorized                                │
    │   └── Wrong code → "Codice errato (X tentativi)"  │
    │                                                    │
    ├── 429 Too Many Requests                           │
    │   └── Too many attempts → "Troppi tentativi"      │
    │                                                    │
    └── 500 Internal Server Error                       │
        └── TOTP validation failed → "Errore sistema"   │
```

**Auto-lockout** (after 5 failed attempts):
```
Attempt count: 5
↓
Backend: Lock account for 15 minutes
↓
Return 429 with:
{
  error: "Account temporaneamente bloccato",
  retryAfter: 900 // seconds
}
↓
UI shows countdown:
"Account bloccato. Riprova tra: 14:59"
```

---

## 2. Error States Chat RAG

### 2.1 Streaming Errors

```
RAG Query → SSE Stream Errors:

┌────────────────────────────────┐
│  POST /api/v1/chat/messages    │
│  → SSE stream starts           │
└────────┬───────────────────────┘
         │
    ┌────┴────┐
    │  Error  │
    │ during  │
    │ stream? │
    └────┬────┘
         │
    ┌────┴──────────────────────────────────────────────┐
    │                                                    │
    ├── event: error                                    │
    │   data: { code: "RETRIEVAL_FAILED" }              │
    │   → "Impossibile recuperare contesto"             │
    │                                                    │
    ├── event: error                                    │
    │   data: { code: "LLM_TIMEOUT" }                   │
    │   → "Timeout LLM, riprova"                        │
    │                                                    │
    ├── event: error                                    │
    │   data: { code: "CONFIDENCE_TOO_LOW" }            │
    │   → "Risposta non affidabile (conf < 0.70)"       │
    │                                                    │
    ├── event: error                                    │
    │   data: { code: "HALLUCINATION_DETECTED" }        │
    │   → "Risposta potenzialmente inesatta"            │
    │                                                    │
    └── Connection lost                                 │
        → "Connessione persa, riprova"                  │
```

**UI Feedback**:
```typescript
// MessageList component
{streamError && (
  <Alert variant="destructive">
    <AlertCircle className="h-4 w-4" />
    <AlertDescription>
      {streamError.message}
      <Button
        variant="outline"
        size="sm"
        onClick={retryQuery}
      >
        Riprova
      </Button>
    </AlertDescription>
  </Alert>
)}
```

**Retry Strategy**:
```typescript
// Exponential backoff with jitter
const retryDelays = [1000, 2000, 4000]; // ms
let retryCount = 0;

async function retryQuery() {
  if (retryCount >= 3) {
    toast.error('Troppi tentativi, contatta il supporto');
    return;
  }

  const delay = retryDelays[retryCount] + Math.random() * 1000;
  await sleep(delay);
  retryCount++;
  sendMessage(originalQuery);
}
```

### 2.2 No Results Found

```
Scenario: RAG retrieval returns 0 relevant chunks

┌────────────────────────────────┐
│  Hybrid Retrieval (RRF)        │
│  Vector + Keyword search       │
└────────┬───────────────────────┘
         │
         │ Both return 0 results
         ↓
┌────────────────────────────────────────┐
│  Backend: NO_RESULTS_FOUND             │
│  {                                     │
│    message: "Nessun risultato",        │
│    suggestions: [                      │
│      "Verifica ortografia domanda",    │
│      "Prova domanda più generale",     │
│      "Consulta FAQ gioco"              │
│    ]                                   │
│  }                                     │
└────────┬───────────────────────────────┘
         │
         │ Frontend shows:
         ↓
┌─────────────────────────────────────────┐
│  🤖 MeepleAI:                           │
│                                         │
│  Non ho trovato informazioni            │
│  pertinenti nel manuale.                │
│                                         │
│  Suggerimenti:                          │
│  • Verifica l'ortografia della domanda │
│  • Prova una domanda più generale      │
│  • Consulta le FAQ del gioco            │
│                                         │
│  [Vedi FAQ] [Contatta supporto]        │
└─────────────────────────────────────────┘
```

### 2.3 Rate Limiting (User Tier)

```
Rate Limit Structure:

Free Tier:
├── 20 queries/hour
├── 100 queries/day
└── Max 5 concurrent streams

Pro Tier:
├── 100 queries/hour
├── 1000 queries/day
└── Max 20 concurrent streams

Admin:
└── Unlimited
```

**Error Response**:
```json
{
  "error": "RATE_LIMIT_EXCEEDED",
  "message": "Limite query raggiunto",
  "retryAfter": 3600,
  "currentUsage": {
    "hour": 20,
    "day": 89
  },
  "limits": {
    "hour": 20,
    "day": 100
  }
}
```

**UI Feedback**:
```
┌─────────────────────────────────────────┐
│  ⚠️  Limite Query Raggiunto             │
│                                         │
│  Hai raggiunto il limite di 20 query   │
│  all'ora per il piano Free.             │
│                                         │
│  Reset tra: 42:15                       │
│                                         │
│  [Upgrade to Pro] [Chiudi]             │
└─────────────────────────────────────────┘
```

---

## 3. Error States Document Processing

### 3.1 PDF Upload Errors

```
PDF Upload → Validation Errors:

┌────────────────────────────────┐
│  File selected for upload      │
└────────┬───────────────────────┘
         │
    ┌────┴────────────────────────────────────────────┐
    │  Client-side validation                         │
    ├──────────────────────────────────────────────────
    │                                                  │
    ├── MIME type != application/pdf                  │
    │   → ❌ "Solo file PDF accettati"                │
    │                                                  │
    ├── File size > 50MB                              │
    │   → ❌ "File troppo grande (max 50MB)"          │
    │                                                  │
    ├── Filename contains special chars               │
    │   → ❌ "Nome file non valido"                   │
    │                                                  │
    └── Corrupted file (magic number check)           │
        → ❌ "File corrotto"                           │


┌────────────────────────────────┐
│  POST /api/v1/documents/upload │
└────────┬───────────────────────┘
         │
    ┌────┴────────────────────────────────────────────┐
    │  Server-side validation                         │
    ├──────────────────────────────────────────────────
    │                                                  │
    ├── PDF page count > 200                          │
    │   → 400 "Troppe pagine (max 200)"               │
    │                                                  │
    ├── PDF password-protected                        │
    │   → 400 "PDF protetto da password"              │
    │                                                  │
    ├── Malformed PDF structure                       │
    │   → 400 "PDF malformato"                        │
    │                                                  │
    └── Duplicate PDF (hash collision)                │
        → 409 "PDF già caricato"                      │
```

**Recovery Actions**:
```typescript
interface UploadError {
  code: string;
  message: string;
  recoverable: boolean;
  action?: 'retry' | 'select_different' | 'contact_support';
}

// Example
{
  code: "FILE_TOO_LARGE",
  message: "File troppo grande (max 50MB)",
  recoverable: true,
  action: "select_different"
}
```

### 3.2 Extraction Pipeline Failures

```
3-Stage Pipeline → Failure Scenarios:

Stage 1: Unstructured (confidence < 0.80)
↓
Stage 2: SmolDocling (confidence < 0.70)
↓
Stage 3: Docnet (confidence < 0.50)
↓
ALL FAILED
↓
┌─────────────────────────────────────────┐
│  Extraction Failed                      │
│                                         │
│  Impossibile estrarre testo dal PDF.    │
│                                         │
│  Opzioni:                               │
│  1. Carica PDF di qualità migliore      │
│  2. Inserisci regole manualmente        │
│  3. Contatta supporto                   │
│                                         │
│  [Riprova] [Input Manuale] [Supporto]  │
└─────────────────────────────────────────┘
```

**Partial Success** (some pages failed):
```
Extraction Result:
├── Pages processed: 45/50
├── Success: 45 pages
├── Failed: 5 pages (23, 34, 41, 48, 50)
└── Confidence: 0.76 (acceptable)

UI:
┌─────────────────────────────────────────┐
│  ⚠️  Estrazione Parziale                │
│                                         │
│  45/50 pagine estratte con successo.    │
│                                         │
│  Pagine con problemi: 23, 34, 41, 48, 50│
│                                         │
│  Confidence: 76%                        │
│                                         │
│  [Pubblica Comunque] [Riprova] [Annulla]│
└─────────────────────────────────────────┘
```

---

## 4. Network Errors

### 4.1 Offline Detection

```
┌────────────────────────────────┐
│  window.addEventListener       │
│  ('offline', handleOffline)    │
└────────┬───────────────────────┘
         │
         │ Network connection lost
         ↓
┌─────────────────────────────────────────┐
│  Global Offline Banner (sticky top)     │
│  ┌─────────────────────────────────────┐│
│  │ 📡 Connessione assente              ││
│  │ Riconnettiti per continuare         ││
│  └─────────────────────────────────────┘│
└─────────────────────────────────────────┘
         │
         │ Queue pending requests
         ↓
┌─────────────────────────────────────────┐
│  Request Queue (IndexedDB)              │
│  • Pending messages                     │
│  • Failed uploads                       │
│  • Unsent feedback                      │
└─────────────────────────────────────────┘
         │
         │ window.addEventListener
         │ ('online', handleOnline)
         ↓
┌─────────────────────────────────────────┐
│  Reconnection Handler                   │
│  1. Show "Reconnecting..." toast        │
│  2. Retry queued requests               │
│  3. Refresh stale queries               │
│  4. Show success banner                 │
└─────────────────────────────────────────┘
```

### 4.2 Request Timeout

```
Timeout Configuration:
├── API calls: 30s
├── SSE streams: 60s
├── File uploads: 120s
└── Health checks: 5s

Timeout Handler:
┌────────────────────────────────┐
│  fetch(url, { timeout: 30000 })│
└────────┬───────────────────────┘
         │
         │ Timeout exceeded
         ↓
┌─────────────────────────────────────────┐
│  Error: TIMEOUT                         │
│  "Richiesta troppo lenta, riprova"      │
│                                         │
│  [Riprova] [Annulla]                    │
└─────────────────────────────────────────┘
```

**Exponential Backoff**:
```typescript
async function fetchWithRetry(url, options, maxRetries = 3) {
  let lastError;

  for (let i = 0; i < maxRetries; i++) {
    try {
      return await fetch(url, options);
    } catch (error) {
      lastError = error;

      if (error.name === 'AbortError') {
        // Timeout - retry with backoff
        const delay = Math.min(1000 * 2 ** i, 10000);
        await sleep(delay);
      } else {
        // Other error - don't retry
        throw error;
      }
    }
  }

  throw lastError;
}
```

---

## 5. Permission Errors

### 5.1 Role-Based Access Control

```
Permission Hierarchy:
Admin > Editor > User > Guest

Route Protection:
┌────────────────────────────────┐
│  User navigates to /admin      │
└────────┬───────────────────────┘
         │
    ┌────┴────┐
    │  Role?  │
    └────┬────┘
         │
    ┌────┴──────────────────────────────────┐
    │                                        │
    ├── Guest (no session)                  │
    │   → Redirect to /login                │
    │                                        │
    ├── User (role: 'User')                 │
    │   → 403 Forbidden                     │
    │   ┌─────────────────────────────────┐ │
    │   │ ⛔ Accesso Negato               │ │
    │   │ Non hai i permessi necessari    │ │
    │   │ [Torna alla Dashboard]          │ │
    │   └─────────────────────────────────┘ │
    │                                        │
    ├── Editor (role: 'Editor')             │
    │   → 403 Forbidden (admin only)        │
    │                                        │
    └── Admin (role: 'Admin')               │
        → ✅ Access granted                 │
```

### 5.2 Resource Ownership

```
Scenario: User tries to delete another user's chat thread

┌────────────────────────────────┐
│  DELETE /api/v1/chats/{id}     │
└────────┬───────────────────────┘
         │
         │ Backend validates ownership
         ↓
┌────────────────────────────────────────┐
│  Query DB:                             │
│  SELECT userId FROM ChatThreads        │
│  WHERE id = {id}                       │
│                                        │
│  Result: userId != currentUser.id      │
└────────┬───────────────────────────────┘
         │
         │ 403 Forbidden
         ↓
┌─────────────────────────────────────────┐
│  Response:                              │
│  {                                      │
│    error: "FORBIDDEN",                  │
│    message: "Non puoi eliminare chat   │
│              di altri utenti"           │
│  }                                      │
└─────────────────────────────────────────┘
```

---

## 6. Rate Limiting

### 6.1 Global Rate Limits

```
IP-based Rate Limiting (sliding window):

┌────────────────────────────────┐
│  Incoming Request              │
│  IP: 192.168.1.100             │
└────────┬───────────────────────┘
         │
         │ Check Redis counter
         ↓
┌────────────────────────────────────────┐
│  INCR rate:192.168.1.100:2025-12-14   │
│  EXPIRE 3600                           │
│                                        │
│  Current: 101                          │
│  Limit: 100/hour                       │
└────────┬───────────────────────────────┘
         │
         │ Limit exceeded
         ↓
┌─────────────────────────────────────────┐
│  429 Too Many Requests                  │
│  {                                      │
│    error: "RATE_LIMIT_EXCEEDED",        │
│    retryAfter: 2847,  // seconds        │
│    limit: 100,                          │
│    current: 101                         │
│  }                                      │
└─────────────────────────────────────────┘
         │
         │ Frontend shows:
         ↓
┌─────────────────────────────────────────┐
│  ⚠️  Troppi Tentativi                   │
│                                         │
│  Hai superato il limite di 100 richieste│
│  all'ora.                               │
│                                         │
│  Riprova tra: 47:27                     │
└─────────────────────────────────────────┘
```

### 6.2 Circuit Breaker

```
Circuit Breaker States:

CLOSED (normal operation)
↓ (failure rate > 50%)
OPEN (reject all requests)
↓ (after 60s cooldown)
HALF_OPEN (test 1 request)
↓
├─ Success → CLOSED
└─ Failure → OPEN

Implementation:
┌────────────────────────────────┐
│  Circuit Breaker Monitor       │
│  Window: 10s                   │
│  Threshold: 50% failures       │
└────────┬───────────────────────┘
         │
         │ 6/10 requests failed
         ↓
┌────────────────────────────────────────┐
│  State: CLOSED → OPEN                  │
│  Reason: High failure rate (60%)       │
│  Next check: 60s                       │
└────────────────────────────────────────┘
         │
         │ User requests during OPEN
         ↓
┌─────────────────────────────────────────┐
│  503 Service Unavailable                │
│  {                                      │
│    error: "CIRCUIT_OPEN",               │
│    message: "Servizio temporaneamente  │
│              non disponibile",          │
│    retryAfter: 58                       │
│  }                                      │
└─────────────────────────────────────────┘
```

---

## 7. Recovery Strategies

### 7.1 Optimistic UI Updates

```
Example: Delete Chat Thread

┌────────────────────────────────┐
│  User clicks "Delete"          │
└────────┬───────────────────────┘
         │
         │ Optimistic update
         ↓
┌────────────────────────────────────────┐
│  1. Immediately remove from UI         │
│  2. Show "Undo" toast (5s)             │
│  3. Send DELETE request                │
└────────┬───────────────────────────────┘
         │
    ┌────┴────┐
    │ Success?│
    └────┬────┘
         │
    ┌────┴────────────────────────┐
    │                              │
   YES                            NO
    │                              │
    │ Toast: "Chat eliminata"      │ Rollback:
    │                              │ 1. Re-add to UI
    │                              │ 2. Show error
    │                              │ 3. Offer retry
```

### 7.2 Request Deduplication

```
Scenario: User double-clicks "Send" button

┌────────────────────────────────┐
│  Click 1: Send message         │
│  Click 2: Send message (50ms)  │
└────────┬───────────────────────┘
         │
         │ httpClient deduplication
         ↓
┌────────────────────────────────────────┐
│  Request signature:                    │
│  SHA256(method + url + body)           │
│                                        │
│  Cache key: "POST:/chat/msg:abc123"    │
│  TTL: 5s                               │
└────────┬───────────────────────────────┘
         │
         │ Check cache
         ↓
┌────────────────────────────────────────┐
│  Cache HIT → Return cached promise     │
│  (blocks duplicate request)            │
└────────────────────────────────────────┘
```

### 7.3 Stale-While-Revalidate

```
TanStack Query Configuration:

{
  staleTime: 5 * 60 * 1000,     // 5 minutes
  cacheTime: 10 * 60 * 1000,    // 10 minutes
  refetchOnWindowFocus: true,
  refetchOnReconnect: true,
}

Flow:
┌────────────────────────────────┐
│  useGames() query              │
└────────┬───────────────────────┘
         │
         │ Data in cache?
         ↓
    ┌────┴────┐
    │   YES   │
    └────┬────┘
         │
    ┌────┴────────────────────────┐
    │                              │
 Stale?                        Fresh?
    │                              │
   YES                            NO
    │                              │
    │ 1. Return cached             │ Return cached
    │ 2. Fetch in background       │ (no fetch)
    │ 3. Update when ready         │
```

---

## Performance SLOs

```
Service Level Objectives:

API Response Times (P95):
├── GET requests: <500ms
├── POST requests: <1000ms
├── SSE streams (first token): <2000ms
└── File uploads: <5000ms

Availability:
├── Uptime: >99.5%
├── Error rate: <0.5%
└── Success rate (RAG): >98%

User Experience:
├── Time to Interactive (TTI): <2.5s
├── First Contentful Paint (FCP): <1.5s
├── Largest Contentful Paint (LCP): <2.5s
└── Cumulative Layout Shift (CLS): <0.1
```

---

## Monitoring & Alerts

```
Alert Triggers:

Critical (PagerDuty):
├── Error rate > 5% (5min window)
├── Latency P95 > 5s
├── Uptime < 99%
└── Circuit breaker OPEN

Warning (Slack):
├── Error rate > 1%
├── Latency P95 > 2s
├── Rate limit hit > 100 users/hour
└── PDF extraction failures > 20%

Info (Email):
├── New user registrations spike
├── Unusual traffic patterns
└── Feature flag changes
```

---

## Conclusioni

Questa documentazione copre i principali edge cases e stati di errore del sistema MeepleAI. Per dettagli implementativi specifici, consultare:

- `apps/web/src/lib/api/core/errors.ts` (Error handling)
- `apps/web/src/lib/api/core/retryPolicy.ts` (Retry logic)
- `apps/web/src/lib/api/core/circuitBreaker.ts` (Circuit breaker)
- `apps/web/src/lib/api/core/requestCache.ts` (Deduplication)

**Prossimi passi**:
1. Implementare E2E tests per tutti gli error scenarios
2. Aggiungere Sentry per error tracking in produzione
3. Implementare retry automatico intelligente per errori transitori
4. Aggiungere metriche Prometheus per tutti gli error states
