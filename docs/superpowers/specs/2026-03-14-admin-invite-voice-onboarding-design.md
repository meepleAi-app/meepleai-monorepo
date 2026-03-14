# Admin Invite + Voice Onboarding — Design Spec

**Date**: 2026-03-14
**Status**: Revised (post spec-review)

## Summary

Admin invites users via email. Invited users accept, set password, go through a skippable onboarding wizard (profile → interests → add game → create agent), then interact with the agent via voice. Admin can change user roles and view audit logs. A single E2E Playwright test validates the entire user story.

## User Story

> As an admin, I want to invite users via email so they can onboard, add games, create agents, and interact with them via voice. I also want to manage user roles and view audit logs.

---

## 1. Admin Invitation Flow

### UI: `/admin/users` (existing page)

- **New button**: "Invita Utente" opens a modal form:
  - Email (required)
  - Role (dropdown: User, Moderator, Admin)
  - Custom message (optional)
- **Submit**: calls `POST /api/v1/admin/invitations` (API already exists)
- **New tab**: "Inviti" in the users page showing pending/accepted/expired invitations
  - Action "Rinvia" for expired invitations (`POST /api/v1/admin/invitations/resend`)

### Email

Contains link: `/accept-invite?token=<token>` (7-day expiry, route under `(public)` group)

### Backend: Already Implemented

- `SendInvitationCommand`, `BulkSendInvitationsCommand`, `ResendInvitationCommand`
- `AcceptInvitationCommand`, `ValidateInvitationTokenQuery`
- `InvitationToken` entity with 7-day expiry

---

## 2. Invitation Acceptance + Onboarding

### Password Setup — `/accept-invite?token=<token>` (EXISTS)

Page already exists at `apps/web/src/app/(public)/accept-invite/page.tsx`:

1. Page validates token via `POST /api/v1/auth/validate-invitation`
2. If valid, shows form: email (pre-filled, readonly) + password + confirm password + strength meter
3. Submit calls `POST /api/v1/auth/accept-invitation`
4. User auto-authenticated → **currently redirects to `/dashboard`**

**Work needed**: Change redirect from `/dashboard` to onboarding wizard route (e.g. `/onboarding?token=<token>`)

### Onboarding Wizard (EXISTS — needs integration)

The `OnboardingWizard` component already exists at `apps/web/src/components/onboarding/OnboardingWizard.tsx` with **5 steps** (not 3):

1. **Password** (required) — handled by `PasswordStep` component
2. **Profile** (skippable) — display name via `ProfileStep`
3. **Interests** (skippable) — interests selection via `InterestsStep`
4. **First Game** (skippable) — search catalog, add to collection via `FirstGameStep`
   - If skipped → step 5 is auto-skipped
5. **First Agent** (only if step 4 completed) — agent name + typology via `FirstAgentStep`

Each step after Password has a "Skip" button. Wizard can be skipped entirely after password.
After completion or skip → redirect to dashboard.

**Work needed**: Since the accept-invite page already handles password setup, the wizard integration should either:
- (A) Route to wizard starting at step 2 (skip password step, already done), OR
- (B) Create a dedicated `/onboarding` page that renders the wizard from step 2

The wizard component, all 5 step components, and the API client (`api.auth.completeOnboarding`) already exist.

---

## 3. Voice Interaction in Chat Card

### Architecture: Two-tier voice system

| Tier | Provider | Cost | Quality |
|------|----------|------|---------|
| **Free (default)** | Browser Web Speech API | $0 | Basic, Chrome/Edge only |
| **Premium (paid users)** | Deepgram (STT) + ElevenLabs (TTS) | Per-minute | High quality, multilingual |

### UI: Hybrid approach (Option C)

- **Mic button** 🎤 in chat input bar, next to send button
- **Recording state**: Inline bar appears above chat with:
  - Red mic icon + timer
  - Animated waveform
  - "Annulla" (Cancel) button
- **Sent messages**: Tagged with "🎤 Vocale" label
- **Agent responses**: Show "🔊 Ascolta" button for TTS playback
- **Toggle "🎤 HD"** in chat card header:
  - Free users: click shows upsell modal "Upgrade per voce HD"
  - Paid users: switches between Web Speech API and premium service
  - Off by default, even for paid users

### Technical approach

- **Frontend-only**: No new backend APIs needed
- STT transcription → text sent to existing `POST /api/v1/knowledge-base/ask`
- TTS reads response text client-side
- Web Speech API: `SpeechRecognition` (STT) + `SpeechSynthesis` (TTS)
- Premium: Deepgram WebSocket for STT, ElevenLabs REST for TTS

---

## 4. Admin Role Management + Audit Logs

### Role Change (backend exists)

- In `/admin/users`, click user → detail view
- Role dropdown: User, Moderator, Admin
- Submit calls `POST /api/v1/admin/users/{id}/roles`
- Generates `RoleChangedEvent` → auto-logged in audit trail

### Audit Logs (backend exists)

- Page `/admin/audit` (already exists in frontend)
- Add user filter: admin can search by user and see all related logs
- Endpoint `GET /api/v1/admin/audit` with `userId` query param
- Each entry shows: timestamp, action, user, before/after, IP, user agent

### Backend: No new development needed

Both features have existing APIs. Only frontend UI verification/completion needed.

---

## 5. E2E Test with Playwright

### Structure

- `test.describe.serial` in a single spec file
- State shared via `storageState` between steps

### Test Steps

#### Step 1: Admin sends invitation

- Login as admin
- Navigate to `/admin/users`
- Click "Invita Utente"
- Fill form (email, role)
- Submit → verify invitation in pending list

#### Step 2: User accepts invitation

- Intercept email or construct URL with token from API
- Navigate to `/accept-invite?token=...`
- Fill password + confirm
- Submit → verify redirect to onboarding
- Save user `storageState`

#### Step 3: Onboarding wizard

- Fill profile (display name)
- Search game in catalog → add to collection
- Create agent for the game
- Verify redirect to dashboard

#### Step 4: Voice interaction with agent

- Open agent chat card
- Verify 🎤 button present
- Mock Web Speech API via `page.addInitScript()`
- Simulate transcription → verify message sent with "🎤 Vocale" tag
- Verify response has "🔊 Ascolta" button

#### Step 5: Admin changes role + verifies audit

- Restore admin `storageState`
- Navigate to `/admin/users`
- Find invited user → change role
- Navigate to `/admin/audit`
- Verify audit log contains: role change entry + invitation accepted entry

### Technical Notes

- Web Speech API mocked via `page.addInitScript()` for voice simulation
- Invitation token retrieved via admin API or DB seed
- Each step depends on previous (serial execution)

---

## Open Issues Check

No duplicate issues found in the 26 open GitHub issues. Related but different:
- Publisher Portal (#6, 18 issues) — different scope (publisher roles)
- Voice Chat (#312, 5 issues, deferred) — broader scope, this spec covers a focused MVP
- AI Training (#237/#238, deferred) — not related

---

## What's Already Built vs What's New

| Component | Status | Work Needed |
|-----------|--------|-------------|
| Invitation API | ✅ Complete | None |
| Password change API | ✅ Complete | None |
| Collection API | ✅ Complete | None |
| Agent creation API | ✅ Complete | None |
| Chat/RAG API | ✅ Complete | None |
| Audit log API | ✅ Complete | None |
| Email queue | ✅ Complete | None |
| Role management API | ✅ Complete | None |
| **Admin invitation UI** | ✅ Complete | `InviteUserDialog`, `BulkInviteDialog`, invitations page exist. **Verify/complete** role dropdown + pending list |
| **Accept invite page** | ✅ Complete | `(public)/accept-invite/page.tsx` exists. **Modify**: redirect to wizard instead of `/dashboard` |
| **Onboarding wizard** | ✅ Complete (5 steps) | `OnboardingWizard.tsx` + all step components exist. **Integrate**: connect to accept-invite flow, skip password step |
| **Voice UI in chat card** | ✅ Complete | `VoiceMicButton`, `TtsSpeakerButton`, `VoiceSettingsPopover` in `ChatThreadView`. Also `/ask` voice-first page. **No new work needed** |
| **Premium voice toggle** | ⚠️ Architecture ready | Provider factory exists (`provider-factory.ts`), Zustand store persists prefs. **Deepgram/ElevenLabs providers not yet implemented** (deferred with #312) |
| **Admin UI completion** | ⚠️ Partial | **Verify/complete role + audit UI** |
| **E2E Playwright test** | ❌ Missing | **New test file** |
