# MeepleAI API Specification v1.0

**Status**: Draft for Phase 1 Implementation
**Version**: 1.0.0
**Base URL**: `https://api.meepleai.dev/v1` (production), `http://localhost:8000/v1` (local)
**Date**: 2025-01-15

---

## Table of Contents

1. [Overview](#overview)
2. [Authentication](#authentication)
3. [Rate Limiting](#rate-limiting)
4. [Error Handling](#error-handling)
5. [Endpoints](#endpoints)
   - [Question Answering](#question-answering)
   - [Games Management](#games-management)
   - [Rulebooks Management](#rulebooks-management)
   - [User Management](#user-management)
   - [Notifications](#notifications)
   - [Admin](#admin)
6. [Data Models](#data-models)
7. [Examples](#examples)
8. [SDK Libraries](#sdk-libraries)

---

## Overview

MeepleAI REST API fornisce accesso programmatico al sistema di assistenza AI per regolamenti di board games. L'API segue i principi REST, utilizza JSON per request/response bodies, e implementa autenticazione via API key o session cookies.

### API Design Principles

1. **Versioned URLs**: `/v1/*` per stabilità contrattuale (breaking changes → nuova versione)
2. **Resource-Oriented**: Endpoints represent resources (games, rulebooks, questions)
3. **HTTP Semantics**: GET (read), POST (create), PUT/PATCH (update), DELETE (delete)
4. **Idempotency**: PUT/DELETE operations sono idempotenti
5. **Pagination**: Endpoints che ritornano liste supportano `page` e `limit` query params
6. **Filtering**: Query parameters per filtering (es. `?language=it&genre=strategy`)

### Base URL Structure

```
Production:  https://api.meepleai.dev/v1
Staging:     https://staging-api.meepleai.dev/v1
Local Dev:   http://localhost:8000/v1
```

### Content Type

- **Request**: `Content-Type: application/json`
- **Response**: `Content-Type: application/json; charset=utf-8`

---

## Authentication

### API Key Authentication (Recommended for Programmatic Access)

**Header**: `X-API-Key: <your_api_key>`

**API Key Format**: `mpl_<env>_<random_base64>` (es. `mpl_prod_a8f3c92b...`)

**Obtaining API Key**:
1. Register account at https://app.meepleai.dev
2. Navigate to Settings → API Keys
3. Click "Generate New API Key"
4. Copy key immediately (shown only once)

**Example**:
```bash
curl -H "X-API-Key: mpl_prod_a8f3c92b..." \
     https://api.meepleai.dev/v1/games
```

### Session Cookie Authentication (Web App)

**Cookie Name**: `meepleai_session`

**Flow**:
1. POST `/v1/auth/login` with credentials
2. Receive session cookie (httpOnly, secure, SameSite=strict)
3. Subsequent requests include cookie automatically

**Example**:
```bash
# Login
curl -X POST https://api.meepleai.dev/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email": "user@example.com", "password": "SecurePass123!"}'

# Subsequent requests (cookie stored by client)
curl --cookie-jar cookies.txt \
     https://api.meepleai.dev/v1/games
```

### Authentication Priority

If both API key and session cookie present: **API key takes precedence**

### Unauthenticated Endpoints

- `GET /v1/games` (public game catalog)
- `GET /v1/games/:id` (public game details)
- `POST /v1/auth/register`
- `POST /v1/auth/login`

---

## Rate Limiting

### Tier-Based Limits

| Tier | Requests/Minute | Requests/Day | Burst |
|------|----------------|--------------|-------|
| **Unauthenticated** | 10 | 100 | 20 |
| **Free User** | 20 | 300 (10 Q&A queries) | 50 |
| **Premium User** | 100 | Unlimited | 200 |
| **B2B Partner** | 500 | Unlimited | 1000 |

### Rate Limit Headers

Every response includes rate limit headers:

```
X-RateLimit-Limit: 100          # Max requests per window
X-RateLimit-Remaining: 73        # Remaining requests
X-RateLimit-Reset: 1705328400    # Unix timestamp when limit resets
Retry-After: 42                  # Seconds to wait (if 429 error)
```

### 429 Too Many Requests Response

```json
{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Rate limit exceeded. Maximum 100 requests per minute for premium tier.",
    "details": {
      "limit": 100,
      "remaining": 0,
      "reset_at": "2025-01-15T14:30:00Z",
      "retry_after_seconds": 42
    }
  }
}
```

### Endpoint-Specific Rate Limits

Some endpoints have stricter rate limits to prevent abuse:

| Endpoint | Limit | Window | Reason |
|----------|-------|--------|--------|
| `POST /v1/notifications/mark-all-read` | 10 | 1 minute | Bulk operation protection |

These limits apply in addition to the tier-based limits above.

---

## Error Handling

### Standard Error Response Format

```json
{
  "error": {
    "code": "ERROR_CODE_CONSTANT",
    "message": "Human-readable error message",
    "details": {
      "field": "Additional context (optional)"
    },
    "request_id": "req_a8f3c92b4d5e6f7a",
    "timestamp": "2025-01-15T12:34:56.789Z"
  }
}
```

### HTTP Status Codes

| Code | Meaning | When Used |
|------|---------|-----------|
| **200 OK** | Success | GET, PUT, PATCH successful |
| **201 Created** | Resource created | POST successful |
| **204 No Content** | Success, no body | DELETE successful |
| **400 Bad Request** | Invalid input | Validation errors, malformed JSON |
| **401 Unauthorized** | Authentication failed | Missing/invalid API key or session |
| **403 Forbidden** | Authorization failed | Valid auth but insufficient permissions |
| **404 Not Found** | Resource not found | Game/rulebook/user doesn't exist |
| **409 Conflict** | Resource conflict | Duplicate game ID, concurrent update |
| **422 Unprocessable Entity** | Semantic error | PDF too large, unsupported game |
| **429 Too Many Requests** | Rate limit exceeded | See rate limiting section |
| **500 Internal Server Error** | Server error | Unexpected server-side failure |
| **503 Service Unavailable** | Service degraded | LLM API down, maintenance mode |

### Common Error Codes

```
AUTHENTICATION_FAILED          # Invalid API key or session
INSUFFICIENT_PERMISSIONS       # User lacks required role
RATE_LIMIT_EXCEEDED           # Too many requests
VALIDATION_ERROR              # Input validation failed
RESOURCE_NOT_FOUND            # Game/rulebook not found
DUPLICATE_RESOURCE            # Resource already exists
LLM_API_UNAVAILABLE           # OpenAI/Claude API down
VECTOR_DB_UNAVAILABLE         # Weaviate connection failed
CONFIDENCE_TOO_LOW            # AI confidence below threshold
PROCESSING_FAILED             # PDF processing error
INTERNAL_ERROR                # Unexpected server error
```

---

## Endpoints

### Question Answering

#### POST /v1/qa/ask

Ask a question about board game rules.

**Authentication**: Required (API key or session)

**Request Body**:
```json
{
  "game_id": "terraforming-mars",
  "question": "Posso usare Standard Projects dopo aver passato?",
  "language": "it",
  "user_id": "user_a8f3c92b" // Optional, for personalization
}
```

**Response 200 OK**:
```json
{
  "data": {
    "answer": "No, Standard Projects possono essere usati solo durante il proprio turno, prima di passare.",
    "confidence": 0.85,
    "confidence_label": "high",
    "citations": [
      {
        "page": 8,
        "snippet": "Durante il proprio turno, prima di effettuare 1 o 2 azioni, il giocatore può utilizzare qualsiasi numero di Progetti Standard.",
        "source": "Regolamento ufficiale Terraforming Mars (italiano)"
      }
    ],
    "sources": [
      {
        "type": "rulebook",
        "name": "Regolamento Terraforming Mars",
        "page": 8,
        "url": "/rulebooks/terraforming-mars/it/pages/8"
      },
      {
        "type": "faq",
        "name": "FAQ ufficiali 2023",
        "question_id": "Q12",
        "url": "https://www.fryxgames.se/terraforming-mars-faq"
      }
    ],
    "validation": {
      "confidence_pass": true,
      "consensus_pass": true,
      "citation_verified": true,
      "hallucination_detected": false
    },
    "metadata": {
      "game_id": "terraforming-mars",
      "game_name": "Terraforming Mars",
      "language": "it",
      "processing_time_ms": 2341,
      "model_used": "gpt-4-turbo-2024-04-09",
      "validation_model": "claude-3-5-sonnet-20240620"
    },
    "request_id": "req_a8f3c92b4d5e6f7a",
    "timestamp": "2025-01-15T12:34:56.789Z"
  }
}
```

**Response 200 OK (Low Confidence - Explicit Uncertainty)**:
```json
{
  "data": {
    "answer": "Non ho informazioni sufficienti per rispondere con certezza a questa domanda.",
    "confidence": 0.62,
    "confidence_label": "low",
    "citations": [],
    "sources": [],
    "validation": {
      "confidence_pass": false,
      "reason": "Confidence 0.62 below threshold 0.70"
    },
    "suggestions": [
      "Consulta il regolamento ufficiale alle pagine 15-18",
      "Chiedi nel forum de La Tana dei Goblin: https://www.gdt.it/forum",
      "Contatta il publisher per chiarimenti: support@fryxgames.se"
    ],
    "metadata": {
      "game_id": "terraforming-mars",
      "game_name": "Terraforming Mars",
      "language": "it",
      "processing_time_ms": 1823,
      "model_used": "gpt-4-turbo-2024-04-09"
    },
    "request_id": "req_b9g4d03c5e6f8g9b",
    "timestamp": "2025-01-15T12:35:12.456Z"
  }
}
```

**Response 400 Bad Request**:
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Validation failed",
    "details": {
      "game_id": "Required field missing",
      "question": "Question must be at least 10 characters"
    },
    "request_id": "req_c0h5e14d6f7g9h0c",
    "timestamp": "2025-01-15T12:36:00.123Z"
  }
}
```

**Response 503 Service Unavailable**:
```json
{
  "error": {
    "code": "LLM_API_UNAVAILABLE",
    "message": "AI service temporarily unavailable. Please try again in a few minutes.",
    "details": {
      "provider": "OpenAI",
      "estimated_recovery": "2025-01-15T12:45:00Z"
    },
    "request_id": "req_d1i6f25e7g8h0i1d",
    "timestamp": "2025-01-15T12:37:30.789Z"
  }
}
```

**Validation Rules**:
- `game_id`: Required, must exist in games catalog
- `question`: Required, 10-500 characters, non-empty after trim
- `language`: Optional, defaults to "it", supported: ["it", "en", "fr", "de", "es"] (Phase 4)
- `user_id`: Optional, used for personalization and analytics

---

#### POST /v1/qa/feedback

Provide feedback on answer quality (thumbs up/down).

**Authentication**: Required

**Request Body**:
```json
{
  "request_id": "req_a8f3c92b4d5e6f7a",
  "feedback": "positive",
  "comment": "Perfetta! Mi ha aiutato a risolvere il dubbio durante la partita."
}
```

**Response 201 Created**:
```json
{
  "data": {
    "feedback_id": "fb_x9y0z1a2b3c4d5e6",
    "request_id": "req_a8f3c92b4d5e6f7a",
    "feedback": "positive",
    "recorded_at": "2025-01-15T12:40:00.000Z"
  }
}
```

**Validation Rules**:
- `request_id`: Required, must be valid request ID from previous Q&A
- `feedback`: Required, enum: ["positive", "negative"]
- `comment`: Optional, max 1000 characters

---

### Games Management

#### GET /v1/games

List all games in catalog.

**Authentication**: Optional (public endpoint, but authenticated users see personalized data)

**Query Parameters**:
- `page`: Integer, default 1, min 1
- `limit`: Integer, default 20, min 1, max 100
- `language`: String, filter by language (e.g., "it", "en")
- `genre`: String, filter by genre (e.g., "strategy", "party", "family")
- `publisher`: String, filter by publisher (e.g., "fryxgames", "stonemaier")
- `min_players`: Integer, minimum player count
- `max_players`: Integer, maximum player count
- `search`: String, search in game name/description

**Response 200 OK**:
```json
{
  "data": {
    "games": [
      {
        "id": "terraforming-mars",
        "name": "Terraforming Mars",
        "publisher": "FryxGames",
        "year": 2016,
        "min_players": 1,
        "max_players": 5,
        "play_time_minutes": 120,
        "genres": ["strategy", "engine-building"],
        "languages": ["it", "en", "de", "fr", "es"],
        "rulebook_available": true,
        "rulebook_pages": 24,
        "indexed_at": "2025-01-10T10:00:00Z",
        "query_count": 1523,
        "avg_confidence": 0.87,
        "image_url": "https://cdn.meepleai.dev/games/terraforming-mars/cover.jpg",
        "bgg_id": 167791,
        "bgg_url": "https://boardgamegeek.com/boardgame/167791/terraforming-mars"
      },
      {
        "id": "wingspan",
        "name": "Wingspan",
        "publisher": "Stonemaier Games",
        "year": 2019,
        "min_players": 1,
        "max_players": 5,
        "play_time_minutes": 60,
        "genres": ["strategy", "card-game", "engine-building"],
        "languages": ["it", "en"],
        "rulebook_available": true,
        "rulebook_pages": 18,
        "indexed_at": "2025-01-11T14:30:00Z",
        "query_count": 987,
        "avg_confidence": 0.91,
        "image_url": "https://cdn.meepleai.dev/games/wingspan/cover.jpg",
        "bgg_id": 266192,
        "bgg_url": "https://boardgamegeek.com/boardgame/266192/wingspan"
      }
    ],
    "pagination": {
      "page": 1,
      "limit": 20,
      "total_pages": 5,
      "total_count": 92,
      "has_next": true,
      "has_previous": false
    }
  }
}
```

---

#### GET /v1/games/:id

Get details for specific game.

**Authentication**: Optional

**Path Parameters**:
- `id`: String, game ID (e.g., "terraforming-mars")

**Response 200 OK**:
```json
{
  "data": {
    "id": "terraforming-mars",
    "name": "Terraforming Mars",
    "name_translations": {
      "it": "Terraforming Mars",
      "en": "Terraforming Mars",
      "de": "Terraforming Mars"
    },
    "publisher": "FryxGames",
    "publisher_url": "https://www.fryxgames.se",
    "year": 2016,
    "designers": ["Jacob Fryxelius"],
    "artists": ["Isaac Fryxelius"],
    "min_players": 1,
    "max_players": 5,
    "optimal_players": [3, 4],
    "play_time_minutes": 120,
    "play_time_range": [90, 180],
    "min_age": 12,
    "complexity": 3.2,
    "genres": ["strategy", "engine-building", "economic"],
    "mechanics": ["hand-management", "drafting", "tile-placement"],
    "languages": ["it", "en", "de", "fr", "es", "pl", "pt"],
    "description": {
      "it": "Nel 2400, l'umanità inizia a terraformare Marte. Corporazioni gigantesche, sponsorizzate dal governo mondiale sulla Terra, iniziano grandi progetti per aumentare la temperatura...",
      "en": "In the 2400s, mankind begins to terraform the planet Mars. Giant corporations, sponsored by the World Government on Earth, initiate huge projects to raise the temperature..."
    },
    "rulebook_available": true,
    "rulebook_versions": [
      {
        "language": "it",
        "version": "1.5",
        "pages": 24,
        "file_size_mb": 2.3,
        "uploaded_at": "2025-01-10T10:00:00Z",
        "chunk_count": 89,
        "download_url": "/rulebooks/terraforming-mars/it/v1.5.pdf"
      },
      {
        "language": "en",
        "version": "1.5",
        "pages": 20,
        "file_size_mb": 1.9,
        "uploaded_at": "2025-01-10T10:05:00Z",
        "chunk_count": 76,
        "download_url": "/rulebooks/terraforming-mars/en/v1.5.pdf"
      }
    ],
    "expansions": [
      {
        "id": "terraforming-mars-venus-next",
        "name": "Venus Next",
        "year": 2017
      },
      {
        "id": "terraforming-mars-prelude",
        "name": "Prelude",
        "year": 2018
      }
    ],
    "stats": {
      "query_count_total": 1523,
      "query_count_30d": 287,
      "avg_confidence": 0.87,
      "avg_latency_ms": 2341,
      "unique_users_30d": 142
    },
    "external_links": {
      "bgg_id": 167791,
      "bgg_url": "https://boardgamegeek.com/boardgame/167791/terraforming-mars",
      "bgg_rating": 8.4,
      "bgg_rank": 5,
      "publisher_url": "https://www.fryxgames.se/games/terraforming-mars",
      "rules_url": "https://www.fryxgames.se/games/terraforming-mars/rules"
    },
    "images": {
      "cover": "https://cdn.meepleai.dev/games/terraforming-mars/cover.jpg",
      "box": "https://cdn.meepleai.dev/games/terraforming-mars/box.jpg",
      "components": [
        "https://cdn.meepleai.dev/games/terraforming-mars/components-1.jpg",
        "https://cdn.meepleai.dev/games/terraforming-mars/components-2.jpg"
      ]
    },
    "created_at": "2025-01-10T09:00:00Z",
    "updated_at": "2025-01-15T10:30:00Z"
  }
}
```

**Response 404 Not Found**:
```json
{
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "Game with ID 'non-existent-game' not found",
    "request_id": "req_e2j7g36f8h9i1j2e",
    "timestamp": "2025-01-15T13:00:00.000Z"
  }
}
```

---

### Rulebooks Management

#### POST /v1/rulebooks

Upload rulebook PDF for indexing (Admin only).

**Authentication**: Required (Admin role)

**Content-Type**: `multipart/form-data`

**Form Fields**:
- `file`: File, PDF document, max 50 MB
- `game_id`: String, existing game ID
- `language`: String, rulebook language (e.g., "it")
- `version`: String, rulebook version (e.g., "1.5")

**Response 202 Accepted**:
```json
{
  "data": {
    "job_id": "job_f3k8h47g9i0j2k3f",
    "status": "processing",
    "game_id": "azul",
    "language": "it",
    "estimated_time_seconds": 120,
    "status_url": "/v1/rulebooks/jobs/job_f3k8h47g9i0j2k3f"
  }
}
```

**Response 400 Bad Request** (File too large):
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "File size exceeds maximum allowed size of 50 MB",
    "details": {
      "file_size_mb": 73.2,
      "max_size_mb": 50
    },
    "request_id": "req_g4l9i58h0j1k3l4g",
    "timestamp": "2025-01-15T13:15:00.000Z"
  }
}
```

**Validation Rules**:
- `file`: Required, PDF only (magic bytes validation), max 50 MB
- `game_id`: Required, must exist in games catalog
- `language`: Required, ISO 639-1 code (e.g., "it", "en")
- `version`: Optional, semantic version format (e.g., "1.5", "2.0.1")

---

#### GET /v1/rulebooks/jobs/:job_id

Check indexing job status.

**Authentication**: Required

**Path Parameters**:
- `job_id`: String, job ID from upload response

**Response 200 OK** (Processing):
```json
{
  "data": {
    "job_id": "job_f3k8h47g9i0j2k3f",
    "status": "processing",
    "game_id": "azul",
    "language": "it",
    "progress": {
      "current_step": "chunking",
      "steps_completed": 2,
      "steps_total": 5,
      "percentage": 40
    },
    "started_at": "2025-01-15T13:10:00Z",
    "estimated_completion": "2025-01-15T13:12:00Z"
  }
}
```

**Response 200 OK** (Completed):
```json
{
  "data": {
    "job_id": "job_f3k8h47g9i0j2k3f",
    "status": "completed",
    "game_id": "azul",
    "language": "it",
    "result": {
      "rulebook_id": "rb_h5m0j69i1k2l4m5h",
      "pages": 12,
      "chunks": 47,
      "processing_method": "unstructured",
      "quality_score": 0.94,
      "indexed_at": "2025-01-15T13:12:15Z"
    },
    "started_at": "2025-01-15T13:10:00Z",
    "completed_at": "2025-01-15T13:12:15Z",
    "duration_seconds": 135
  }
}
```

**Response 200 OK** (Failed):
```json
{
  "data": {
    "job_id": "job_f3k8h47g9i0j2k3f",
    "status": "failed",
    "game_id": "azul",
    "language": "it",
    "error": {
      "code": "PROCESSING_FAILED",
      "message": "PDF text extraction failed. File may be corrupted or scanned with poor quality.",
      "details": {
        "step": "text_extraction",
        "method_tried": ["unstructured", "smoldocling", "docnet"],
        "extracted_text_length": 23
      }
    },
    "started_at": "2025-01-15T13:10:00Z",
    "failed_at": "2025-01-15T13:11:42Z",
    "duration_seconds": 102
  }
}
```

---

### User Management

#### POST /v1/auth/register

Register new user account.

**Authentication**: None (public endpoint)

**Request Body**:
```json
{
  "email": "giocatore@example.com",
  "password": "SecurePass123!",
  "display_name": "Marco Rossi",
  "language": "it"
}
```

**Response 201 Created**:
```json
{
  "data": {
    "user_id": "user_i6n1k70j2l3m5n6i",
    "email": "giocatore@example.com",
    "display_name": "Marco Rossi",
    "role": "free_user",
    "language": "it",
    "created_at": "2025-01-15T14:00:00Z",
    "session": {
      "token": "sess_j7o2l81k3m4n6o7j",
      "expires_at": "2025-01-22T14:00:00Z"
    }
  }
}
```

**Response 409 Conflict** (Email already exists):
```json
{
  "error": {
    "code": "DUPLICATE_RESOURCE",
    "message": "User with email 'giocatore@example.com' already exists",
    "request_id": "req_k8p3m92l4n5o7p8k",
    "timestamp": "2025-01-15T14:01:00.000Z"
  }
}
```

**Validation Rules**:
- `email`: Required, valid email format, unique, max 255 chars
- `password`: Required, min 8 chars, must contain: uppercase, lowercase, digit
- `display_name`: Required, 2-100 chars, no special chars except space/hyphen/apostrophe
- `language`: Optional, defaults to "it", ISO 639-1 code

---

#### POST /v1/auth/login

Login with email and password.

**Authentication**: None

**Request Body**:
```json
{
  "email": "giocatore@example.com",
  "password": "SecurePass123!"
}
```

**Response 200 OK**:
```json
{
  "data": {
    "user_id": "user_i6n1k70j2l3m5n6i",
    "email": "giocatore@example.com",
    "display_name": "Marco Rossi",
    "role": "premium_user",
    "language": "it",
    "session": {
      "token": "sess_l9q4n03m5o6p8q9l",
      "expires_at": "2025-01-22T14:05:00Z"
    },
    "subscription": {
      "tier": "premium",
      "status": "active",
      "started_at": "2025-01-01T00:00:00Z",
      "renews_at": "2025-02-01T00:00:00Z"
    }
  }
}
```

**Response 401 Unauthorized**:
```json
{
  "error": {
    "code": "AUTHENTICATION_FAILED",
    "message": "Invalid email or password",
    "request_id": "req_m0r5o14n6p7q9r0m",
    "timestamp": "2025-01-15T14:06:00.000Z"
  }
}
```

---

#### GET /v1/users/me

Get current user profile.

**Authentication**: Required

**Response 200 OK**:
```json
{
  "data": {
    "user_id": "user_i6n1k70j2l3m5n6i",
    "email": "giocatore@example.com",
    "display_name": "Marco Rossi",
    "role": "premium_user",
    "language": "it",
    "subscription": {
      "tier": "premium",
      "status": "active",
      "started_at": "2025-01-01T00:00:00Z",
      "renews_at": "2025-02-01T00:00:00Z",
      "cancel_at_period_end": false
    },
    "usage": {
      "queries_today": 23,
      "queries_this_month": 387,
      "daily_limit": null,
      "monthly_limit": null
    },
    "api_keys": [
      {
        "key_id": "key_n1s6p25o7q8r0s1n",
        "name": "Production API Key",
        "prefix": "mpl_prod_a8f3c92b",
        "created_at": "2025-01-10T10:00:00Z",
        "last_used_at": "2025-01-15T12:30:00Z"
      }
    ],
    "preferences": {
      "default_language": "it",
      "email_notifications": true,
      "query_history_retention_days": 30
    },
    "created_at": "2024-12-01T10:00:00Z",
    "updated_at": "2025-01-15T14:00:00Z"
  }
}
```

---

### Notifications

User notification endpoints for upload/processing completion alerts.

#### GET /v1/notifications

Get notifications for authenticated user.

**Authentication**: Required (Session or API Key)

**Query Parameters**:
- `unreadOnly`: Boolean, filter for unread notifications only
- `limit`: Integer, max number of notifications to return

**Response 200 OK**:
```json
{
  "data": [
    {
      "id": "notif_a1b2c3d4",
      "type": "pdf_processing_complete",
      "title": "PDF Processing Complete",
      "message": "Your rulebook 'Catan Rules' has been processed",
      "read": false,
      "created_at": "2025-01-15T14:00:00Z"
    }
  ]
}
```

#### GET /v1/notifications/unread-count

Get count of unread notifications (optimized for badge display).

**Authentication**: Required (Session or API Key)

**Response 200 OK**:
```json
{
  "count": 5
}
```

#### POST /v1/notifications/{notificationId}/mark-read

Mark a single notification as read.

**Authentication**: Required (Session or API Key)

**Response 200 OK**:
```json
{
  "success": true
}
```

**Response 404 Not Found** (Notification not found or unauthorized):
```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Notification not found"
  }
}
```

#### POST /v1/notifications/mark-all-read

Bulk operation to mark all unread notifications as read.

**Authentication**: Required (Session or API Key)

**Rate Limiting**: 10 requests per minute (stricter limit for bulk operations)

**Response 200 OK**:
```json
{
  "updatedCount": 12
}
```

**Response 429 Too Many Requests** (Rate limit exceeded):
```json
{
  "error": "Rate limit exceeded",
  "retryAfter": 45,
  "message": "Too many mark-all requests. Please wait before retrying."
}
```

**Rate Limit Headers** (Always included):
```
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 7
Retry-After: 45  (only when rate limited)
```

---

### Admin

#### GET /v1/admin/metrics

Get system-wide metrics (Admin only).

**Authentication**: Required (Admin role)

**Query Parameters**:
- `period`: String, enum: ["1h", "24h", "7d", "30d"], default "24h"

**Response 200 OK**:
```json
{
  "data": {
    "period": "24h",
    "timestamp": "2025-01-15T14:30:00Z",
    "accuracy": {
      "overall": 0.92,
      "by_game": {
        "terraforming-mars": 0.94,
        "wingspan": 0.91,
        "azul": 0.89
      },
      "by_confidence_range": {
        "0.90-1.00": 0.97,
        "0.80-0.89": 0.91,
        "0.70-0.79": 0.83,
        "below_0.70": 0.62
      }
    },
    "validation": {
      "confidence_failures": 23,
      "consensus_failures": 7,
      "citation_failures": 4,
      "hallucination_detections": 2
    },
    "performance": {
      "requests_total": 3842,
      "requests_per_second_avg": 0.04,
      "requests_per_second_peak": 12.3,
      "latency_p50_ms": 1834,
      "latency_p95_ms": 2912,
      "latency_p99_ms": 4321,
      "error_rate": 0.012
    },
    "llm_usage": {
      "calls_total": 3849,
      "tokens_input": 1847203,
      "tokens_output": 234182,
      "cost_usd": 127.43,
      "by_provider": {
        "openai": {
          "calls": 3842,
          "tokens_input": 1842100,
          "tokens_output": 230145,
          "cost_usd": 121.89
        },
        "anthropic": {
          "calls": 892,
          "tokens_input": 5103,
          "tokens_output": 4037,
          "cost_usd": 5.54
        }
      }
    },
    "cache": {
      "hit_rate": 0.43,
      "hits": 1652,
      "misses": 2190,
      "evictions": 47
    },
    "users": {
      "active_users_24h": 287,
      "new_registrations_24h": 12,
      "premium_conversions_24h": 2
    }
  }
}
```

---

## Data Models

### Game

```typescript
interface Game {
  id: string;                    // Unique game ID (slug format)
  name: string;                  // Game name
  publisher: string;             // Publisher name
  year: number;                  // Publication year
  min_players: number;           // Minimum players
  max_players: number;           // Maximum players
  play_time_minutes: number;     // Average play time
  genres: string[];              // Game genres
  languages: string[];           // Available languages
  rulebook_available: boolean;   // Rulebook indexed
  rulebook_pages?: number;       // Page count
  indexed_at?: string;           // ISO 8601 timestamp
  query_count: number;           // Total queries
  avg_confidence: number;        // Average confidence (0.0-1.0)
  image_url: string;             // Cover image URL
  bgg_id?: number;               // BoardGameGeek ID
  bgg_url?: string;              // BoardGameGeek URL
}
```

### Question Answer

```typescript
interface QuestionAnswer {
  answer: string;                // AI-generated answer
  confidence: number;            // Confidence score (0.0-1.0)
  confidence_label: string;      // "low" | "medium" | "high"
  citations: Citation[];         // Supporting citations
  sources: Source[];             // Source documents
  validation: Validation;        // Validation results
  metadata: Metadata;            // Processing metadata
  request_id: string;            // Unique request ID
  timestamp: string;             // ISO 8601 timestamp
}

interface Citation {
  page: number;                  // Page number
  snippet: string;               // Text snippet (max 200 chars)
  source: string;                // Source description
}

interface Source {
  type: string;                  // "rulebook" | "faq" | "errata"
  name: string;                  // Source name
  page?: number;                 // Page number (if applicable)
  question_id?: string;          // FAQ question ID
  url: string;                   // Source URL
}

interface Validation {
  confidence_pass: boolean;      // Confidence >= 0.70
  consensus_pass?: boolean;      // Multi-model agreement
  citation_verified: boolean;    // Citation page/snippet match
  hallucination_detected: boolean; // Forbidden keywords
}

interface Metadata {
  game_id: string;
  game_name: string;
  language: string;
  processing_time_ms: number;
  model_used: string;            // LLM model identifier
  validation_model?: string;     // Validation LLM
}
```

### User

```typescript
interface User {
  user_id: string;               // Unique user ID
  email: string;                 // User email
  display_name: string;          // Display name
  role: string;                  // "free_user" | "premium_user" | "admin" | "publisher"
  language: string;              // Preferred language
  subscription?: Subscription;   // Subscription details
  usage: Usage;                  // Usage statistics
  api_keys: ApiKey[];            // API keys
  preferences: Preferences;      // User preferences
  created_at: string;            // ISO 8601
  updated_at: string;            // ISO 8601
}

interface Subscription {
  tier: string;                  // "free" | "premium" | "b2b"
  status: string;                // "active" | "canceled" | "expired"
  started_at: string;
  renews_at?: string;
  cancel_at_period_end: boolean;
}

interface Usage {
  queries_today: number;
  queries_this_month: number;
  daily_limit: number | null;    // null = unlimited
  monthly_limit: number | null;
}

interface ApiKey {
  key_id: string;
  name: string;
  prefix: string;                // First 12 chars of key
  created_at: string;
  last_used_at?: string;
}
```

---

## Examples

### Example 1: Ask Question (cURL)

```bash
curl -X POST https://api.meepleai.dev/v1/qa/ask \
  -H "Content-Type: application/json" \
  -H "X-API-Key: mpl_prod_a8f3c92b..." \
  -d '{
    "game_id": "wingspan",
    "question": "Posso attivare poteri quando attivato durante setup?",
    "language": "it"
  }'
```

### Example 2: List Games (Python)

```python
import requests

response = requests.get(
    "https://api.meepleai.dev/v1/games",
    params={"language": "it", "genre": "strategy", "limit": 10},
    headers={"X-API-Key": "mpl_prod_a8f3c92b..."}
)

games = response.json()["data"]["games"]
for game in games:
    print(f"{game['name']} ({game['year']}) - {game['query_count']} queries")
```

### Example 3: Upload Rulebook (JavaScript)

```javascript
const formData = new FormData();
formData.append('file', pdfFile);
formData.append('game_id', 'scythe');
formData.append('language', 'it');
formData.append('version', '1.8');

const response = await fetch('https://api.meepleai.dev/v1/rulebooks', {
  method: 'POST',
  headers: {
    'X-API-Key': 'mpl_prod_a8f3c92b...'
  },
  body: formData
});

const data = await response.json();
console.log(`Job ID: ${data.data.job_id}, Status: ${data.data.status}`);
```

---

## SDK Libraries

### Python SDK (Phase 2+)

```python
from meepleai import MeepleAI

client = MeepleAI(api_key="mpl_prod_a8f3c92b...")

# Ask question
answer = client.qa.ask(
    game_id="terraforming-mars",
    question="Posso usare Standard Projects dopo aver passato?",
    language="it"
)

print(answer.answer)
print(f"Confidence: {answer.confidence}")
for citation in answer.citations:
    print(f"  - Page {citation.page}: {citation.snippet}")

# List games
games = client.games.list(language="it", genre="strategy", limit=10)
for game in games:
    print(f"{game.name} ({game.year})")
```

### JavaScript SDK (Phase 2+)

```javascript
import { MeepleAI } from '@meepleai/sdk';

const client = new MeepleAI({ apiKey: 'mpl_prod_a8f3c92b...' });

// Ask question
const answer = await client.qa.ask({
  gameId: 'wingspan',
  question: 'Posso attivare poteri quando attivato durante setup?',
  language: 'it'
});

console.log(answer.answer);
console.log(`Confidence: ${answer.confidence}`);
answer.citations.forEach(citation => {
  console.log(`  - Page ${citation.page}: ${citation.snippet}`);
});

// List games
const games = await client.games.list({ language: 'it', genre: 'strategy', limit: 10 });
games.forEach(game => console.log(`${game.name} (${game.year})`));
```

---

## Rate Limiting Best Practices

1. **Cache responses client-side** (30-60 min TTL for game catalog, 5 min for Q&A)
2. **Implement exponential backoff** on 429 errors (wait `Retry-After` seconds)
3. **Batch requests** where possible (future endpoint: `POST /v1/qa/batch`)
4. **Monitor `X-RateLimit-Remaining`** header, throttle proactively
5. **Upgrade to Premium** if hitting free tier limits frequently

---

## Changelog

### v1.0.0 (2025-01-15) - Initial Release
- Question Answering API (`POST /v1/qa/ask`)
- Games Management (`GET /v1/games`, `GET /v1/games/:id`)
- Rulebooks Management (`POST /v1/rulebooks`, `GET /v1/rulebooks/jobs/:id`)
- User Management (`POST /v1/auth/register`, `POST /v1/auth/login`, `GET /v1/users/me`)
- Admin Metrics (`GET /v1/admin/metrics`)
- API Key authentication
- Rate limiting (tier-based)

### Future Versions

**v1.1.0 (Phase 2)** - Planned Q2 2025:
- Feedback endpoint (`POST /v1/qa/feedback`)
- Batch Q&A (`POST /v1/qa/batch`)
- Streaming responses (Server-Sent Events)
- WebSocket support (real-time game state)

**v1.2.0 (Phase 3)** - Planned Q3 2025:
- Advanced search (`POST /v1/search`)
- User corrections (`POST /v1/corrections`)
- Community Q&A (`GET /v1/community/qa`, `POST /v1/community/qa`)

**v1.3.0 (Phase 4)** - Planned Q4 2025:
- Multilingual support (French, German, Spanish)
- Third-party integrations (`POST /v1/integrations/bga`, `/v1/integrations/discord`)
- Analytics dashboard API (`GET /v1/analytics/*`)

---

**Document Metadata**:
- **Version**: 1.0.0
- **Last Updated**: 2025-01-15
- **Status**: Draft for Phase 1 Implementation
- **Maintainer**: API Team <api@meepleai.dev>
