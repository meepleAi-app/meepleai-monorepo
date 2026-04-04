# API Documentation

**MeepleAI REST API** - Complete reference per integrazione backend

---

## Quick Access

| Resource | URL |
|----------|-----|
| **Scalar UI (Interactive)** | http://localhost:8080/scalar/v1 |
| **OpenAPI Spec** | http://localhost:8080/openapi/v1.json |
| **Health Check** | http://localhost:8080/health |
| **Metrics** | http://localhost:8080/metrics |

---

## Authentication

MeepleAI supporta **3 metodi di autenticazione**:

### 1. Cookie-Based Authentication

**Utilizzato da**: Frontend web application

**Flow**:
```
POST /api/v1/auth/login
  ↓
Set-Cookie: .AspNetCore.Identity.Application (httpOnly, secure)
  ↓
Requests auto-include cookie
```

**Example**:
```typescript
// Frontend (fetch with credentials)
const response = await fetch('http://localhost:8080/api/v1/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  credentials: 'include', // CRITICAL: include cookies
  body: JSON.stringify({ email, password })
});
```

### 2. API Key Authentication

**Utilizzato da**: External integrations, scripts, mobile apps

**Format**: `mpl_{env}_{base64}` (PBKDF2 hashed in DB)

**Example**:
```bash
curl -H "X-Api-Key: mpl_prod_abc123..." \
  http://localhost:8080/api/v1/games
```

**Generate API Key**:
```bash
POST /api/v1/auth/api-keys
Authorization: Bearer <your-jwt-or-cookie>
Content-Type: application/json

{
  "name": "Mobile App",
  "expiresAt": "2026-12-31T23:59:59Z"
}
```

### 3. OAuth 2.0

**Providers**: Google, Discord, GitHub

**Flow**:
```
GET /api/v1/auth/oauth/google
  ↓
Redirect to Google OAuth
  ↓
Callback: /api/v1/auth/oauth/callback/google
  ↓
Set cookie + return user
```

**Example**:
```typescript
// Redirect to OAuth provider
window.location.href = 'http://localhost:8080/api/v1/auth/oauth/google';
```

---

## API Endpoints Overview

### Authentication (`/api/v1/auth`)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/register` | Create new user account | None |
| POST | `/login` | Login with email/password | None |
| POST | `/logout` | Invalidate session | Cookie |
| GET | `/me` | Get current user profile | Cookie/API Key |
| POST | `/2fa/enable` | Enable TOTP 2FA | Cookie |
| POST | `/2fa/verify` | Verify TOTP code | Cookie |
| POST | `/api-keys` | Generate API key | Cookie |
| DELETE | `/api-keys/{id}` | Revoke API key | Cookie |
| GET | `/oauth/{provider}` | Start OAuth flow | None |
| GET | `/oauth/callback/{provider}` | OAuth callback | None |

### Games (`/api/v1/games`)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/` | List all games (paginated) | None |
| GET | `/{id}` | Get game by ID | None |
| POST | `/` | Create new game | Admin |
| PUT | `/{id}` | Update game | Admin |
| DELETE | `/{id}` | Delete game | Admin |
| GET | `/{id}/rules` | Get game rules (PDF links) | None |

### Knowledge Base (`/api/v1/chat`)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/` | Ask question (SSE streaming) | Cookie/API Key |
| GET | `/threads` | List chat threads | Cookie/API Key |
| GET | `/threads/{id}` | Get thread by ID | Cookie/API Key |
| DELETE | `/threads/{id}` | Delete thread | Cookie/API Key |

### Documents (`/api/v1/documents`)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/upload` | Upload PDF rulebook | Cookie/API Key |
| GET | `/` | List uploaded documents | Cookie/API Key |
| GET | `/{id}` | Get document metadata | Cookie/API Key |
| DELETE | `/{id}` | Delete document | Cookie/API Key |
| GET | `/{id}/status` | Get processing status | Cookie/API Key |

### Administration (`/api/v1/admin`)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/users` | List all users | Admin |
| GET | `/stats` | System statistics | Admin |
| GET | `/alerts` | Active alerts | Admin |
| POST | `/configuration` | Update config | Admin |
| GET | `/configuration` | Get all config keys | Admin |

---

## Request/Response Examples

### Register User

**Request**:
```http
POST /api/v1/auth/register HTTP/1.1
Host: localhost:8080
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "confirmPassword": "SecurePassword123!"
}
```

**Response** (201 Created):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "createdAt": "2026-01-01T10:00:00Z"
}
```

### Login

**Request**:
```http
POST /api/v1/auth/login HTTP/1.1
Host: localhost:8080
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response** (200 OK):
```http
HTTP/1.1 200 OK
Set-Cookie: .AspNetCore.Identity.Application=...; Path=/; HttpOnly; Secure; SameSite=Lax
Content-Type: application/json

{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "roles": ["User"]
}
```

### Ask Question (SSE Streaming)

**Request**:
```http
POST /api/v1/chat HTTP/1.1
Host: localhost:8080
Content-Type: application/json
Cookie: .AspNetCore.Identity.Application=...

{
  "question": "Come si gioca a Catan?",
  "gameId": "550e8400-e29b-41d4-a716-446655440000",
  "threadId": null
}
```

**Response** (200 OK - SSE Stream):
```http
HTTP/1.1 200 OK
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive

event: token
data: {"content":"Catan"}

event: token
data: {"content":" è"}

event: token
data: {"content":" un"}

event: done
data: {"confidence":0.92,"sources":["rules.pdf"]}
```

### Upload PDF

**Request**:
```http
POST /api/v1/documents/upload HTTP/1.1
Host: localhost:8080
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary
Cookie: .AspNetCore.Identity.Application=...

------WebKitFormBoundary
Content-Disposition: form-data; name="file"; filename="catan-rules.pdf"
Content-Type: application/pdf

<binary PDF data>
------WebKitFormBoundary
Content-Disposition: form-data; name="gameId"

550e8400-e29b-41d4-a716-446655440000
------WebKitFormBoundary--
```

**Response** (202 Accepted):
```json
{
  "documentId": "660e8400-e29b-41d4-a716-446655440001",
  "status": "Processing",
  "message": "Document uploaded successfully, processing started"
}
```

### Get Processing Status

**Request**:
```http
GET /api/v1/documents/660e8400-e29b-41d4-a716-446655440001/status HTTP/1.1
Host: localhost:8080
Cookie: .AspNetCore.Identity.Application=...
```

**Response** (200 OK):
```json
{
  "documentId": "660e8400-e29b-41d4-a716-446655440001",
  "status": "Completed",
  "progress": 100,
  "extractedPages": 24,
  "chunksCreated": 156,
  "confidence": 0.87,
  "errors": []
}
```

---

## Error Responses

### Standard Error Format

Tutte le risposte di errore seguono RFC 7807 (Problem Details):

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/auth/register",
  "errors": {
    "Email": ["Email is required"],
    "Password": ["Password must be at least 8 characters"]
  }
}
```

### Common HTTP Status Codes

| Code | Meaning | Example |
|------|---------|---------|
| 200 | OK | Successful GET/PUT |
| 201 | Created | Successful POST (resource created) |
| 202 | Accepted | Async operation started |
| 204 | No Content | Successful DELETE |
| 400 | Bad Request | Validation errors |
| 401 | Unauthorized | Missing/invalid authentication |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Duplicate resource (e.g., email) |
| 422 | Unprocessable Entity | Business logic validation failed |
| 500 | Internal Server Error | Server-side error |
| 503 | Service Unavailable | Dependency unavailable (DB, Redis, Qdrant) |

---

## Rate Limiting

**Strategy**: Token bucket algorithm

**Limits**:
- **Anonymous**: 100 req/min per IP
- **Authenticated**: 1000 req/min per user
- **Admin**: Unlimited

**Headers**:
```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 987
X-RateLimit-Reset: 1672531200
```

**Rate Limit Exceeded (429)**:
```json
{
  "type": "https://httpstatuses.com/429",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Try again in 42 seconds.",
  "instance": "/api/v1/chat",
  "retryAfter": 42
}
```

---

## Pagination

**Standard Pattern**: Cursor-based pagination

**Request**:
```http
GET /api/v1/games?limit=20&cursor=eyJpZCI6IjEyMyJ9 HTTP/1.1
```

**Response**:
```json
{
  "data": [
    { "id": "...", "name": "Catan" },
    { "id": "...", "name": "Ticket to Ride" }
  ],
  "pagination": {
    "limit": 20,
    "nextCursor": "eyJpZCI6IjQ1NiJ9",
    "hasMore": true
  }
}
```

---

## Webhooks

**Eventi supportati**:
- `document.processing.completed`
- `document.processing.failed`
- `user.created`
- `alert.triggered`

**Configurazione**:
```http
POST /api/v1/webhooks HTTP/1.1
Content-Type: application/json

{
  "url": "https://your-app.com/webhooks/meepleai",
  "events": ["document.processing.completed"],
  "secret": "whsec_abc123..."
}
```

**Payload Example**:
```json
{
  "id": "evt_123",
  "type": "document.processing.completed",
  "createdAt": "2026-01-01T10:00:00Z",
  "data": {
    "documentId": "660e8400-e29b-41d4-a716-446655440001",
    "status": "Completed",
    "confidence": 0.87
  }
}
```

**Signature Verification**:
```typescript
import crypto from 'crypto';

function verifyWebhook(payload: string, signature: string, secret: string): boolean {
  const hmac = crypto.createHmac('sha256', secret);
  const digest = hmac.update(payload).digest('hex');
  return crypto.timingSafeEqual(
    Buffer.from(signature),
    Buffer.from(digest)
  );
}
```

---

## Data Models

### User

```typescript
interface User {
  id: string;                    // UUID
  email: string;
  roles: string[];               // ["User"] | ["Admin"]
  emailConfirmed: boolean;
  twoFactorEnabled: boolean;
  createdAt: string;             // ISO 8601
  updatedAt: string;
}
```

### Game

```typescript
interface Game {
  id: string;                    // UUID
  name: string;
  nameIt: string;                // Italian name
  description?: string;
  descriptionIt?: string;
  publisher?: string;
  yearPublished?: number;
  minPlayers: number;
  maxPlayers: number;
  playingTime: number;           // minutes
  minAge: number;
  bggId?: number;                // BoardGameGeek ID
  imageUrl?: string;
  createdAt: string;
  updatedAt: string;
}
```

### ChatMessage

```typescript
interface ChatMessage {
  id: string;                    // UUID
  threadId: string;
  role: 'user' | 'assistant';
  content: string;
  confidence?: number;           // 0.0 - 1.0
  sources?: Source[];
  createdAt: string;
}

interface Source {
  documentId: string;
  page: number;
  excerpt: string;
  relevanceScore: number;
}
```

### Document

```typescript
interface Document {
  id: string;                    // UUID
  gameId: string;
  filename: string;
  fileSize: number;              // bytes
  mimeType: string;              // "application/pdf"
  status: 'Uploading' | 'Processing' | 'Completed' | 'Failed';
  progress: number;              // 0-100
  extractedPages: number;
  chunksCreated: number;
  confidence: number;            // 0.0 - 1.0
  errors: string[];
  uploadedBy: string;            // User ID
  createdAt: string;
  completedAt?: string;
}
```

---

## Performance

### Response Times (P95)

| Endpoint | Target | Actual |
|----------|--------|--------|
| `/api/v1/games` | <100ms | 45ms |
| `/api/v1/chat` (first token) | <500ms | 320ms |
| `/api/v1/chat` (full response) | <3s | 1.8s |
| `/api/v1/documents/upload` | <200ms | 150ms |

### Caching Strategy

**HybridCache** (L1: Memory + L2: Redis):
- Games list: 5 min
- Game details: 10 min
- User profile: 1 min
- RAG results: 30 min (cache key = hash(question + gameId))

**Cache Headers**:
```http
Cache-Control: public, max-age=300
ETag: "33a64df551425fcc55e4d42a148795d9f25f89d4"
```

---

## Security

### HTTPS Only (Production)

```http
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

### CORS Configuration

**Allowed Origins** (Production):
- `https://meepleai.com`
- `https://app.meepleai.com`

**Headers**:
```http
Access-Control-Allow-Origin: https://app.meepleai.com
Access-Control-Allow-Credentials: true
Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
Access-Control-Allow-Headers: Content-Type, Authorization, X-Api-Key
```

### Content Security Policy

```http
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'
```

### API Key Security

- **Format**: `mpl_{env}_{base64}` (32 bytes random)
- **Storage**: PBKDF2 hashed with 10,000 iterations
- **Rotation**: Recommended every 90 days
- **Revocation**: Immediate via DELETE endpoint

---

## Client Libraries

### TypeScript/JavaScript

**Auto-generated client** da OpenAPI spec:

```typescript
import { ApiClient } from '@/lib/api';

const client = new ApiClient({
  baseUrl: 'http://localhost:8080',
  credentials: 'include', // for cookie auth
});

// OR with API key
const client = new ApiClient({
  baseUrl: 'http://localhost:8080',
  apiKey: 'mpl_prod_abc123...',
});

// Usage
const games = await client.games.list();
const answer = await client.chat.ask({
  question: 'Come si gioca?',
  gameId: '...',
});
```

### cURL Examples

**Get Games**:
```bash
curl http://localhost:8080/api/v1/games
```

**Ask Question with API Key**:
```bash
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: mpl_prod_abc123..." \
  -d '{"question":"Come si gioca a Catan?","gameId":"..."}'
```

**Upload PDF with Cookie**:
```bash
curl -X POST http://localhost:8080/api/v1/documents/upload \
  -H "Cookie: .AspNetCore.Identity.Application=..." \
  -F "file=@catan-rules.pdf" \
  -F "gameId=550e8400-e29b-41d4-a716-446655440000"
```

---

## Monitoring

### Health Check

**Endpoint**: `GET /health`

**Response**:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0500000"
    },
    "qdrant": {
      "status": "Healthy",
      "duration": "00:00:00.0300000"
    },
    "redis": {
      "status": "Healthy",
      "duration": "00:00:00.0100000"
    }
  }
}
```

### Metrics (Prometheus)

**Endpoint**: `GET /metrics`

**Key Metrics**:
- `http_requests_total{endpoint, method, status}`
- `http_request_duration_seconds{endpoint, method}`
- `rag_query_confidence{game}`
- `pdf_processing_duration_seconds{stage}`

---

## Versioning

**Strategy**: URI versioning (`/api/v1/...`)

**Deprecation Policy**:
- 6 months notice for breaking changes
- Old version supported for 12 months after deprecation notice
- Sunset header: `Sunset: Sat, 01 Jan 2027 00:00:00 GMT`

---

## Resources

- [Interactive API Explorer (Scalar)](http://localhost:8080/scalar/v1)
- [OpenAPI Specification](http://localhost:8080/openapi/v1.json)
- [Authentication Guide](../development/README.md#authentication)
- [Rate Limiting Details](../architecture/adr/adr-020-rate-limiting.md)

---

**Version**: 1.0
**Last Updated**: 2026-01-01
**API Version**: v1
**Maintainers**: Engineering Team
