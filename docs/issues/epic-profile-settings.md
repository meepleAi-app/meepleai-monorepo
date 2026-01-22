# Epic: Profile & Settings - User Account Management

## Overview
User account settings with profile info, preferences, privacy controls, and account management.

## Design Mockup
`docs/design-proposals/meepleai-style/complete-mockups.html` (Tab 3)

## Key Features
- **Profile Tab**: Name, email, avatar upload, level/badges, stats
- **Preferenze Tab**: Notifications, theme, animations, library defaults (sort, view)
- **Privacy Tab**: Public profile, library visibility, stats sharing toggles
- **Account Tab**: Password change, 2FA setup, delete account (danger zone)

## Implementation Issues (6 total)

### Backend (3)
1. UpdateUserProfileCommand (name, email)
2. ChangePasswordCommand
3. Enable2FACommand / Disable2FACommand

### Frontend (2)
4. Settings page with 4 tabs
5. Avatar upload with preview + crop

### Testing (1)
6. Settings E2E (profile update, password change, 2FA flow)

## Timeline
1-2 weeks | Priority: MEDIUM

## Labels
`epic`, `profile`, `settings`, `frontend`, `backend`
