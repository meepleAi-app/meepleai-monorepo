# Auth Flow v2 Migration — Design Spec

**Date**: 2026-04-21
**Owner**: redesign-v2 Fase 2.6
**Branch**: `refactor/redesign-v2-m6`
**Source mockup**: `admin-mockups/design_files/auth-flow.{html,jsx}`

## Goal

Migrate the pre-authentication surface (login, register, forgot/reset password, verify email) and the `AuthModal` to the M6 v2 design system. Introduce 5 new v2 primitives (`InputField`, `PwdInput`, `StrengthMeter`, `Divider`, `SuccessCard`) and refactor `LoginForm`/`RegisterForm` to use them.

## Context & decisions

Scope confirmed with user (2026-04-21):

1. **2FA setup screen: EXCLUDED.** The mockup includes a 2FA setup screen (QR + PIN), but backend endpoints for enrollment are unverified and the `PinInput` primitive would be needed only for that flow. Deferred to Fase 2.6.5 (pending backend audit). Existing `TwoFactorVerification` component (login challenge) is out of scope.
2. **AuthModal: REFACTOR (not deprecate).** The modal is kept for session-expired in-context re-login. Refactored internally to use v2 primitives; public API (`open`, `onClose`, `defaultMode`) preserved.
3. **Single PR, internal milestones.** One PR on #484 (continuation of redesign-v2-m6). Milestones: M1 primitives → M2 forms → M3 AuthModal → M4 pages.
4. **Primitives with dedicated tests.** Each new v2 primitive ships with its own `__tests__/*.test.tsx` file, matching the M6 pattern (AuthCard, OAuthButton, StepProgress).

## Architecture

### Routes affected

| Route | Action |
|-------|--------|
| `/login` | Rewrite `_content.tsx` to use `AuthCard` + inline `LoginForm` (no modal wrapper) |
| `/register` | Rewrite `_content.tsx`: public mode uses `AuthCard` + `RegisterForm`; invite-only keeps `RequestAccessForm` |
| `/verify-email` | Audit + align with v2 tokens (uses existing Alice-flow confirm-by-token endpoint) |
| `/verification-pending` | Rewrite `_content.tsx`: v2 layout (envelope emoji circle, resend with 30s countdown, "Cambia email" ghost button) |
| `/reset-password` | Rewrite `_content.tsx`: unify dual-mode (request vs reset) with v2 `InputField` + `PwdInput` + `SuccessCard` on success |

### New v2 primitives

Location: `apps/web/src/components/ui/v2/<primitive-name>/`

| Primitive | Purpose | Props |
|-----------|---------|-------|
| `InputField` | Labeled input with optional error, hint, right slot | `label`, `type`, `placeholder`, `value`, `onChange`, `error?`, `hint?`, `right?`, `autoComplete?`, `name?`, `id?` |
| `PwdInput` | Password input with show/hide toggle, optional strength meter | Same as `InputField` minus `type`/`right` + `showStrength?` |
| `StrengthMeter` | 4-segment bar colored by password score | `password: string` |
| `Divider` | "oppure" labeled visual separator | `label?: string` (default "oppure") |
| `SuccessCard` | Centered success state with emoji + CTA | `emoji`, `title`, `body` (string \| ReactNode), `cta`, `onCta` |

All primitives:
- Use HSL entity tokens (`hsl(var(--c-danger))`, `hsl(var(--c-toolkit))`, etc.)
- Export from package-local barrel `index.ts`
- Re-exported from `v2/index.ts` where applicable
- TypeScript strict, `readonly` props, explicit `JSX.Element` return
- Dedicated test file covering render, interaction, a11y (aria-describedby, aria-invalid, role)

### Refactored components

**`LoginForm`** (`src/components/auth/LoginForm.tsx`):
- Replace `AccessibleFormInput` with v2 `InputField` + `PwdInput`
- Replace `LoadingButton` with v2 `Btn loading={isSubmitting}`
- Remove inline OAuth handling (moved to parent which renders v2 `OAuthButton` + `Divider`)
- Keeps `onSuccess` + `onSwitchToRegister` callbacks (unchanged API)

**`RegisterForm`** (`src/components/auth/RegisterForm.tsx`):
- Same pattern as LoginForm
- Add `StrengthMeter` under password field (`showStrength` prop on `PwdInput`)
- Add terms checkbox with inline ToS/Privacy links (keeps `termsAcceptedAt` contract)

**`AuthModal`** (`src/components/auth/AuthModal.tsx`):
- Keep `AccessibleModal` as outer shell (retains focus-trap + ARIA)
- Replace shadcn `Tabs` with v2-styled tabs (simple button-based segmented control, no Radix)
- Use new `LoginForm`/`RegisterForm` internals
- Use v2 `OAuthButton` + `Divider` (delete legacy `OAuthButtons` component)

**Delete**: `apps/web/src/components/auth/OAuthButtons.tsx` (legacy, all consumers migrated to v2)

### Data / API

No backend changes. Existing endpoints preserved:
- `api.auth.login(email, password)` → session cookie
- `api.auth.register(email, password, termsAcceptedAt, honeypot)` → session + sendVerificationEmail side-effect
- `api.auth.resendVerificationEmail()` (verification-pending)
- `api.auth.requestPasswordReset(email)` (forgot-password)
- `api.auth.confirmPasswordReset(token, password)` (reset-password)
- `api.auth.verifyEmail(token)` (verify-email link target)

### Accessibility

- `InputField` / `PwdInput` use `aria-invalid={!!error}`, `aria-describedby` pointing to error/hint id
- `PwdInput` toggle is `<button type="button" aria-label="Mostra password" aria-pressed={show}>`
- `StrengthMeter` wrapper has `role="status" aria-live="polite"` with readable label ("Password: Buona")
- `Divider` uses `role="separator" aria-orientation="horizontal"`; label has `aria-hidden="true"`
- Form errors focused on submit (existing pattern preserved)
- `SuccessCard` emoji wrapper `aria-hidden="true"`; CTA keyboard-reachable
- All flows keyboard-complete; `AuthModal` preserves focus-trap via `AccessibleModal`
- `prefers-reduced-motion` disables `StrengthMeter` bar transitions

## Testing

### Unit (Vitest + RTL)

New primitive tests:
- `InputField.test.tsx`: renders label, fires onChange, shows error, shows hint when no error, right slot rendered
- `PwdInput.test.tsx`: toggles show/hide, strength meter shown when `showStrength` + value present, hidden otherwise
- `StrengthMeter.test.tsx`: returns null when empty; score progression (6/10 chars, special chars) updates bar count + label
- `Divider.test.tsx`: renders custom label, default "oppure"
- `SuccessCard.test.tsx`: renders emoji + title + body + CTA; CTA fires onCta

Refactored form tests:
- `LoginForm.test.tsx`: existing behavior preserved + new primitive assertions (eye toggle fires, input aria-invalid on error)
- `RegisterForm.test.tsx`: same + strength meter visible, terms checkbox required

AuthModal test:
- `AuthModal.test.tsx` (new): tab switching, default mode, onClose, TwoFactorVerification handoff (stub)

Page tests:
- `/verification-pending/_content.test.tsx`: updated for v2 layout + 30s countdown
- `/reset-password/_content.test.tsx`: updated for SuccessCard success state

### E2E (Playwright) — OUT OF SCOPE

Existing `/login`, `/register`, `/forgot-password`, `/reset-password` E2E specs remain green. No new E2E added; visual migration covered by screenshot diff if CI has visual regression (else manual QA).

## Error handling

| Scenario | Behavior |
|----------|----------|
| `api.auth.login` fails | Inline form error (existing pattern) |
| `api.auth.register` fails (409) | Inline "Email già registrata" error on email field |
| `requestPasswordReset` fails | Ghost error below CTA; form stays editable |
| `confirmPasswordReset` fails (invalid token) | SuccessCard replaced by error card with retry CTA → back to forgot-password |
| Network timeout | Generic error, retry button |

## Out of scope

- 2FA setup screen (Fase 2.6.5, pending backend audit)
- `PinInput` primitive (only needed for 2FA setup)
- E2E Playwright spec additions
- Visual regression / screenshot tests
- Email template updates
- OAuth provider changes (mockup removes GitHub from OAuth list — but current app has Google + Discord + GitHub; **keep all three**, add button for GitHub via v2 `OAuthButton`)
- Backend endpoint changes

## Success criteria

- All 5 new v2 primitives ship with dedicated tests, exported from `v2/index.ts`
- `LoginForm`, `RegisterForm`, `AuthModal` no longer import `AccessibleFormInput` or `LoadingButton`
- Legacy `src/components/auth/OAuthButtons.tsx` deleted; all consumers use v2 `OAuthButton`
- `/login`, `/register`, `/verification-pending`, `/reset-password` render with v2 `AuthCard` + `Btn`
- No regression on existing auth tests (933+ Vitest tests remain green)
- Bundle baseline updated (expected delta +15-30 KB for 5 new primitives)
- Lighthouse a11y score ≥ 95 on `/login` (unchanged from current baseline)

## Related

- Mockup: `admin-mockups/design_files/auth-flow.{html,jsx,css}`
- Previous phase: `docs/superpowers/specs/2026-04-20-onboarding-product-tour-design.md`
- M6 primitives completed: AuthCard (Task 1), OAuthButton (Task 2) — reused here
- Future: Fase 2.6.5 — 2FA setup screen + PinInput primitive (pending backend audit)
