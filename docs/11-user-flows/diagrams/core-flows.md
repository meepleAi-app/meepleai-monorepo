# Core User Flow Sequence Diagrams

> Mermaid sequence diagrams for key user interactions.

## Table of Contents

- [Authentication Flows](#authentication-flows)
- [Library Management Flows](#library-management-flows)
- [AI Chat Flows](#ai-chat-flows)
- [Game Session Flows](#game-session-flows)
- [Editor Flows](#editor-flows)
- [Admin Flows](#admin-flows)

---

## Authentication Flows

### Complete Login Flow (with 2FA)

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API
    participant DB as Database
    participant R as Redis
    participant E as Email Service

    U->>F: Navigate to /login
    U->>F: Enter email + password
    F->>F: Client-side validation
    F->>A: POST /api/v1/auth/login

    A->>DB: Find user by email
    alt User not found
        A-->>F: 401 Invalid credentials
        F-->>U: Show error
    else User found
        A->>A: Verify password (BCrypt)
        alt Password wrong
            A-->>F: 401 Invalid credentials
        else Password correct
            A->>DB: Check 2FA status

            alt 2FA enabled
                A->>A: Generate temp token
                A-->>F: { requires2FA: true, tempToken }
                F-->>U: Show 2FA input
                U->>F: Enter TOTP code
                F->>A: POST /api/v1/auth/2fa/verify
                A->>A: Validate TOTP
                alt Invalid code
                    A-->>F: 401 Invalid code
                else Valid code
                    A->>R: Create session
                    A->>A: Set auth cookie
                    A-->>F: 200 OK + UserDto
                end
            else 2FA not enabled
                A->>R: Create session
                A->>A: Set auth cookie
                A-->>F: 200 OK + UserDto
            end

            F->>F: Store auth state
            F->>F: Navigate to /dashboard
            F-->>U: Show dashboard
        end
    end
```

### OAuth Flow

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API
    participant P as OAuth Provider
    participant DB as Database
    participant R as Redis

    U->>F: Click "Continue with Google"
    F->>A: GET /api/v1/auth/oauth/google/authorize
    A->>A: Generate state + PKCE verifier
    A->>R: Store state temporarily
    A-->>F: { redirectUrl }
    F->>P: Redirect to Google OAuth

    U->>P: Authorize MeepleAI
    P->>F: Redirect to /oauth-callback?code=xxx&state=yyy

    F->>A: GET /api/v1/auth/oauth/google/callback
    A->>R: Validate state
    A->>P: Exchange code for tokens
    P-->>A: { access_token, id_token }
    A->>P: Get user info
    P-->>A: { email, name, picture }

    A->>DB: Find user by OAuth ID
    alt New user
        A->>DB: Create user with OAuth link
    else Existing user
        A->>DB: Update OAuth link
    end

    A->>R: Create session
    A->>A: Set auth cookie
    A-->>F: Redirect to /dashboard
    F-->>U: Show dashboard
```

---

## Library Management Flows

### Add Game with Quota Check

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API
    participant Q as QuotaService
    participant DB as Database
    participant C as Cache

    U->>F: Click "Add to Library" on game
    F->>A: POST /api/v1/library/games/{gameId}

    A->>Q: CheckQuota(userId)
    Q->>C: Get cached quota
    alt Cache hit
        C-->>Q: Quota status
    else Cache miss
        Q->>DB: Count library entries
        DB-->>Q: Current count
        Q->>DB: Get tier limits
        DB-->>Q: Max allowed
        Q->>C: Cache result (5 min)
    end

    alt current >= max
        Q-->>A: QuotaDenied
        A-->>F: 403 { error: "QUOTA_EXCEEDED", current, max }
        F-->>U: Show "Library full" modal with upgrade option
    else Quota OK
        Q-->>A: QuotaAllowed
        A->>DB: Insert LibraryEntry
        A->>C: Invalidate quota cache
        A-->>F: 201 Created
        F->>F: Update local state
        F-->>U: Show success toast
    end
```

### Upload Custom PDF

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API
    participant Q as QuotaService
    participant S as Storage
    participant P as PDF Processor
    participant V as Vector Store
    participant R as Redis

    U->>F: Click "Upload Custom PDF"
    F->>A: GET /api/v1/users/me/upload-quota
    A->>Q: GetUploadQuota(userId)
    Q->>R: Get daily/weekly counts
    R-->>Q: Counts
    Q-->>A: { daily: {used, limit}, weekly: {used, limit} }
    A-->>F: Quota status

    alt Quota exceeded
        F-->>U: Show "Limit reached" with reset time
    else Quota OK
        F-->>U: Show upload modal
        U->>F: Select PDF file

        alt File > 10MB
            F->>A: POST /ingest/pdf/chunked/init
            A-->>F: { sessionId, chunkSize }
            loop Upload chunks
                F->>A: POST /ingest/pdf/chunked/chunk
                A->>S: Store chunk
                A-->>F: Progress update
                F-->>U: Update progress bar
            end
            F->>A: POST /ingest/pdf/chunked/complete
        else Normal size
            F->>A: POST /api/v1/library/games/{id}/pdf
            A->>S: Store file
        end

        A->>Q: IncrementQuota(userId)
        Q->>R: Increment counters with TTL
        A-->>F: 202 { pdfId, status: "processing" }

        Note over A,V: Background Processing
        A->>P: Queue extraction job
        P->>P: Extract text (Unstructured)
        P->>P: OCR if needed
        P->>V: Create embeddings
        V-->>P: Indexed

        loop Poll status
            F->>A: GET /pdfs/{id}/progress
            A-->>F: { status, progress }
            F-->>U: Update progress
        end

        A-->>F: { status: "completed" }
        F-->>U: Show success
    end
```

---

## AI Chat Flows

### RAG Question with Streaming

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API
    participant R as RAG Service
    participant V as Qdrant
    participant E as Embedding Service
    participant L as LLM Service

    U->>F: Type question + Send
    F->>F: Add message to UI (optimistic)
    F->>A: POST /api/v1/chat-threads/{id}/messages

    Note over A,L: RAG Pipeline
    A->>E: Generate question embedding
    E-->>A: Question vector

    A->>V: Hybrid search (vector + keyword)
    V-->>A: Relevant chunks (top 10)

    A->>R: Rerank results
    R-->>A: Reranked chunks (top 5)

    A->>A: Build prompt with context
    A->>L: Stream completion request

    Note over A,F: SSE Streaming
    A-->>F: SSE event: { type: "start", messageId }
    F-->>U: Show typing indicator

    loop Token stream
        L-->>A: Token
        A-->>F: SSE event: { type: "token", content }
        F->>F: Append to message
        F-->>U: Update display
    end

    A-->>F: SSE event: { type: "citation", data }
    F->>F: Add citation cards

    A-->>F: SSE event: { type: "done" }
    A->>A: Save response to DB
    F-->>U: Show complete response + citations
```

### Thread Management

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API
    participant DB as Database

    Note over U,DB: Create Thread
    U->>F: Start chat for game
    F->>A: POST /api/v1/chat-threads
    A->>DB: Create ChatThread record
    A-->>F: { threadId }
    F->>F: Navigate to chat

    Note over U,DB: Rename Thread
    U->>F: Click rename
    U->>F: Enter new title
    F->>A: PATCH /api/v1/chat-threads/{id}
    A->>DB: Update title
    A-->>F: 200 OK
    F-->>U: Title updated

    Note over U,DB: Close Thread
    U->>F: Click close
    F->>A: POST /api/v1/chat-threads/{id}/close
    A->>DB: Set status = "closed"
    A-->>F: 200 OK
    F-->>U: Thread closed (read-only)

    Note over U,DB: Export Thread
    U->>F: Click export
    F->>A: GET /api/v1/chat-threads/{id}/export?format=md
    A->>DB: Get all messages
    A->>A: Format as Markdown
    A-->>F: Markdown content
    F->>F: Trigger download
    F-->>U: File downloaded
```

---

## Game Session Flows

### Session with State Tracking

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API
    participant DB as Database
    participant H as SignalR Hub

    Note over U,DB: Create Session
    U->>F: Click "Start Session"
    F-->>U: Show setup modal
    U->>F: Add players, configure
    F->>A: POST /api/v1/sessions
    A->>DB: Create GameSession
    A->>DB: Create Player records
    A-->>F: { sessionId }

    Note over U,DB: Initialize State
    F->>A: POST /api/v1/sessions/{id}/state/initialize
    A->>DB: Get game's state template
    A->>DB: Create state from template
    A-->>F: Initial state
    F-->>U: Display state UI

    Note over U,H: Real-time State Updates
    F->>H: Connect to SignalR
    H-->>F: Connected

    U->>F: Update Alice's brick to 4
    F->>A: PATCH /api/v1/sessions/{id}/state
    A->>DB: Update state
    A->>DB: Create ledger entry
    A->>H: Broadcast StateUpdated
    A-->>F: Updated state
    H-->>F: StateUpdated event (all clients)
    F-->>U: UI updates

    Note over U,DB: Snapshot & Restore
    U->>F: Click "Save Snapshot"
    F->>A: POST /api/v1/sessions/{id}/state/snapshots
    A->>DB: Create snapshot
    A-->>F: { snapshotId }

    U->>F: Click "Restore Snapshot"
    F->>A: POST /api/v1/sessions/{id}/state/restore/{snapshotId}
    A->>DB: Load snapshot
    A->>DB: Update current state
    A->>H: Broadcast StateUpdated
    A-->>F: Restored state
    F-->>U: State restored
```

---

## Editor Flows

### Game Publication Workflow

```mermaid
sequenceDiagram
    participant E as Editor
    participant F as Frontend
    participant A as API
    participant DB as Database
    participant N as Notification Service
    participant Ad as Admin

    Note over E,DB: Create Game
    E->>F: Navigate to New Game
    E->>F: Fill form, upload PDF
    F->>A: POST /api/v1/admin/shared-games
    A->>DB: Insert (status: draft)
    A-->>F: { gameId }

    Note over E,DB: Add Content
    E->>F: Add quick questions
    F->>A: POST /api/v1/admin/shared-games/{id}/quick-questions
    E->>F: Add FAQ
    F->>A: POST /api/v1/admin/shared-games/{id}/faq

    Note over E,Ad: Submit for Approval
    E->>F: Click "Submit for Approval"
    F->>A: POST /api/v1/admin/shared-games/{id}/submit-for-approval
    A->>A: Validate completeness
    alt Validation fails
        A-->>F: 400 { errors }
        F-->>E: Show validation errors
    else Valid
        A->>DB: Update status = "pending_approval"
        A->>N: Notify admins
        A-->>F: 200 { status: "pending_approval" }
        F-->>E: "Submitted for review"
    end

    Note over Ad,DB: Admin Review
    Ad->>F: View pending approvals
    F->>A: GET /api/v1/admin/shared-games/pending-approvals
    A-->>F: Pending list

    Ad->>F: Click "Approve"
    F->>A: POST /api/v1/admin/shared-games/{id}/approve-publication
    A->>DB: Update status = "published"
    A->>N: Notify editor
    A-->>F: 200 { status: "published" }

    N-->>E: "Your game was approved!"
```

---

## Admin Flows

### User Tier Management

```mermaid
sequenceDiagram
    participant A as Admin
    participant F as Frontend
    participant API as API
    participant DB as Database
    participant C as Cache

    A->>F: Navigate to User Management
    F->>API: GET /api/v1/admin/users?page=1
    API->>DB: Query users
    API-->>F: User list
    F-->>A: Display users table

    A->>F: Click "Change Tier" on user
    F-->>A: Show tier selection dialog

    A->>F: Select "Premium"
    F->>API: PUT /api/v1/admin/users/{id}/tier
    API->>DB: Update user tier
    API->>DB: Create audit log
    API->>C: Invalidate user's quota cache
    API-->>F: 200 OK

    F-->>A: "Tier updated to Premium"

    Note over A,C: User immediately gets new limits
```

### System Configuration Flow

```mermaid
sequenceDiagram
    participant A as Admin
    participant F as Frontend
    participant API as API
    participant DB as Database
    participant C as Cache

    A->>F: Navigate to Configuration
    F->>API: GET /api/v1/admin/system/game-library-limits
    API->>DB: Get current limits
    API-->>F: { free: 5, normal: 20, premium: 50 }
    F-->>A: Display form

    A->>F: Change Free tier to 10 games
    A->>F: Click "Save"
    F->>API: PUT /api/v1/admin/system/game-library-limits

    API->>DB: Update configuration
    API->>DB: Create audit entry
    API->>C: Invalidate all quota caches
    API-->>F: 200 OK

    F-->>A: "Configuration saved"

    Note over A,C: All Free tier users now have 10 game limit
```

---

## Error Handling Patterns

### Generic Error Flow

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant A as API

    U->>F: Perform action
    F->>A: API request

    alt Success
        A-->>F: 200/201 Response
        F-->>U: Success feedback
    else Validation Error
        A-->>F: 400 { errors[] }
        F-->>U: Show field-level errors
    else Unauthorized
        A-->>F: 401
        F->>F: Redirect to login
    else Forbidden (Quota/Permission)
        A-->>F: 403 { error, details }
        F-->>U: Show upgrade/permission modal
    else Not Found
        A-->>F: 404
        F-->>U: Show "Not found" page
    else Rate Limited
        A-->>F: 429 { retryAfter }
        F-->>U: Show rate limit message
    else Server Error
        A-->>F: 500
        F-->>U: Show generic error toast
    end
```

---

*Last Updated: 2026-01-19*
