# Admin Invite, Onboarding, Voice Chat & User Management

**Date**: 2026-03-11
**Status**: Draft
**Approach**: Incremental (4 independent phases)

## Overview

End-to-end flow: admin invites users via email → forced password change → guided onboarding wizard → voice-enabled agent chat → admin role management with full audit logging.

### Decisions Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Invite mechanism | Token-based link (no password in email) | Security — reuses password-reset pattern |
| Onboarding | Step-by-step wizard (skippable) | Guided but not blocking |
| Voice STT | Whisper API + Web Speech API fallback | Quality + resilience |
| Voice TTS | Browser-native SpeechSynthesis | Free, sufficient quality |
| Paid features | Whisper (cloud STT) gated by UserTier | Free users get browser-native STT |
| Admin UX | Inline role change + dedicated audit page | Quick ops + deep analysis |
| Audit scope | Everything (auth, activity, API, errors) | Full visibility, integrates with Epic #124 |
| Architecture | 4 independent phases | Low risk, incremental value |

### Open Issues Check

No duplicate issues found among 70+ open issues. Related:
- **#33** "Epic: Email, Notifiche & Calendario" — email infra exists, no invite flow
- **#130** "Audit Trail Viewer tab" — will be covered by Phase 4
- **#124** "Epic: Admin Infrastructure Panel" — audit log integrates with this

## Existing Infrastructure

| Component | Status | Location |
|-----------|--------|----------|
| `CreateUserCommand` | Exists | `Administration/Application/Commands/` |
| Email template system | Exists | `UserNotifications/` (queue + templates + event handlers) |
| Password reset flow | Exists | `Authentication/Application/Commands/` + `/reset-password` page |
| 5-tier role hierarchy | Exists | `SharedKernel/Domain/ValueObjects/Role.cs` (user/editor/creator/admin/superadmin) |
| `UserTier` on User | Exists | `Authentication/Domain/Entities/User.cs` |
| Agent builder modal | Exists | `components/admin/shared-games/AgentBuilderModal.tsx` |
| Chat with agent (SSE) | Exists | KnowledgeBase endpoints |
| `PUT /admin/users/{id}` (general update) | Exists | `Routing/AdminUserEndpoints.cs` |
| `POST /admin/users/bulk/role-change` | Exists | `Routing/AdminUserEndpoints.cs` |
| `GET /admin/users/{id}/role-history` | Exists | `Routing/AdminUserEndpoints.cs` |
| `PUT /admin/users/{id}/role` (dedicated) | **Needed** | Must be created in Phase 4 |
| Voice/speech features | None | Zero speech-to-text in codebase |
| Invite system | None | No invitation entity or flow |
| Forced password change | None | No `MustChangePassword` flag |
| Onboarding wizard | None | Welcome page auto-redirects to dashboard |

## Phase 1: Admin Invite System

### Backend

#### New Entity: `UserInvitation` (Authentication bounded context)

```
UserInvitation
├── Id: Guid
├── Email: string
├── Role: string
├── DisplayName: string
├── InvitationToken: string (hashed)
├── ExpiresAt: DateTime (48h from creation)
├── Status: InvitationStatus (Pending | Accepted | Expired | Revoked)
├── CreatedBy: Guid (admin userId)
├── AcceptedAt: DateTime?
├── CreatedAt: DateTime
└── UpdatedAt: DateTime
```

#### Modifications to `User` entity

- `MustChangePassword: bool` (default: false)
- `InvitedBy: Guid?` (nullable — tracks who invited)

#### Flow

1. Admin calls `POST /admin/users/invite` with `{email, role, displayName}`
2. Handler validates: reject if email already registered (409 Conflict). Reject if pending invitation exists for same email (409 Conflict with "invitation already pending" message). Creates `UserInvitation` only — **no User record yet**.
3. Email sent via new `SendInvitationEmailCommand` (MediatR, not direct service injection — `IEmailTemplateService` is `internal` to UserNotifications). Template data: `{inviteLink, adminName, expiresAt, displayName}`.
4. Email contains link: `/accept-invite?token=xxx`
5. User clicks → `POST /auth/accept-invite` validates token (checks: not expired, not already accepted, not revoked — single-use enforcement). Creates `User` record at this point (with random password, `MustChangePassword = true`, `OnboardingCompleted = false`, `EmailVerified = true` — admin-supplied email is trusted). Marks invitation as `Accepted`. Creates temporary session → redirect to `/change-password`.
6. User changes password via `UpdatePassword` (admin-path, no current password required — NOT `ChangePassword` which requires current password verification) → `MustChangePassword = false` → redirect to onboarding wizard.

#### Invitation cleanup

- Expired invitations: background job marks `Pending` → `Expired` after 48h. No ghost User records exist (User created only at acceptance).
- Revoked invitations: admin action, only affects `UserInvitation` status. No User cleanup needed.
- Re-invite: admin can create new invitation for same email after previous one is Expired or Revoked.

#### Login guard

On every login, if `MustChangePassword == true`, redirect to `/change-password`. No access to other pages. Enforced server-side: all authenticated endpoints (except `/change-password` and `/logout`) return 403 with `must_change_password` error code.

#### New Commands

- `InviteUserCommand(Email, Role, DisplayName)` → validates uniqueness, creates invitation only (no User yet), sends email via `SendInvitationEmailCommand`
- `AcceptInvitationCommand(Token)` → validates token (not expired, not used, not revoked), creates User, marks invitation accepted, creates session
- `RevokeInvitationCommand(InvitationId)` → marks as revoked (admin action)
- `SendInvitationEmailCommand(Email, TemplateData)` → new command in UserNotifications for invitation-specific emails (avoids abusing `EnqueueEmailCommand` which has document-processing schema: `FileName`, `DocumentUrl`, `ErrorMessage`)

#### New Queries

- `GetPendingInvitationsQuery` → list for admin UI
- `GetInvitationByTokenQuery(Token)` → for accept-invite page

#### New Endpoints

- `POST /admin/users/invite` → InviteUserCommand
- `POST /auth/accept-invite` → AcceptInvitationCommand
- `DELETE /admin/users/invitations/{id}` → RevokeInvitationCommand
- `GET /admin/users/invitations` → GetPendingInvitationsQuery

### Frontend

#### Admin UI

- Button "Invita Utente" in the user list page → opens modal
- Modal fields: Email, DisplayName, Role (dropdown: user/editor/creator/admin)
- Pending invitations table (with revoke action)

#### Auth Pages

- New page `/accept-invite` — validates token, shows welcome message, redirects to `/change-password`
- Modified `/change-password` — handles invite flow (post-change redirects to `/onboarding` instead of dashboard)

#### Email Template

- Template "invitation" — contains: admin name who invited, link to accept, expiration notice
- Sent via new `SendInvitationEmailCommand` (not `EnqueueEmailCommand` which has incompatible schema)
- Template rendered by a new `RenderInvitationEmail` method on a new or extended template service

## Phase 2: Onboarding Wizard

### Backend

#### Modifications to `User` entity

- `OnboardingCompleted: bool` (default: true for existing users, false for invited)
- `OnboardingCompletedAt: DateTime?`
- `OnboardingSkipped: bool` (default: false — for analytics)

#### New Command

- `CompleteOnboardingCommand(SkippedSteps: string[]?)` → sets `OnboardingCompleted = true`, records skipped steps in `AuditLogEntry.Details` (JSONB) when Phase 4 is active. Also stored in User entity for analytics queries.

#### Existing endpoints used (no changes needed)

- `POST /api/v1/user-library/games` — add game to collection
- `POST /api/v1/agent-definitions` — create agent
- Chat SSE endpoint — talk to agent

### Frontend

#### Wizard (`/onboarding`) — 3 steps

**Step 1: "Aggiungi il tuo primo gioco"**
- Search SharedGame catalog
- Click to add to collection
- Shows preview of selected game
- "Salta questo step →" link bottom-right

**Step 2: "Crea il tuo primo agente"**
- Simplified form (name auto-generated from game, KB cards pre-selected)
- Streamlined version of AgentBuilderModal
- "Salta questo step →" link bottom-right

**Step 3: "Prova a chiedergli qualcosa"**
- Inline mini-chat with the agent just created
- Pre-filled suggestions: "Qual è lo scopo del gioco?" / "Descrivi un turno di gioco"
- "Completa" button to finish
- "Salta questo step →" link bottom-right

#### Skip controls

- **Per-step skip**: "Salta questo step →" link on each step. Step marked as "skipped" (not "completed").
- **Skip all**: "Salta il wizard" link in header top-right. Confirmation: "Puoi trovare queste funzionalità nella dashboard quando vuoi". Sets `OnboardingCompleted = true`, `OnboardingSkipped = true`.

#### Navigation guard

- Next.js middleware: if `onboardingCompleted === false`, redirect to `/onboarding`
- Exceptions: `/change-password`, `/logout`, `/accept-invite`, `/api/*`
- **Interaction with email verification**: invited users have `EmailVerified = true` set at acceptance time (admin-supplied email is trusted), so the email verification guard does not interfere with onboarding

#### Dashboard reminder

- If user skipped onboarding, show dismissible banner: "Non hai completato il setup — riprendi da dove eri rimasto"
- Dismiss stored in localStorage, does not reappear

#### Visual design

- Progress bar at top: 3 dots with current step highlighted
- Glassmorphic style consistent with design system (bg-white/70, backdrop-blur-md, amber accents)
- Font: Quicksand headings, Nunito body

## Phase 3: Voice in Chat

### Backend

#### New endpoint (proxy for Whisper)

- `POST /api/v1/speech/transcribe`
  - Receives: audio blob (webm/ogg)
  - Sends to: OpenAI Whisper API
  - Returns: `{ text: string, language: string, duration: number }`
  - Auth: requires valid session
  - **Tier gating**: free users → `403 Forbidden` with message
  - Rate limit: max 60 requests/hour per user (paid tier)

#### Configuration

- New secret file: `infra/secrets/speech.secret`
  ```
  WHISPER_API_KEY=sk-...
  WHISPER_MODEL=whisper-1
  ```
- Priority: optional (speech features degrade gracefully)

### Frontend

#### Mic button in chat input

- `Mic` icon (Lucide) next to Send button
- Click → starts recording (`MediaRecorder API`)
- Icon turns red + pulses + shows duration
- Click again (or auto-stop after 30s) → sends audio

#### STT flow

1. Check user tier
2. If paid → try `POST /api/v1/speech/transcribe` (Whisper)
3. If free OR Whisper fails (503, timeout, no API key) → fallback to `webkitSpeechRecognition` / `SpeechRecognition`
4. Transcribed text appears in input field → user can edit before sending
5. Visual indicator: "🎙️ HD" (Whisper, paid) or "🎙️" (browser, free)

#### TTS for responses

- When user sent last message via mic, agent response is read aloud via `SpeechSynthesis API` (browser-native, free for all tiers)
- Speaker 🔊 button on each agent message for manual replay
- Global toggle "Auto-lettura" in chat header (default: on when using mic)
- Language auto-detect from response (Italian/English)

#### Tier gating rule

- Cloud APIs (Whisper STT, future cloud TTS) → paid tier only (tier `Normal` or `Premium` — NOT `Free`)
- Browser-native features (Web Speech API, SpeechSynthesis) → all tiers including `Free`
- Tier check: backend `tier != "free"`, frontend reads `user.tier` from session
- Tooltip for free users on mic: "Trascrizione base — Upgrade per qualità HD"

#### Browser permissions

- First mic click → requests microphone permission
- If denied → toast: "Permesso microfono necessario" with link to settings

#### New components

- `VoiceChatButton` — toggle recording, shows state (idle/recording/transcribing)
- `useAudioRecorder()` hook — manages MediaRecorder + audio blob
- `SpeechService` class — abstracts Whisper vs Web Speech API with automatic fallback
- `TextToSpeechButton` — speaker button on single message
- `useTextToSpeech()` hook — manages SpeechSynthesis lifecycle

## Phase 4: Admin User Management + Audit

### Backend

#### New Entity: `AuditLogEntry` (Administration bounded context)

```
AuditLogEntry
├── Id: Guid
├── UserId: Guid (subject — who was affected)
├── ActorId: Guid (who performed the action)
├── Action: AuditAction (enum)
├── Details: string (JSONB — flexible payload per event type)
├── IpAddress: string?
├── UserAgent: string?
├── CreatedAt: DateTime
```

#### `AuditAction` enum

```
RoleChanged, UserInvited, InviteAccepted, InviteRevoked,
PasswordChanged, Login, LoginFailed, Logout,
AccountSuspended, AccountUnlocked, AccountBanned,
TierChanged, OnboardingCompleted, OnboardingSkipped,
GameAdded, GameRemoved, AgentCreated, AgentDeleted,
ChatMessage, PdfUploaded, PdfDeleted,
VoiceTranscription, SettingsChanged,
ApiError, RateLimitExceeded
```

#### Event-driven architecture

Each bounded context publishes domain events → a centralized handler in Administration writes `AuditLogEntry`. **Exception**: `RoleChangedEvent` in Authentication bounded context must be modified to include `ChangedById: Guid` (currently only has `UserId`, `OldRole`, `NewRole`). The `User.AssignRole`/`User.UpdateRole` methods must accept and forward the actor ID.

#### New Endpoints

- `PUT /admin/users/{id}/role` → `ChangeUserRoleCommand(UserId, NewRole)` — dedicated role change endpoint (currently only bulk `POST /admin/users/bulk/role-change` exists)
- `GET /admin/audit-log` — paginated list, filters: userId, action, dateFrom, dateTo, actorId, search
- `GET /admin/audit-log/export` — CSV export
- `GET /admin/users/{id}/audit-log` — filtered log for single user

#### Domain event: `RoleChangedEvent` (modification)

- **Existing** in Authentication bounded context — must add `ChangedById: Guid` property
- Updated payload: userId, oldRole, newRole, changedById

#### Retention

- 90 days default, configurable via SystemConfiguration
- Background job for cleanup: `AuditLogCleanupJob` (Quartz)

### Frontend

#### Inline role change (existing user list)

- Role dropdown directly in user table row
- Change → confirmation modal: "Cambiare il ruolo di {nome} da {old} a {new}?"
- Success/error toast

#### "Activity" tab in user detail

- New tab in admin user detail page
- Chronological timeline of user events (login, actions, role changes)
- Filter by event type
- Infinite scroll or pagination

#### Dedicated Audit Log page (`/admin/monitor/audit`)

- Full-width table with all cross-user events
- Filters: user, event type, date range, actor
- Full-text search in details
- CSV export button
- Integrates with Epic #124 (#130 Audit Trail Viewer) — replaces that issue's scope

### Integration with existing issues

- **#130** "Audit Trail Viewer" → covered by this implementation, can be closed
- **#140** "Log Viewer" → remains separate (application logs, not audit)
- **#124** "Epic: Admin Infrastructure Panel" → audit becomes part of this epic

## Cross-Phase Concerns

### Database Migrations

Each phase adds its own migration:
- Phase 1: `AddUserInvitations` + `AddMustChangePasswordToUser`
- Phase 2: `AddOnboardingFieldsToUser`
- Phase 3: No migration (only new secret file)
- Phase 4: `AddAuditLogEntries` — **must include indexes**: `IX_AuditLogEntry_CreatedAt` (for retention cleanup job), `IX_AuditLogEntry_UserId_CreatedAt` (for per-user queries), `IX_AuditLogEntry_Action` (for event type filtering)

### Testing Strategy

| Phase | Unit Tests | Integration Tests | E2E Tests |
|-------|-----------|-------------------|-----------|
| 1 | Invitation commands, validators, handlers | DB: create/accept/revoke invitation | Full invite → accept → change password flow |
| 2 | Wizard step logic, skip handling | Onboarding completion command | Wizard 3-step walkthrough + skip |
| 3 | SpeechService fallback logic, tier gating | Whisper proxy endpoint | Mic → transcribe → send → TTS response |
| 4 | Audit event handlers, role change | Audit query filters, pagination | Role change → verify audit log entry |

### Security Considerations

- Invitation tokens: cryptographically random, hashed in DB, single-use, 48h expiry
- `MustChangePassword` enforced server-side (not just frontend redirect)
- Whisper proxy validates session + tier before forwarding audio
- Audit log entries are immutable (no update/delete endpoints)
- Audio blobs not stored — transcribed and discarded
- Rate limiting on voice endpoint (60/hour)

### Git Workflow

Each phase = separate feature branch → PR to `main-dev`:
- `feature/issue-XXX-admin-invite-system`
- `feature/issue-XXX-onboarding-wizard`
- `feature/issue-XXX-voice-chat`
- `feature/issue-XXX-admin-audit-management`

After creating each branch: `git config branch.<feature>.parent main-dev` to ensure PRs target correct base.

## Spec Review Fixes Applied

Issues found during automated spec review (10 total, all resolved):

| # | Issue | Fix |
|---|-------|-----|
| 1 | `EnqueueEmailCommand` incompatible schema | New `SendInvitationEmailCommand` introduced |
| 2 | `RoleChangedEvent` missing `ActorId` | Spec now requires modification to add `ChangedById` |
| 3 | `POST /admin/users/{id}/role` doesn't exist | Corrected infra table; new `PUT` endpoint in Phase 4 |
| 4 | `ChangePassword` requires current password | Flow now uses `UpdatePassword` (admin-path) |
| 5 | Onboarding guard conflicts with email verification | Invited users get `EmailVerified = true`; `/accept-invite` added to exceptions |
| 6 | Missing edge cases (duplicate email, token reuse) | Full validation rules + single-use enforcement documented |
| 7 | `IEmailTemplateService` is `internal` | Uses MediatR command (not direct service injection) |
| 8 | "Paid tier" not mapped to enum values | Explicitly: `Normal` or `Premium` = paid, `Free` = gated |
| 9 | No DB indexes on AuditLogEntry | 3 indexes specified in migration |
| 10 | Ghost user accounts at invite-send | User created at acceptance time only, not at invite-send |
