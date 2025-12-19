# Piano Test E2E Senza Mock - MeepleAI Application

## Overview

Questo documento definisce una suite completa di test E2E (End-to-End) **senza mock** per coprire tutte le funzionalità web dell'applicazione MeepleAI.

### Principi Test E2E

✅ **Nessun Mock**: Uso di servizi reali tramite Testcontainers  
✅ **Full Stack**: Test dalla UI/API fino al database  
✅ **Isolamento**: Ogni test deve essere indipendente  
✅ **Cleanup**: Teardown completo dopo ogni test  
✅ **Data Seeding**: Setup dati di test realistici

### Testcontainers Required

- **PostgreSQL** - Database principale
- **Redis** - Cache e sessions
- **MinIO** - Blob storage per PDF
- **Qdrant** - Vector database per RAG
- **Unstructured** - PDF text extraction service
- **SmolDocling** - Advanced PDF processing (opzionale)
- **SMTP Server** (MailHog) - Email notifications

### Priority Levels

- **P0** - Funzionalità critiche (blockers)
- **P1** - Funzionalità core (must-have)
- **P2** - Funzionalità secondarie (nice-to-have)

---

## 1. AUTHENTICATION CONTEXT

### 1.1 User Registration E2E [P0]

**Test ID**: `E2E-AUTH-001`  
**Scenario**: Complete user registration flow

**Steps**:

1. Navigate to `/register`
2. Fill form with valid data (email, password, displayName)
3. Submit registration
4. Verify user created in PostgreSQL `users` table
5. Verify session created in `user_sessions` table
6. Verify session cookie set in browser
7. Verify redirect to dashboard
8. Verify user can access authenticated pages

**Expected Results**:

- User record in DB with hashed password
- Active session with valid token
- HttpOnly cookie set with session token
- User logged in and can navigate protected routes

**Cleanup**:

- Delete test user from DB
- Clear sessions
- Clear cookies

---

### 1.2 User Login E2E [P0]

**Test ID**: `E2E-AUTH-002`  
**Scenario**: Login with existing credentials

**Steps**:

1. Seed user in database
2. Navigate to `/login`
3. Enter valid credentials
4. Submit login
5. Verify session created in DB
6. Verify cookie set
7. Verify redirect to dashboard
8. Verify authenticated state in UI

**Variants**:

- ✅ Login with valid credentials
- ❌ Login with invalid password
- ❌ Login with non-existent email
- ⏸️ Login with inactive account

---

### 1.3 Two-Factor Authentication E2E [P1]

**Test ID**: `E2E-AUTH-003`  
**Scenario**: Setup and verify 2FA

**Steps**:

1. Login as user
2. Navigate to `/settings`
3. Enable 2FA
4. Scan QR code (extract secret from response)
5. Generate OTP code programmatically
6. Verify OTP
7. Logout
8. Login again
9. Verify 2FA prompt appears
10. Submit valid OTP
11. Verify full login successful

**Expected Results**:

- QR code generated with valid TOTP secret
- OTP validation works
- Login requires OTP when 2FA enabled
- Backup codes generated

---

### 1.4 Password Reset Flow E2E [P1]

**Test ID**: `E2E-AUTH-004`  
**Scenario**: Complete password reset via email

**Steps**:

1. Seed user in DB
2. Navigate to `/reset-password`
3. Submit email
4. Verify reset token created in DB
5. Check MailHog for reset email
6. Extract reset link from email
7. Navigate to reset link
8. Submit new password
9. Verify password updated in DB (hash changed)
10. Verify old password no longer works
11. Verify can login with new password

**Testcontainers**: MailHog for SMTP

---

### 1.5 API Key Authentication E2E [P1]

**Test ID**: `E2E-AUTH-005`  
**Scenario**: Create and use API key

**Steps**:

1. Login as user
2. Navigate to `/settings`
3. Generate new API key
4. Verify key created in DB
5. Copy API key
6. Make API request with `Authorization: ApiKey {key}` header
7. Verify request authenticated
8. Revoke API key
9. Verify key marked as revoked in DB
10. Verify revoked key rejected

---

### 1.6 Session Management E2E [P1]

**Test ID**: `E2E-AUTH-006`  
**Scenario**: Manage multiple sessions

**Steps**:

1. Login from browser A (session 1)
2. Login from browser B (session 2)
3. Navigate to `/sessions`
4. Verify both sessions listed
5. Revoke session 2 from browser A
6. Verify browser B logged out
7. Verify session 2 marked revoked in DB

**Test Cases**:

- ✅ List all user sessions
- ✅ Revoke specific session
- ✅ Extend session expiration
- ✅ Logout from all devices
- ✅ Session expiration enforcement

---

### 1.7 OAuth Login E2E [P2]

**Test ID**: `E2E-AUTH-007`  
**Scenario**: Login via OAuth provider (mock OAuth server)

**Setup**: Mock OAuth server in Testcontainers

**Steps**:

1. Click "Login with Google"
2. Redirect to mock OAuth
3. Approve authorization
4. Callback to `/oauth-callback`
5. Verify user created/updated
6. Verify session created
7. Verify logged in

---

## 2. DOCUMENT PROCESSING CONTEXT

### 2.1 PDF Upload Standard E2E [P0]

**Test ID**: `E2E-PDF-001`  
**Scenario**: Upload PDF with full processing pipeline

**Steps**:

1. Login as user
2. Create test game in DB (or use existing)
3. Navigate to `/upload`
4. Select PDF file (< 10MB)
5. Fill metadata (gameId, language, version)
6. Submit upload
7. **Verify MinIO**: File stored in blob storage
8. **Verify PostgreSQL**: `pdf_documents` record created with status `processing`
9. **Poll progress**: Wait for text extraction completion
10. **Verify Unstructured**: PDF text extracted
11. **Verify PostgreSQL**: `extracted_text` field populated
12. **Poll progress**: Wait for indexing completion
13. **Verify Qdrant**: Chunks indexed in vector DB
14. **Verify PostgreSQL**: Status updated to `completed`, `indexed_at` set
15. Navigate to game detail page
16. Verify PDF listed
17. Download PDF
18. Verify file content matches upload

**Expected Results**:

- File in MinIO under correct path
- DB record with all metadata
- Text extracted successfully
- Semantic search working (can find chunks)

**Cleanup**:

- Delete PDF from MinIO
- Delete chunks from Qdrant
- Delete DB record

---

### 2.2 PDF Upload Chunked E2E [P1]

**Test ID**: `E2E-PDF-002`  
**Scenario**: Upload large PDF via chunked upload

**Steps**:

1. Login as user
2. Prepare large PDF file (> 10MB)
3. Call `/api/v1/ingest/pdf/chunked/init`
4. Verify chunked upload session created in DB
5. Split file into chunks (use returned chunkSizeBytes)
6. Upload each chunk via `/api/v1/ingest/pdf/chunked/chunk`
7. Poll `/api/v1/ingest/pdf/chunked/{sessionId}/status`
8. Verify progress updated after each chunk
9. Call `/api/v1/ingest/pdf/chunked/complete`
10. Verify file assembled in MinIO
11. Verify processing triggered
12. Wait for completion
13. Verify fully processed

**Error Scenarios**:

- ❌ Missing chunk detection
- ⏱️ Session expiration
- 🔄 Retry failed chunk

---

### 2.3 PDF Processing Cancellation E2E [P1]

**Test ID**: `E2E-PDF-003`  
**Scenario**: Cancel PDF processing mid-flight

**Steps**:

1. Upload PDF
2. Wait for processing to start
3. Call `/api/v1/pdfs/{pdfId}/processing` DELETE
4. Verify processing cancelled
5. Verify status updated to `cancelled` in DB
6. Verify background task stopped
7. Verify partial data cleaned up

---

### 2.4 PDF Download with Authorization E2E [P1]

**Test ID**: `E2E-PDF-004`  
**Scenario**: Test Row-Level Security for PDF download

**Steps**:

1. User A uploads PDF
2. Verify User A can download
3. Login as User B
4. Attempt to download User A's PDF
5. **Verify 403 Forbidden**
6. Login as Admin
7. Verify Admin can download any PDF

**RLS Test**:

- ✅ Owner can download
- ❌ Other users cannot download
- ✅ Admin can download

---

### 2.5 PDF Visibility Toggle E2E [P2]

**Test ID**: `E2E-PDF-005`  
**Scenario**: Set PDF as public/private

**Steps**:

1. Upload PDF as User A
2. Set visibility to `public`
3. Verify `is_public` flag in DB
4. Logout
5. Login as User B
6. Verify can see PDF in public library
7. Login as User A
8. Set visibility to `private`
9. Login as User B
10. Verify PDF no longer visible

---

### 2.6 PDF Deletion E2E [P1]

**Test ID**: `E2E-PDF-006`  
**Scenario**: Delete PDF with full cleanup

**Steps**:

1. Upload and process PDF completely
2. Verify file in MinIO
3. Verify chunks in Qdrant
4. Verify record in PostgreSQL
5. Delete PDF via `/api/v1/pdf/{pdfId}` DELETE
6. **Verify MinIO**: File deleted
7. **Verify Qdrant**: Chunks deleted
8. **Verify PostgreSQL**: Record soft-deleted or removed
9. **Verify Audit**: Deletion logged in `audit_log` table
10. Verify PDF no longer appears in lists

---

### 2.7 PDF Re-indexing E2E [P2]

**Test ID**: `E2E-PDF-007`  
**Scenario**: Force re-index for stuck PDF

**Steps**:

1. Upload PDF
2. Mark as `failed` in DB (simulate failure)
3. Call `/api/v1/ingest/pdf/{pdfId}/index` POST
4. Verify processing restarted
5. Verify successful completion
6. Verify chunks in Qdrant

---

## 3. GAME MANAGEMENT CONTEXT

### 3.1 Game CRUD E2E [P0]

**Test ID**: `E2E-GAME-001`  
**Scenario**: Complete game lifecycle

**Steps**:

1. Login as Admin
2. Navigate to `/editor`
3. Create new game with metadata
4. Verify game in DB
5. Navigate to `/games`
6. Verify game appears in list
7. Click game to view details
8. Update game metadata
9. Verify updates in DB
10. Delete game (if supported)
11. Verify soft-delete in DB

---

### 3.2 Game Search E2E [P1]

**Test ID**: `E2E-GAME-002`  
**Scenario**: Search games by name

**Steps**:

1. Seed multiple games in DB
2. Navigate to `/games`
3. Enter search query
4. Verify filtered results
5. Test pagination
6. Verify page sizes respected

---

### 3.3 BGG Integration E2E [P1]

**Test ID**: `E2E-GAME-003`  
**Scenario**: Search and import from BoardGameGeek

**Steps**:

1. Login as Editor
2. Navigate to game creation
3. Search BGG with query (e.g., "Catan")
4. Select game from results
5. Verify metadata pre-filled from BGG
6. Save game
7. Verify BGG ID stored in DB

**Mock**: BGG API può essere mockato se troppo lento, ma preferibilmente test reale

---

### 3.4 Game Session Complete Flow E2E [P0]

**Test ID**: `E2E-SESSION-001`  
**Scenario**: Start, play, complete game session

**Steps**:

1. Login as user
2. Navigate to game detail
3. Click "Start Session"
4. Add players with names and colors
5. Verify session created in DB with status `active`
6. Verify players in `game_session_players` table
7. Pause session
8. Verify status `paused`
9. Resume session
10. Verify status `active`
11. Complete session with winner
12. Verify status `completed`
13. Verify winner recorded
14. Verify `completed_at` timestamp
15. Navigate to `/sessions`
16. Verify session in history

---

### 3.5 Session Statistics E2E [P2]

**Test ID**: `E2E-SESSION-002`  
**Scenario**: Calculate session statistics

**Steps**:

1. Seed multiple completed sessions
2. Call `/api/v1/sessions/statistics?gameId={id}`
3. Verify correct aggregations:
   - Total sessions
   - Average duration
   - Top players by wins
   - Play frequency

---

### 3.6 Game FAQs E2E [P1]

**Test ID**: `E2E-FAQ-001`  
**Scenario**: Manage game FAQs

**Steps**:

1. Login as Editor
2. Navigate to game detail
3. Add FAQ (question + answer)
4. Verify FAQ created in DB
5. Upvote FAQ
6. Verify upvote count incremented
7. Update FAQ
8. Verify updates persisted
9. Delete FAQ
10. Verify soft-delete

---

## 4. KNOWLEDGE BASE & AI CONTEXT

### 4.1 QA Agent Full Pipeline E2E [P0]

**Test ID**: `E2E-RAG-001`  
**Scenario**: Question answering with RAG (full pipeline)

**Prerequisites**:

- Game with indexed PDF

**Steps**:

1. Upload PDF for game
2. Wait for indexing completion
3. Verify chunks in Qdrant
4. Login as user
5. Navigate to `/board-game-ai`
6. Select game
7. Ask question (e.g., "How many players?")
8. **Verify RAG flow**:
   - Query embedding generated
   - Qdrant search executed
   - Relevant chunks retrieved
   - LLM prompt constructed
   - LLM response generated
9. Verify answer displayed
10. Verify citations shown
11. Verify confidence score
12. **Verify PostgreSQL**: `ai_requests` log created
13. **Verify telemetry**: Token count, latency logged

**Assertions**:

- Answer relevant to question
- Citations include correct page numbers
- Confidence score > 0.5 for good answers
- Response time < 10s

---

### 4.2 QA Streaming E2E [P1]

**Test ID**: `E2E-RAG-002`  
**Scenario**: Streaming question answering via SSE

**Steps**:

1. Setup as above
2. Navigate to `/chat`
3. Ask question
4. Establish SSE connection to `/api/v1/agents/qa/stream`
5. **Verify stream events**:
   - `token` events with incremental text
   - `citations` event with snippets
   - `complete` event with metadata
6. Verify UI updates in real-time
7. Verify final answer matches streaming output
8. Verify follow-up questions generated

---

### 4.3 Explain Agent E2E [P1]

**Test ID**: `E2E-RAG-003`  
**Scenario**: Generate topic explanation

**Steps**:

1. Setup indexed game
2. Navigate to explain interface
3. Submit topic (e.g., "setup")
4. Verify detailed script generated
5. Verify estimated reading time calculated
6. Verify citations included

---

### 4.4 Setup Guide E2E [P1]

**Test ID**: `E2E-RAG-004`  
**Scenario**: Generate step-by-step setup guide

**Steps**:

1. Setup indexed game
2. Navigate to `/setup`
3. Select game
4. Request setup guide
5. Verify streaming steps via SSE
6. **Verify each step includes**:
   - Title
   - Description
   - Estimated time
   - Optional illustrations
7. Verify total time calculated
8. Verify guide saved to DB (if persistence enabled)

---

### 4.5 Chess Agent E2E [P2]

**Test ID**: `E2E-CHESS-001`  
**Scenario**: Chess knowledge Q&A with FEN analysis

**Prerequisites**:

- Chess knowledge indexed in Qdrant

**Steps**:

1. Index chess knowledge (Admin action)
2. Navigate to `/chess`
3. Ask question without FEN (e.g., "What is a fork?")
4. Verify general chess answer
5. Ask question with FEN position
6. Verify position-specific analysis
7. Verify move suggestions
8. Verify explanation quality

---

### 4.6 RAG Cache Bypass E2E [P2]

**Test ID**: `E2E-RAG-005`  
**Scenario**: Test cache behavior for RAG

**Steps**:

1. Ask question with `bypassCache=false`
2. Note response time T1
3. Ask same question again
4. Verify response time T2 < T1 (cache hit)
5. Verify **Redis**: Cache entry exists
6. Ask with `bypassCache=true`
7. Verify fresh LLM call made
8. Verify response time similar to T1

---

### 4.7 Follow-Up Questions E2E [P1]

**Test ID**: `E2E-RAG-006`  
**Scenario**: Automatic follow-up question generation

**Steps**:

1. Ask question
2. Verify answer received
3. Verify 3-5 follow-up questions generated
4. Verify follow-ups relevant to context
5. Click follow-up question
6. Verify query auto-populated
7. Submit and verify answer

---

### 4.8 Agent Feedback E2E [P2]

**Test ID**: `E2E-RAG-007`  
**Scenario**: Submit feedback on AI response

**Steps**:

1. Ask question
2. Receive answer
3. Click thumbs-up
4. Verify feedback recorded in DB
5. Ask another question
6. Click thumbs-down
7. Verify negative feedback recorded
8. Query analytics endpoint
9. Verify feedback aggregated correctly

---

### 4.9 Multi-Document RAG E2E [P1]

**Test ID**: `E2E-RAG-008`  
**Scenario**: Query with multiple PDF sources

**Steps**:

1. Upload PDF 1 for game
2. Upload PDF 2 for same game
3. Wait for both indexed
4. Ask question
5. Verify chunks from both PDFs retrieved
6. Verify citations distinguish sources
7. Verify answer synthesizes both sources

---

### 4.10 Hybrid Search Mode E2E [P2]

**Test ID**: `E2E-RAG-009`  
**Scenario**: Test different search modes

**Test Cases**:

- Search with `mode=Semantic`
- Search with `mode=Keyword`
- Search with `mode=Hybrid` (default)

**Verify**: Different results for each mode

---

## 5. ADMINISTRATION CONTEXT

### 5.1 User Management E2E [P1]

**Test ID**: `E2E-ADMIN-001`  
**Scenario**: Admin manages users

**Steps**:

1. Login as Admin
2. Navigate to `/admin`
3. View user list
4. Change user role (User → Editor)
5. Verify role updated in DB
6. Disable user account
7. Verify user cannot login
8. Re-enable user
9. Verify user can login

---

### 5.2 Analytics Dashboard E2E [P1]

**Test ID**: `E2E-ADMIN-002`  
**Scenario**: View AI usage analytics

**Prerequisites**:

- Seed AI request logs

**Steps**:

1. Login as Admin
2. Navigate to analytics dashboard
3. View usage charts
4. Filter by date range
5. Filter by user
6. Filter by endpoint
7. View token consumption
8. View cost estimates
9. Export report

---

### 5.3 Audit Log E2E [P1]

**Test ID**: `E2E-ADMIN-003`  
**Scenario**: Query audit logs

**Steps**:

1. Perform auditable actions (PDF delete, user role change)
2. Login as Admin
3. Navigate to audit log
4. Filter by action type
5. Filter by user
6. Filter by entity
7. Verify all actions logged correctly
8. Verify sensitive data not exposed

---

### 5.4 API Key Management E2E [P1]

**Test ID**: `E2E-ADMIN-004`  
**Scenario**: Admin creates API key for user API keys

**Steps**:

1. Login as Admin
2. Navigate to API key management
3. Create key with specific scopes
4. Verify key in DB
5. Test key with allowed scope
6. Test key with disallowed scope (should fail)
7. Rotate key
8. Verify old key revoked
9. Verify new key works

---

## 6. SYSTEM CONFIGURATION CONTEXT

### 6.1 Feature Flags E2E [P1]

**Test ID**: `E2E-CONFIG-001`  
**Scenario**: Toggle feature flags

**Steps**:

1. Login as Admin
2. Navigate to feature flags
3. Disable `Features.PdfUpload`
4. Verify upload endpoint returns 403
5. Verify upload UI disabled
6. Re-enable `Features.PdfUpload`
7. Verify upload works

**Test Multiple Flags**:

- `Features.StreamingResponses`
- `Features.SetupGuideGeneration`

---

### 6.2 Dynamic Configuration E2E [P2]

**Test ID**: `E2E-CONFIG-002`  
**Scenario**: Update configuration at runtime

**Steps**:

1. Update `SessionExpirationDays` from 30 → 7
2. Create new session
3. Verify session expires in 7 days
4. Update `MaxUploadSizeMB`
5. Attempt upload exceeding limit
6. Verify rejected

---

### 6.3 Cache Clearing E2E [P2]

**Test ID**: `E2E-CONFIG-003`  
**Scenario**: Clear application cache

**Steps**:

1. Trigger cached operation (e.g., RAG query)
2. Verify cache entry in Redis
3. Call cache clear endpoint
4. Verify Redis keys deleted
5. Repeat operation
6. Verify fresh execution (no cache hit)

---

## 7. USER NOTIFICATIONS CONTEXT

### 7.1 Alert Configuration E2E [P2]

**Test ID**: `E2E-NOTIF-001`  
**Scenario**: Configure alerts for events

**Steps**:

1. Login as user
2. Navigate to notification settings
3. Enable alert for "PDF processing complete"
4. Upload PDF
5. Wait for processing
6. Verify alert generated
7. Verify notification displayed in UI
8. Verify notification persisted in DB

---

### 7.2 Email Notifications E2E [P2]

**Test ID**: `E2E-NOTIF-002`  
**Scenario**: Email notifications via SMTP

**Testcontainers**: MailHog

**Steps**:

1. Enable email notifications
2. Trigger notifiable event
3. Check MailHog for email
4. Verify email content
5. Verify email sent to correct recipient

---

## 8. WORKFLOW INTEGRATION CONTEXT

### 8.1 n8n Workflow Trigger E2E [P2]

**Test ID**: `E2E-WORKFLOW-001`  
**Scenario**: Trigger n8n workflow from app event

**Setup**: Mock n8n webhook endpoint

**Steps**:

1. Configure workflow trigger for "PDF indexed"
2. Upload and process PDF
3. Verify webhook called
4. Verify payload includes PDF metadata
5. Verify workflow execution logged

---

## 9. CROSS-FUNCTIONAL E2E TESTS

### 9.1 Complete User Journey E2E [P0]

**Test ID**: `E2E-JOURNEY-001`  
**Scenario**: New user to first AI answer

**Steps**:

1. Register new user
2. Login
3. Browse games
4. Upload PDF for game
5. Wait for processing completion
6. Ask question about uploaded PDF
7. Receive AI-generated answer
8. Provide feedback
9. View session history
10. Logout

**Duration**: ~5-10 minutes  
**Services Used**: All

---

### 9.2 Multi-User Concurrent Access E2E [P1]

**Test ID**: `E2E-JOURNEY-002`  
**Scenario**: Multiple users accessing system simultaneously

**Steps**:

1. User A uploads PDF
2. User B uploads different PDF (concurrent)
3. Both users ask questions (concurrent)
4. Verify no data bleed between users
5. Verify both get correct answers
6. Verify sessions isolated

---

### 9.3 Large PDF Processing E2E [P1]

**Test ID**: `E2E-STRESS-001`  
**Scenario**: Process 50+ page PDF

**Steps**:

1. Upload large PDF (50-100 pages)
2. Monitor processing time
3. Verify all pages extracted
4. Verify all chunks indexed
5. Verify search quality across document
6. Verify memory usage acceptable

**Assertions**:

- Processing completes in < 5 minutes
- No memory leaks
- All pages accessible

---

### 9.4 Session Expiration E2E [P1]

**Test ID**: `E2E-SESSION-EXPIRE-001`  
**Scenario**: Test session expiration enforcement

**Steps**:

1. Login with shortened session (e.g., 1 minute)
2. Perform action
3. Wait for expiration
4. Attempt authenticated action
5. Verify 401 Unauthorized
6. Verify redirected to login
7. Login again
8. Verify can continue

---

### 9.5 Rate Limiting E2E [P2]

**Test ID**: `E2E-RATE-LIMIT-001`  
**Scenario**: Test rate limiting on AI endpoints

**Steps**:

1. Configure rate limit (e.g., 10 requests/minute)
2. Send 10 QA requests rapidly
3. Verify all succeed
4. Send 11th request
5. Verify 429 Too Many Requests
6. Wait for window reset
7. Verify requests succeed again

---

## 10. ERROR HANDLING & RECOVERY E2E

### 10.1 Database Connection Loss E2E [P1]

**Test ID**: `E2E-ERROR-001`  
**Scenario**: Graceful degradation on DB failure

**Steps**:

1. Perform successful operation
2. Stop PostgreSQL container
3. Attempt database operation
4. Verify graceful error message (not 500)
5. Restart PostgreSQL
6. Verify service recovers
7. Verify operation succeeds

---

### 10.2 MinIO Connection Loss E2E [P2]

**Test ID**: `E2E-ERROR-002`  
**Scenario**: Handle blob storage unavailability

**Steps**:

1. Stop MinIO container
2. Attempt PDF upload
3. Verify user-friendly error
4. Restart MinIO
5. Retry upload
6. Verify success

---

### 10.3 Qdrant Connection Loss E2E [P2]

**Test ID**: `E2E-ERROR-003`  
**Scenario**: RAG degradation without vector DB

**Steps**:

1. Stop Qdrant container
2. Attempt QA query
3. Verify fallback behavior (e.g., keyword search or error message)
4. Restart Qdrant
5. Verify normal RAG resumes

---

### 10.4 LLM Provider Failure E2E [P1]

**Test ID**: `E2E-ERROR-004`  
**Scenario**: Handle LLM API failures

**Setup**: Mock LLM endpoint to fail

**Steps**:

1. Trigger QA query
2. Simulate LLM timeout
3. Verify retry logic
4. Verify eventual fallback or clear error
5. Verify system stability

---

## 11. SECURITY E2E TESTS

### 11.1 SQL Injection Prevention E2E [P0]

**Test ID**: `E2E-SEC-001`  
**Scenario**: Test input sanitization

**Steps**:

1. Attempt SQL injection in search field
2. Attempt SQL injection in user input (question)
3. Verify queries parameterized
4. Verify no data leak
5. Verify error handling

---

### 11.2 XSS Prevention E2E [P0]

**Test ID**: `E2E-SEC-002`  
**Scenario**: Test XSS protection

**Steps**:

1. Submit malicious script in input
2. Verify output escaped
3. Verify script not executed
4. Test multiple injection points

---

### 11.3 CSRF Protection E2E [P0]

**Test ID**: `E2E-SEC-003`  
**Scenario**: Verify CSRF tokens

**Steps**:

1. Login
2. Attempt state-changing request without CSRF token
3. Verify request rejected
4. Repeat with valid token
5. Verify request succeeds

---

### 11.4 Authorization Bypass E2E [P0]

**Test ID**: `E2E-SEC-004`  
**Scenario**: Attempt privilege escalation

**Steps**:

1. Login as regular User
2. Attempt Admin-only action (direct API call)
3. Verify 403 Forbidden
4. Attempt to access other user's data
5. Verify 403 Forbidden

---

## 12. PERFORMANCE E2E TESTS

### 12.1 RAG Response Time E2E [P1]

**Test ID**: `E2E-PERF-001`  
**Scenario**: Measure RAG performance

**Steps**:

1. Execute 100 QA queries
2. Measure P50, P95, P99 latency
3. **Assertions**:
   - P50 < 3s
   - P95 < 7s
   - P99 < 10s

---

### 12.2 Concurrent User Load E2E [P2]

**Test ID**: `E2E-PERF-002`  
**Scenario**: Load test with 50 concurrent users

**Steps**:

1. Spin up 50 virtual users
2. Each performs: login → upload → query
3. Measure throughput
4. Measure error rate
5. **Assertions**:
   - Error rate < 1%
   - No crashes

---

## IMPLEMENTATION PRIORITY

### Phase 1 - Critical Path (P0)

1. `E2E-AUTH-001` - Registration
2. `E2E-AUTH-002` - Login
3. `E2E-PDF-001` - PDF Upload & Processing
4. `E2E-GAME-001` - Game CRUD
5. `E2E-SESSION-001` - Game Session Flow
6. `E2E-RAG-001` - QA Agent Full Pipeline
7. `E2E-JOURNEY-001` - Complete User Journey
8. `E2E-SEC-001/002/003/004` - Security tests

### Phase 2 - Core Features (P1)

9. All P1-tagged tests (30+ tests)

### Phase 3 - Secondary Features (P2)

10. All P2-tagged tests (15+ tests)

---

## TEST INFRASTRUCTURE REQUIREMENTS

### Testcontainers Setup

```typescript
// docker-compose.e2e.yml equivalent in Testcontainers
const postgres = await new PostgreSqlContainer().start();
const redis = await new GenericContainer("redis:7-alpine").start();
const minio = await new MinIOContainer().start();
const qdrant = await new GenericContainer("qdrant/qdrant").start();
const unstructured = await new GenericContainer(
  "downloads.unstructured.io/unstructured-io/unstructured-api:latest"
).start();
const mailhog = await new GenericContainer("mailhog/mailhog").start();
```

### Test Data Seeding

- Reusable factories for User, Game, PDF, Session
- Realistic test data (not just "test@test.com")
- SQL migration verification

### Cleanup Strategy

- Transaction rollback per test (where possible)
- Explicit cleanup in `afterEach`
- Container restart between test suites

---

## TEST EXECUTION

### Local Development

```bash
npm run test:e2e:local
```

### CI/CD

```bash
npm run test:e2e:ci
```

### Selective Execution

```bash
npm run test:e2e -- --grep "E2E-AUTH"  # Only auth tests
npm run test:e2e -- --grep "P0"        # Only P0 tests
```

---

## METRICS & REPORTING

### Coverage Goals

- **P0 Tests**: 100% coverage
- **P1 Tests**: 80% coverage
- **P2 Tests**: 50% coverage

### Test Reports

- HTML report with screenshots on failure
- Video recording for debugging
- Performance metrics dashboard
- Flaky test tracking

---

## TOTAL TEST COUNT

**Estimated**: **80-100 E2E tests**

- Authentication: 7 tests
- Document Processing: 7 tests
- Game Management: 6 tests
- Knowledge Base & AI: 10 tests
- Administration: 4 tests
- System Configuration: 3 tests
- Notifications: 2 tests
- Workflows: 1 test
- Cross-Functional: 5 tests
- Error Handling: 4 tests
- Security: 4 tests
- Performance: 2 tests

**Total Duration**: 2-4 hours (parallel execution)

---

## NOTES

- Tests should be **deterministic** - no flaky tests tolerated
- Use **realistic data** - avoid "test123" everywhere
- **Screenshot on failure** for debugging
- **Video recording** for complex UI flows
- **Database assertions** are mandatory - don't just test API responses
- **Service logs** should be collected for debugging
- All tests must **clean up** - no pollution between tests
