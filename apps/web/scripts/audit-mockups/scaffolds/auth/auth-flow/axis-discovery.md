# auth-flow — Axis Discovery

**Source HTML**: `admin-mockups/design_files/auth-flow.html`
**JSX twin**: `admin-mockups/design_files/auth-flow.jsx`
**Phase B classification**: `design_intent: current` · no `pair_disagreement`
**Mockup canonical**: HTML (per MOCKUPS_INDEX pairing rule)

## Mockup stage layout

The mockup renders 6 `PhoneShell` frames side-by-side in a `.phones-grid`
(`auth-flow.jsx:789-794`, `AuthRoot` component). Each frame is wrapped by
`AuthPhone` which holds local state for `screen` (initialScreen → user can
navigate via inner links e.g. `onNav('register')`).

The mockup is a **multi-route stage**: 1 mockup HTML covers 8 distinct app
routes. Per DS-17 Phase C-1 spec § 6, we render ONE canonical component
(`LoginForm`) and document the rest via `argTypes.screen` and Frame stories
for designer review.

## Axis (canonical)

| Axis | Type | Values | Source | Notes |
|------|------|--------|--------|-------|
| `screen` | enum | `login` \| `register` \| `forgot` \| `reset` \| `verify` \| `2fa` | `SCREENS` registry `auth-flow.jsx:757-764` | One frame per `config` entry (lines 778-785) |
| `state` | enum | `default` \| `loading` \| `error` \| `validation` | LoginForm `loading`/`error` props (LoginForm.tsx:38-44) | Drives MSW handler scenario |

The mockup itself does not expose `stateOverride` — each PhoneShell renders
the unauthenticated default. State variants (loading/error) come from the
real Client component props (`LoginForm.tsx` already has `loading: boolean`
+ `error: string` + `onErrorDismiss` props — see existing `LoginForm.stories.tsx`).

## Frame matrix (Desktop only Phase C-1)

| Frame | mockup JSX label (lines) | Canonical screen | Axis values |
|-------|--------------------------|------------------|-------------|
| 01 | `01 · Login` (jsx:779) | `LoginScreen` (jsx:370-409) | `screen='login', state='default'` |
| 02 | `02 · Registrazione` (jsx:780) | `RegisterScreen` (jsx:414-471) | `screen='register', state='default'` |
| 03 | `03 · Recupero password` (jsx:781) | `ForgotPasswordScreen` (jsx:476-514) | `screen='forgot', state='default'` |
| 04 | `04 · Reset password` (jsx:782) | `ResetPasswordScreen` (jsx:519-566) | `screen='reset', state='default'` |
| 05 | `05 · Verifica email` (jsx:783) | `VerifyEmailScreen` (jsx:571-624) | `screen='verify', state='default'` |
| 06 | `06 · Setup 2FA` (jsx:784) | `TwoFactorSetupScreen` (jsx:629-740) | `screen='2fa', state='default'` |

## Component mapping (route ↔ canonical)

| Route | Real Client component | File |
|-------|-----------------------|------|
| `/login` | `LoginPageContent` (uses `LoginForm`) | `apps/web/src/app/(auth)/login/_content.tsx` |
| `/register` | `RegisterPageContent` (uses `RegisterForm`) | `apps/web/src/app/(auth)/register/_content.tsx` |
| `/reset-password` | `ResetPasswordPageContent` | `apps/web/src/app/(auth)/reset-password/_content.tsx` |
| `/oauth-callback` | `OAuthCallbackPage` | `apps/web/src/app/(auth)/oauth-callback/page.tsx` |
| `/verify-email` | `VerifyEmailPageContent` | `apps/web/src/app/(auth)/verify-email/_content.tsx` |
| `/verification-pending` | `VerificationPendingPageContent` (uses `VerificationPending`) | `apps/web/src/app/(auth)/verification-pending/_content.tsx` |
| `/verification-success` | `VerificationSuccessPage` (uses `VerificationSuccess`) | `apps/web/src/app/(auth)/verification-success/page.tsx` |
| `/invitation-expired` | `InvitationExpiredPage` | `apps/web/src/app/(auth)/invitation-expired/page.tsx` |

## Canonical component pick

**Picked**: `apps/web/src/components/auth/LoginForm.tsx`

**Why**:
1. Smallest pure client component (no `useSearchParams`, no `useRouter`, no
   `useAuth` hook side-effects) → renders cleanly in Storybook 10.4 client
   boundary.
2. Already has `loading` + `error` + `onErrorDismiss` props mapping 1:1 to
   the AuthState axis.
3. Existing `LoginForm.stories.tsx` (Components/Auth/LoginForm) covers state
   variants; the new Pages/Auth/Auth Flow story owns the **mockup matrix**
   semantics (frame-per-screen documentation).

## Mockup ↔ codebase divergences

| # | Divergence | Resolution |
|---|------------|------------|
| 1 | Mockup uses Discord OAuth provider; codebase wires `google`/`discord`/`github` (LoginPageContent.tsx:140-142). | Codebase superset — no action needed. Note in story header. |
| 2 | Mockup `TwoFactorSetupScreen` shows inline QR + PIN; codebase has dedicated `/profile?tab=settings&section=security` modal wizard. | Different route. `2fa` frame here is documentation-only; live wizard lives in sp5-profile-settings cluster. |
| 3 | Mockup `VerifyEmailScreen` has inline "Cambia email" button; codebase splits this across `/verification-pending` (resend) + `/verify-email` (token consume). | Frame 05 documents the consolidated demo; real routes split intentionally. |
| 4 | Mockup uses inline state nav (`onNav('forgot')`); codebase uses Next.js `<Link>` between routes. | Storybook stage does not need router; LoginForm renders without nav. |

## JSX evidence (line refs)

- `SCREENS` registry: `auth-flow.jsx:757-764`
- `AuthPhone` wrapper with internal `useState(screen)`: `auth-flow.jsx:767-775`
- Stage layout `phones-grid` map: `auth-flow.jsx:789-794`
- 6-entry `config` array (frame labels): `auth-flow.jsx:778-785`
- `LoginForm` props `loading` + `error`: `LoginForm.tsx:38-44`
