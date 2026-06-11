/**
 * @mockup admin-mockups/design_files/auth-flow.html
 *
 * Auth Flow argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Multi-route mockup covering 8 (auth) routes:
 *   /login, /register, /reset-password, /oauth-callback,
 *   /verify-email, /verification-pending, /verification-success,
 *   /invitation-expired
 *
 * Stage axis (auth-flow.jsx config 778-785 — 6 PhoneShell side-by-side
 * Desktop frames; Mobile DEFERRED a Phase 4 viewport sweep):
 *   screen: 'login' | 'register' | 'forgot' | 'reset' | 'verify' | '2fa'
 *   state:  'default' | 'loading' | 'error' | 'validation'
 *
 * CANONICAL COMPONENT PICK (DS-17 Phase C-1 spec § 6 multi-route consolidation):
 *   Renders `LoginForm` as hero for stage matrix. Other 5 screens are
 *   documentation-only (real components — RegisterForm, ResetPasswordForm,
 *   VerificationPending, TwoFactorSetup — have dedicated stories under
 *   apps/web/src/components/auth/<Component>.stories.tsx).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { fn } from 'storybook/test';

import { mswForAuthFlowState } from '@/__tests__/fixtures/mockup-pilots/auth/auth-flow';
import { LoginForm } from '@/components/auth/LoginForm';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LoginForm> = {
  title: 'Pages/Auth/Auth Flow',
  component: LoginForm,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di auth-flow.jsx stage frames 01-06 (Desktop only Phase C-1; Mobile deferred Phase 4). 6 PhoneShell frames mappati 1:1 ai 6 canonical auth screen (Login, Register, ForgotPassword, ResetPassword, VerifyEmail, 2FA setup). Render hero usa LoginForm; altre screen documentation-only via argTypes per designer review.',
      },
    },
  },
  argTypes: {
    loading: {
      control: 'boolean',
      description: 'Loading state for submit button.',
    },
    error: {
      control: 'text',
      description: 'Error message to display in error banner.',
    },
  },
  args: {
    onSubmit: fn(),
    onErrorDismiss: fn(),
    loading: false,
    error: undefined,
  },
  decorators: [
    Story => (
      <div className="min-h-dvh flex items-center justify-center bg-muted p-6">
        <div className="w-full max-w-md p-6 bg-card rounded-2xl shadow-lg">
          <Story />
        </div>
      </div>
    ),
  ],
};
export default meta;

type Story = StoryObj<typeof LoginForm>;

// ── Stage frame canonicals (mapped 1:1 ai 6 PhoneShell frames Desktop) ────
// Mobile frames DEFERRED to Phase 4 (viewport sweep, Mobile opt-in).

export const Frame01_Login: Story = {
  name: '01 · Login — default',
  args: { loading: false, error: undefined },
  parameters: { msw: { handlers: mswForAuthFlowState('default') } },
};

export const Frame02_Register: Story = {
  name: '02 · Registrazione — strength meter + OAuth',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForAuthFlowState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Real `RegisterForm` (apps/web/src/components/auth/RegisterForm.tsx) renders username + strength meter + terms checkbox; see RegisterForm.stories.tsx for dedicated coverage.',
      },
    },
  },
};

export const Frame03_ForgotPassword: Story = {
  name: '03 · Recupero password — email input + success state',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForAuthFlowState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Real `ResetPasswordForm` drives /reset-password route con email submit → "email sent" success card.',
      },
    },
  },
};

export const Frame04_ResetPassword: Story = {
  name: '04 · Reset password — new pwd + confirm + token hidden',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForAuthFlowState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Token reset (`/reset-password?token=...`) shows new pwd + confirm with strength meter, success → CTA login.',
      },
    },
  },
};

export const Frame05_VerifyEmail: Story = {
  name: '05 · Verifica email — countdown 30s + cambia email',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForAuthFlowState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. /verification-pending route uses `VerificationPending` con resend cooldown + Cambia email CTA.',
      },
    },
  },
};

export const Frame06_TwoFactorSetup: Story = {
  name: '06 · Setup 2FA — QR + codice manuale + PIN 6 cifre',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForAuthFlowState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. `TwoFactorSetup` (apps/web/src/components/auth/TwoFactorSetup.tsx) renders QR + manual code + 6-digit PIN inline confirm.',
      },
    },
  },
};

// ── State variant frames (axis = AuthState) ─────────────────────────────────

export const Loading: Story = {
  name: 'State · Loading',
  args: { loading: true, error: undefined },
  parameters: { msw: { handlers: mswForAuthFlowState('loading') } },
};

export const ErrorState: Story = {
  name: 'State · Error (invalid credentials)',
  args: {
    loading: false,
    error: 'Email o password non validi. Riprova.',
  },
  parameters: { msw: { handlers: mswForAuthFlowState('error') } },
};
