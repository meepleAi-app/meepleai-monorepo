# MeepleAI - Complete User Test Flows

**Data**: 2026-01-26
**Scopo**: Definizione completa di tutti i flussi utente per testing E2E e validazione funzionalità

---

## Executive Summary

| Categoria | Flussi Esistenti | Flussi Mancanti | Totale Richiesto |
|-----------|------------------|-----------------|------------------|
| **Authentication** | 8 | 6 | 14 |
| **Game Discovery** | 5 | 3 | 8 |
| **Library Management** | 6 | 4 | 10 |
| **AI Chat & RAG** | 7 | 5 | 12 |
| **Game Sessions** | 6 | 5 | 11 |
| **Editor Flows** | 8 | 4 | 12 |
| **Admin Flows** | 10 | 8 | 18 |
| **Error & Edge Cases** | 5 | 15 | 20 |
| **TOTALE** | **55** | **50** | **105** |

---

## 1. AUTHENTICATION FLOWS

### 1.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| AUTH-01 | Registration with Email | ✅ Testato | `auth.spec.ts` |
| AUTH-02 | Login with Email/Password | ✅ Testato | `auth.spec.ts` |
| AUTH-03 | Logout | ✅ Testato | `auth.spec.ts` |
| AUTH-04 | OAuth Registration (Google/GitHub) | ✅ Testato | `auth-oauth-registration.spec.ts` |
| AUTH-05 | OAuth Link/Unlink | ✅ Testato | `auth-oauth-advanced.spec.ts` |
| AUTH-06 | Password Reset | ✅ Testato | `auth-password-reset.spec.ts` |
| AUTH-07 | 2FA Enable/Disable | ✅ Testato | `auth-2fa-complete.spec.ts` |
| AUTH-08 | Session Management | ✅ Testato | `auth-logout-all-devices.spec.ts` |

### 1.2 Flussi Mancanti ❌ (Da Implementare)

#### AUTH-09: Email Verification Post-Registration
```gherkin
Feature: Email Verification

  Scenario: User completes email verification
    Given I have registered with email "user@example.com"
    And I have not verified my email
    When I click the verification link in my email
    Then I should see "Email verified successfully"
    And I should have full platform access

  Scenario: Resend verification email
    Given I am on the verification pending page
    When I click "Resend verification email"
    Then I should see "Verification email sent"
    And I should not be able to resend for 60 seconds

  Scenario: Expired verification token
    Given I have a verification token older than 24 hours
    When I click the verification link
    Then I should see "Verification link expired"
    And I should see option to resend
```

**Steps**:
1. POST `/auth/register` → Redirects to `/auth/verification-pending`
2. User checks email → Clicks link with token
3. GET `/auth/verify-email?token=xxx`
4. Backend validates token → Updates `email_verified = true`
5. Redirect to `/dashboard` with success toast

**Edge Cases**:
- Token expired (24h limit)
- Token already used
- Invalid/malformed token
- Rate limiting on resend (1/minute)
- User tries to access features before verification

---

#### AUTH-10: Account Lockout After Failed Attempts
```gherkin
Feature: Account Lockout Security

  Scenario: Account locked after 5 failed attempts
    Given I enter wrong password 5 times
    Then my account should be locked for 15 minutes
    And I should see "Account temporarily locked"

  Scenario: Unlock after waiting period
    Given my account was locked 15 minutes ago
    When I try to login with correct password
    Then I should be logged in successfully

  Scenario: Admin unlock
    Given my account is locked
    When an admin unlocks my account
    Then I should be able to login immediately
```

**Steps**:
1. Track failed attempts in `login_attempts` table
2. After 5 failures → Set `locked_until = NOW() + 15min`
3. Block login attempts during lockout
4. Clear attempts on successful login
5. Admin override via `/admin/users/{id}/unlock`

**Edge Cases**:
- Multiple IPs same account
- Account locked during OAuth attempt
- Password reset during lockout
- Progressive lockout (15min → 1h → 24h)

---

#### AUTH-11: Login Notification (New Device/Location)
```gherkin
Feature: Login Notifications

  Scenario: Notify on new device login
    Given I have login notifications enabled
    When I login from a new device
    Then I should receive an email notification
    And the notification should include device info and location

  Scenario: Suspicious login alert
    Given I logged in from Italy yesterday
    When I login from another country today
    Then I should see a security warning
    And I should be asked to verify my identity
```

**Steps**:
1. On login → Compare device fingerprint with known devices
2. If new device → Send notification email
3. Store device info: User-Agent, IP, approximate location
4. Flag suspicious: impossible travel, new country
5. Optional: Require 2FA on suspicious login

---

#### AUTH-12: Device Management
```gherkin
Feature: Device Management

  Scenario: View all logged-in devices
    Given I am logged in
    When I go to Settings > Security > Devices
    Then I should see all my active sessions with device info

  Scenario: Revoke specific device
    Given I have 3 active sessions
    When I click "Revoke" on a device
    Then that session should be terminated
    And other sessions should remain active
```

**Steps**:
1. GET `/users/me/sessions` → List all active sessions
2. Display: Device type, browser, last active, location
3. POST `/auth/sessions/{id}/revoke` → Terminate specific session
4. POST `/auth/sessions/revoke-all` → Logout all except current

---

#### AUTH-13: Remember Me / Extended Session
```gherkin
Feature: Extended Session

  Scenario: Remember me extends session
    Given I am on the login page
    When I check "Remember me" and login
    Then my session should last 30 days

  Scenario: Standard session duration
    Given I login without "Remember me"
    Then my session should expire in 24 hours
```

---

#### AUTH-14: API Key CRUD Complete Flow
```gherkin
Feature: API Key Management

  Scenario: Complete API key lifecycle
    Given I am logged in with 2FA enabled
    When I create a new API key with scopes ["read:games", "write:library"]
    And I use the key to call an API
    And I view usage statistics
    And I rotate the key
    And I revoke the key
    Then the key should no longer work
```

**Steps**:
1. POST `/api-keys` with name, scopes → Returns key (shown once)
2. Copy key to clipboard
3. Use key: `Authorization: ApiKey {key}`
4. GET `/api-keys/{id}/stats` → View usage
5. POST `/api-keys/{id}/rotate` → New value
6. DELETE `/api-keys/{id}` → Revoke

---

## 2. GAME DISCOVERY FLOWS

### 2.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| DISC-01 | Browse Game Catalog | ✅ Testato | `game-search-browse.spec.ts` |
| DISC-02 | Search Games by Name | ✅ Testato | `game-search-browse.spec.ts` |
| DISC-03 | Filter by Category/Players | ✅ Testato | `game-search-browse.spec.ts` |
| DISC-04 | View Game Details | ✅ Testato | `giochi-game-detail.spec.ts` |
| DISC-05 | BGG Import | ✅ Testato | `bgg-integration.spec.ts` |

### 2.2 Flussi Mancanti ❌

#### DISC-06: Autocomplete Search
```gherkin
Feature: Search Autocomplete

  Scenario: Autocomplete suggestions appear
    Given I am on the games page
    When I type "cat" in the search box
    Then I should see suggestions including "Catan", "Carcassonne", "Cat Lady"
    And suggestions should update as I type
```

---

#### DISC-07: Similar Games Recommendations
```gherkin
Feature: Similar Games

  Scenario: View similar games
    Given I am viewing "Catan" game page
    When I scroll to "Similar Games" section
    Then I should see games with similar mechanics
    And I should be able to add them to my library
```

---

#### DISC-08: Advanced Filters Combination
```gherkin
Feature: Complex Filtering

  Scenario: Multiple filters with no results
    Given I filter by players "7+" and playtime "<30min"
    When no games match
    Then I should see "No games found"
    And I should see suggestion to broaden filters
```

---

## 3. LIBRARY MANAGEMENT FLOWS

### 3.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| LIB-01 | Add Game to Library | ✅ Testato | `library.spec.ts` |
| LIB-02 | Remove Game from Library | ✅ Testato | `library.spec.ts` |
| LIB-03 | Mark as Favorite | ✅ Testato | `library.spec.ts` |
| LIB-04 | Bulk Operations | ✅ Testato | `library-bulk-operations.spec.ts` |
| LIB-05 | Library Quota Display | ✅ Testato | `library.spec.ts` |
| LIB-06 | Custom PDF Upload | ✅ Testato | `pdf-upload-journey.spec.ts` |

### 3.2 Flussi Mancanti ❌

#### LIB-07: Session Quota Display & Enforcement
```gherkin
Feature: Session Quota

  Scenario: View session quota
    Given I am a Free tier user
    When I view my sessions page
    Then I should see "2/3 sessions used"

  Scenario: Cannot create session at quota
    Given I have 3 active sessions (Free tier limit)
    When I try to create a new session
    Then I should see "Session limit reached"
    And I should see upgrade prompt

  Scenario: Premium unlimited sessions
    Given I am a Premium user
    When I view my sessions page
    Then I should see "Unlimited sessions"
```

**Steps**:
1. GET `/users/me/session-quota` → Returns `{current: 2, max: 3, tier: "Free"}`
2. Display in `SessionQuotaBar` component
3. POST `/sessions` checks quota before creation
4. If at limit → Return 403 with upgrade message

**Edge Cases**:
- Session ends while at quota → Quota freed
- Tier downgrade with sessions over limit
- Concurrent session creation race condition

---

#### LIB-08: Wishlist Management
```gherkin
Feature: Wishlist

  Scenario: Add game to wishlist
    Given I don't own "Wingspan"
    When I click "Add to Wishlist"
    Then the game should appear in my wishlist
    And I should see wishlist count increase

  Scenario: Move from wishlist to library
    Given "Wingspan" is in my wishlist
    When I click "I own this now"
    Then it should move to my library
    And be removed from wishlist
```

---

#### LIB-09: Game Loan Tracking
```gherkin
Feature: Game Loans

  Scenario: Mark game as loaned
    Given I have "Catan" in my library
    When I mark it as "Loaned to John"
    Then I should see loan indicator on the game
    And I should be able to send reminder

  Scenario: Send loan reminder
    Given "Catan" is loaned to john@email.com
    When I click "Send Reminder"
    Then an email should be sent
    And I should see "Reminder sent"
```

---

#### LIB-10: Play History & Statistics
```gherkin
Feature: Play History

  Scenario: View game play history
    Given I have played "Catan" 5 times
    When I view the game detail
    Then I should see play count and dates
    And I should see win/loss statistics
```

---

## 4. AI CHAT & RAG FLOWS

### 4.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| CHAT-01 | Ask Question (RAG) | ✅ Testato | `chat.spec.ts` |
| CHAT-02 | Streaming Response | ✅ Testato | `chat-streaming.spec.ts` |
| CHAT-03 | Citation Click | ✅ Testato | `chat-citations.spec.ts` |
| CHAT-04 | Multi-turn Conversation | ✅ Testato | `qa-multi-turn.spec.ts` |
| CHAT-05 | Context Switching | ✅ Testato | `chat-context-switching.spec.ts` |
| CHAT-06 | Export Chat | ✅ Testato | `chat-export.spec.ts` |
| CHAT-07 | Chat History | ✅ Testato | `chat.spec.ts` |

### 4.2 Flussi Mancanti ❌

#### CHAT-08: Stop Streaming Response
```gherkin
Feature: Stop Streaming

  Scenario: User stops long response
    Given the AI is streaming a long response
    When I click the "Stop" button
    Then the streaming should stop immediately
    And the partial response should be kept
    And I should be able to ask follow-up
```

**Steps**:
1. Show "Stop" button during streaming
2. On click → Abort SSE connection
3. Keep partial response in UI
4. Enable input for new question

---

#### CHAT-09: Response Feedback (Thumbs Up/Down)
```gherkin
Feature: Response Feedback

  Scenario: Provide positive feedback
    Given I received a helpful answer
    When I click the thumbs up button
    Then my feedback should be recorded
    And I should see "Thanks for your feedback"

  Scenario: Provide negative feedback with reason
    Given I received an unhelpful answer
    When I click thumbs down
    Then I should see feedback form
    And I can select reason and add comment
```

**Steps**:
1. POST `/agents/feedback` with `{messageId, rating: "positive"|"negative", reason?, comment?}`
2. Store feedback for model improvement
3. Show thank you message

---

#### CHAT-10: Voice Input
```gherkin
Feature: Voice Input

  Scenario: Ask question via voice
    Given I am on the chat page
    When I click the microphone button
    And I say "How do I set up Catan?"
    Then my question should appear in text
    And the AI should respond
```

**Steps**:
1. Request microphone permission
2. Use Web Speech API for transcription
3. Display transcribed text
4. Submit as regular question

---

#### CHAT-11: Share Chat Thread
```gherkin
Feature: Share Chat

  Scenario: Share chat via link
    Given I have a helpful chat thread
    When I click "Share"
    Then I should get a shareable link
    And anyone with the link can view (read-only)
```

---

#### CHAT-12: Quick Questions (Pre-defined)
```gherkin
Feature: Quick Questions

  Scenario: Use quick question
    Given I am on a game page with quick questions
    When I click "How do I set up the game?"
    Then the answer should appear immediately
    And I should be able to ask follow-ups
```

---

## 5. GAME SESSION FLOWS

### 5.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| SESS-01 | Create Session | ✅ Testato | Parziale in vari file |
| SESS-02 | Add Players | ✅ Testato | Parziale |
| SESS-03 | Session State Tracking | ✅ Testato | Parziale |
| SESS-04 | Complete Session | ✅ Testato | Parziale |
| SESS-05 | Session History | ✅ Testato | Parziale |
| SESS-06 | Pause/Resume | ✅ Testato | Parziale |

### 5.2 Flussi Mancanti ❌

#### SESS-07: Session Limits Enforcement
```gherkin
Feature: Session Limits

  Scenario: Free user at session limit
    Given I am a Free tier user with 3 active sessions
    When I try to create a new session
    Then I should see "Session limit reached (3/3)"
    And I should see "Upgrade to Normal for 10 sessions"

  Scenario: Normal user session limit
    Given I am a Normal tier user with 10 active sessions
    When I try to create a new session
    Then I should see "Session limit reached (10/10)"
    And I should see "Upgrade to Premium for unlimited"
```

---

#### SESS-08: Session Invite via Link
```gherkin
Feature: Session Invites

  Scenario: Invite player via link
    Given I have an active session
    When I generate an invite link
    And send it to a friend
    Then they should be able to join as a player

  Scenario: Join session via invite
    Given I have a session invite link
    When I click the link
    Then I should join the session as a viewer/player
```

---

#### SESS-09: Real-time State Sync (SignalR)
```gherkin
Feature: Real-time Sync

  Scenario: Multiple players see same state
    Given 2 players are in a session
    When Player 1 updates the score
    Then Player 2 should see the update instantly
```

---

#### SESS-10: AI Move Suggestions
```gherkin
Feature: AI Suggestions

  Scenario: Get AI suggestion during play
    Given I am in a chess session
    When I click "Suggest Move"
    Then I should see AI-recommended moves
    And I can apply a suggestion with one click
```

---

#### SESS-11: Session Statistics
```gherkin
Feature: Session Stats

  Scenario: View aggregated statistics
    Given I have played 20 sessions of Catan
    When I view session statistics
    Then I should see win rate, average duration, frequent players
```

---

## 6. EDITOR FLOWS

### 6.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| EDIT-01 | Create Game | ✅ Testato | `admin-game-creation.spec.ts` |
| EDIT-02 | Edit Game | ✅ Testato | `admin-games-workflow.spec.ts` |
| EDIT-03 | Upload PDF | ✅ Testato | `pdf-upload-journey.spec.ts` |
| EDIT-04 | PDF Processing Monitor | ✅ Testato | `pdf-processing-progress.spec.ts` |
| EDIT-05 | Submit for Approval | ✅ Testato | Parziale |
| EDIT-06 | FAQ Management | ✅ Testato | `game-faq.spec.ts` |
| EDIT-07 | BGG Bulk Import | ✅ Testato | `bgg-integration.spec.ts` |
| EDIT-08 | Version History | ⚠️ Parziale | - |

### 6.2 Flussi Mancanti ❌

#### EDIT-09: PDF OCR Validation
```gherkin
Feature: PDF OCR Quality

  Scenario: Review OCR extraction quality
    Given I uploaded a scanned PDF
    When processing completes
    Then I should see extracted text with confidence scores
    And I should be able to correct OCR errors
```

---

#### EDIT-10: Multi-language PDF Support
```gherkin
Feature: Multi-language PDFs

  Scenario: Upload PDF in multiple languages
    Given I have a PDF with English and German sections
    When I upload and process it
    Then both languages should be extracted correctly
    And search should work in both languages
```

---

#### EDIT-11: Queue Position Tracking
```gherkin
Feature: Approval Queue

  Scenario: Track position in approval queue
    Given I submitted a game for approval
    When I view submission status
    Then I should see "Position 3 of 12 in queue"
    And estimated review time
```

---

#### EDIT-12: Draft Preview
```gherkin
Feature: Draft Preview

  Scenario: Preview game before publishing
    Given I have a game in draft state
    When I click "Preview"
    Then I should see the game as users will see it
    And I should be able to edit from preview
```

---

## 7. ADMIN FLOWS

### 7.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| ADM-01 | User List & Search | ✅ Testato | `admin-users.spec.ts` |
| ADM-02 | User Role Assignment | ✅ Testato | `admin-users.spec.ts` |
| ADM-03 | Approval Queue | ✅ Testato | Parziale |
| ADM-04 | Feature Flags Toggle | ✅ Testato | `admin-configuration.spec.ts` |
| ADM-05 | System Health Monitor | ✅ Testato | `admin-infrastructure.spec.ts` |
| ADM-06 | Alert Configuration | ✅ Testato | `admin-alert-config.spec.ts` |
| ADM-07 | Prompt Management | ✅ Testato | `admin-prompts-management.spec.ts` |
| ADM-08 | Bulk Export | ✅ Testato | `admin-bulk-export.spec.ts` |
| ADM-09 | Analytics Dashboard | ✅ Testato | `admin-analytics.spec.ts` |
| ADM-10 | Audit Log | ⚠️ Parziale | - |

### 7.2 Flussi Mancanti ❌

#### ADM-11: Session Limits Configuration
```gherkin
Feature: Session Limits Admin

  Scenario: Configure session limits by tier
    Given I am an admin
    When I go to System Configuration > Session Limits
    Then I should see current limits per tier
    And I should be able to update them
```

**Steps**:
1. GET `/admin/system/session-limits`
2. Display form with: Free limit, Normal limit, Premium limit
3. PUT `/admin/system/session-limits` to update
4. Changes take effect immediately

---

#### ADM-12: PDF Limits Configuration
```gherkin
Feature: PDF Limits Admin

  Scenario: Configure PDF upload limits
    Given I am an admin
    When I go to System Configuration > PDF Limits
    Then I should see max file size, max pages, allowed types
    And I should be able to update them
```

---

#### ADM-13: Feature Flags Tier-Based
```gherkin
Feature: Tier-Based Feature Flags

  Scenario: Configure flag per tier
    Given I am on Feature Flags page
    When I toggle "Advanced RAG" for Free tier OFF
    And keep it ON for Normal and Premium
    Then Free users should not see Advanced RAG
```

---

#### ADM-14: AI Usage Analytics
```gherkin
Feature: AI Usage Dashboard

  Scenario: View AI token consumption
    Given I am an admin
    When I view AI Usage Analytics
    Then I should see total tokens consumed
    And breakdown by user, model, time period
    And estimated costs
```

---

#### ADM-15: User Impersonation
```gherkin
Feature: User Impersonation

  Scenario: Impersonate user for debugging
    Given I am a super admin
    When I impersonate user "john@example.com"
    Then I should see the platform as John sees it
    And all actions should be logged as impersonated
    And I should have a "Stop Impersonating" button
```

---

#### ADM-16: Account Lockout Management
```gherkin
Feature: Lockout Management

  Scenario: View locked accounts
    Given some users are locked out
    When I view Security > Locked Accounts
    Then I should see list of locked users with reason
    And I should be able to unlock them
```

---

#### ADM-17: Email Verification Management
```gherkin
Feature: Email Verification Admin

  Scenario: View unverified accounts
    Given some users haven't verified email
    When I view Users > Unverified
    Then I should see list with registration date
    And I should be able to manually verify or send reminder
```

---

#### ADM-18: 2FA Admin Override
```gherkin
Feature: 2FA Admin Actions

  Scenario: Disable 2FA for locked user
    Given a user is locked out of 2FA (lost device)
    When I use admin 2FA disable
    Then 2FA should be disabled for that user
    And an audit log should be created
```

---

## 8. ERROR & EDGE CASE FLOWS

### 8.1 Flussi Esistenti ✅

| ID | Flow | Status Test | File Test |
|----|------|-------------|-----------|
| ERR-01 | Network Error Recovery | ✅ Testato | `error-handling.spec.ts` |
| ERR-02 | Session Expiration | ✅ Testato | `session-expiration.spec.ts` |
| ERR-03 | 404 Page Not Found | ✅ Testato | `error-handling.spec.ts` |
| ERR-04 | 403 Forbidden | ✅ Testato | `rbac-authorization.spec.ts` |
| ERR-05 | Offline Mode | ✅ Testato | `offline-resilience.spec.ts` |

### 8.2 Flussi Mancanti ❌

#### ERR-06: Rate Limiting (429)
```gherkin
Feature: Rate Limit Handling

  Scenario: Hit rate limit
    Given I make too many requests quickly
    When I receive a 429 response
    Then I should see "Too many requests. Please wait."
    And I should see countdown timer
    And requests should auto-retry after cooldown
```

---

#### ERR-07: Token Expiration Mid-Operation
```gherkin
Feature: Token Expiration

  Scenario: Session expires during long operation
    Given my session expires while uploading PDF
    When the upload completes
    Then I should be redirected to login
    And my upload should be preserved (resumable)
```

---

#### ERR-08: Concurrent Edit Conflict
```gherkin
Feature: Concurrent Edit

  Scenario: Two editors edit same game
    Given Editor A and B both editing "Catan"
    When Editor A saves changes
    And Editor B tries to save
    Then Editor B should see conflict warning
    And should be able to merge or overwrite
```

---

#### ERR-09: PDF Processing Failure
```gherkin
Feature: PDF Processing Errors

  Scenario: Corrupted PDF upload
    Given I upload a corrupted PDF file
    When processing fails
    Then I should see specific error message
    And I should be able to retry with different file

  Scenario: OCR timeout
    Given I upload a very large PDF (500 pages)
    When processing times out
    Then I should see "Processing taking longer than expected"
    And I should have option to check back later
```

---

#### ERR-10: Payment/Tier Change Errors
```gherkin
Feature: Tier Change Errors

  Scenario: Downgrade with over-quota items
    Given I am Premium with 100 games in library
    When I downgrade to Free (limit 5)
    Then I should see warning about items over quota
    And I should choose which items to keep
```

---

#### ERR-11: OAuth Provider Unavailable
```gherkin
Feature: OAuth Errors

  Scenario: Google OAuth is down
    Given Google OAuth service is unavailable
    When I try to login with Google
    Then I should see "Service temporarily unavailable"
    And I should see alternative login options
```

---

#### ERR-12: SSE Stream Interruption
```gherkin
Feature: Stream Recovery

  Scenario: Network drops during streaming
    Given AI is streaming a response
    When network connection drops
    Then I should see "Connection lost"
    And partial response should be preserved
    And I should have "Retry" button
```

---

#### ERR-13: File Upload Validation Errors
```gherkin
Feature: Upload Validation

  Scenario: Wrong file type
    Given I try to upload a .exe file as PDF
    Then I should see "Invalid file type"
    Before the file is uploaded

  Scenario: File too large
    Given I try to upload a 200MB PDF
    Then I should see "File exceeds 100MB limit"
```

---

#### ERR-14: Quota Exceeded Actions
```gherkin
Feature: Quota Exceeded

  Scenario: Library full - add game
    Given my library is at 5/5 (Free limit)
    When I try to add a game
    Then I should see quota exceeded modal
    With option to upgrade or remove existing game
```

---

#### ERR-15: 2FA Recovery Code Usage
```gherkin
Feature: 2FA Recovery

  Scenario: Use backup code when device lost
    Given I lost my 2FA device
    When I enter a backup code
    Then I should be logged in
    And the backup code should be consumed
    And I should be prompted to setup new 2FA
```

---

#### ERR-16: Database Connection Error
```gherkin
Feature: Database Errors

  Scenario: Graceful degradation
    Given database connection is lost
    When I try to load my library
    Then I should see "Service temporarily unavailable"
    And cached data should be shown if available
```

---

#### ERR-17: AI Service Unavailable
```gherkin
Feature: AI Fallback

  Scenario: RAG service down
    Given AI service is unavailable
    When I ask a question
    Then I should see "AI service temporarily unavailable"
    And I should see cached FAQ if available
```

---

#### ERR-18: Invalid Deep Link
```gherkin
Feature: Deep Link Errors

  Scenario: Game no longer exists
    Given I have a bookmark to game ID 123
    When the game was deleted
    Then I should see "Game not found"
    And I should see suggestions for similar games
```

---

#### ERR-19: Concurrent Session Creation Race
```gherkin
Feature: Concurrent Session Race

  Scenario: Two devices create session simultaneously
    Given I am at 9/10 session limit
    When I create session from phone and laptop simultaneously
    Then only one should succeed
    And the other should see "Limit reached"
```

---

#### ERR-20: Import Conflicts
```gherkin
Feature: Import Errors

  Scenario: BGG import duplicate detection
    Given "Catan" already exists in catalog
    When I try to import "Catan" from BGG
    Then I should see "Game already exists"
    And option to merge or skip
```

---

## 9. ACCESSIBILITY & CROSS-BROWSER FLOWS

### ACC-01: Keyboard Navigation Complete Flow
```gherkin
Feature: Keyboard-Only Navigation

  Scenario: Complete user journey with keyboard only
    Given I navigate using only keyboard
    When I perform: Login → Browse games → Add to library → Start chat → Ask question
    Then all actions should be completable with Tab, Enter, Escape
    And focus should be visible at all times
```

### ACC-02: Screen Reader Announcements
```gherkin
Feature: Screen Reader Support

  Scenario: Important actions announced
    Given I use a screen reader
    When I add a game to library
    Then I should hear "Game added to library"
    When error occurs
    Then I should hear the error message
```

### ACC-03: Reduced Motion
```gherkin
Feature: Reduced Motion Support

  Scenario: Respect prefers-reduced-motion
    Given I have reduced motion enabled in OS
    When I use the application
    Then animations should be disabled or simplified
```

---

## 10. PRIORITIZATION MATRIX

### P0 - Critical (Must Test Before Release)

| ID | Flow | Reason |
|----|------|--------|
| AUTH-09 | Email Verification | Security feature |
| AUTH-10 | Account Lockout | Security feature |
| LIB-07 | Session Quota | New feature |
| SESS-07 | Session Limits | New feature |
| ADM-11 | Session Limits Config | Admin control |
| ERR-06 | Rate Limiting | Security |
| ERR-14 | Quota Exceeded | UX critical |

### P1 - High (Test in Sprint)

| ID | Flow | Reason |
|----|------|--------|
| AUTH-11 | Login Notifications | Security |
| AUTH-12 | Device Management | Security |
| CHAT-08 | Stop Streaming | UX improvement |
| CHAT-09 | Response Feedback | Quality improvement |
| ADM-12 | PDF Limits Config | Admin feature |
| ADM-13 | Tier Feature Flags | New feature |

### P2 - Medium (Test in Quarter)

| ID | Flow | Reason |
|----|------|--------|
| DISC-06 | Autocomplete Search | UX enhancement |
| LIB-08 | Wishlist | Feature expansion |
| CHAT-10 | Voice Input | Advanced feature |
| ADM-14 | AI Usage Analytics | Monitoring |
| ERR-08 | Concurrent Edit | Edge case |

### P3 - Low (Test When Capacity)

| ID | Flow | Reason |
|----|------|--------|
| LIB-09 | Game Loans | Nice to have |
| CHAT-11 | Share Chat | Nice to have |
| ADM-15 | User Impersonation | Admin convenience |

---

## 11. TEST FILE MAPPING

### Files to Create

```
apps/web/e2e/
├── auth/
│   ├── email-verification.spec.ts      # AUTH-09
│   ├── account-lockout.spec.ts         # AUTH-10
│   ├── login-notifications.spec.ts     # AUTH-11
│   └── device-management.spec.ts       # AUTH-12
├── library/
│   ├── session-quota.spec.ts           # LIB-07
│   ├── wishlist.spec.ts                # LIB-08
│   └── game-loans.spec.ts              # LIB-09
├── chat/
│   ├── stop-streaming.spec.ts          # CHAT-08
│   ├── response-feedback.spec.ts       # CHAT-09
│   └── voice-input.spec.ts             # CHAT-10
├── sessions/
│   ├── session-limits.spec.ts          # SESS-07
│   ├── session-invites.spec.ts         # SESS-08
│   └── realtime-sync.spec.ts           # SESS-09
├── admin/
│   ├── session-limits-config.spec.ts   # ADM-11
│   ├── pdf-limits-config.spec.ts       # ADM-12
│   ├── tier-feature-flags.spec.ts      # ADM-13
│   ├── ai-usage-analytics.spec.ts      # ADM-14
│   └── user-impersonation.spec.ts      # ADM-15
└── errors/
    ├── rate-limiting.spec.ts           # ERR-06
    ├── token-expiration.spec.ts        # ERR-07
    ├── concurrent-edit.spec.ts         # ERR-08
    └── quota-exceeded.spec.ts          # ERR-14
```

---

## 12. SUMMARY

### Copertura Attuale vs Target

| Area | Attuale | Target | Gap |
|------|---------|--------|-----|
| Authentication | 85% | 100% | 15% |
| Game Discovery | 90% | 100% | 10% |
| Library | 80% | 100% | 20% |
| AI Chat | 75% | 95% | 20% |
| Game Sessions | 65% | 95% | 30% |
| Editor | 85% | 95% | 10% |
| Admin | 70% | 95% | 25% |
| Error Handling | 50% | 90% | 40% |

### Next Actions

1. **Creare issues GitHub** per ogni flusso mancante priorità P0/P1
2. **Implementare backend** per features mancanti (email verification, session limits)
3. **Creare test E2E** seguendo la mappatura file
4. **Aggiornare documentazione** man mano che i test passano

---

**Autore**: Gap Analysis System
**Versione**: 1.0
**Data**: 2026-01-26
