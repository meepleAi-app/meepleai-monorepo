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
 * The mockup stage shows 6 PhoneShell frames side-by-side (Desktop only Phase
 * C-1; Mobile DEFERRED a Phase 4 viewport sweep). Each frame renders one
 * canonical auth screen. Axis discovery (from JSX twin auth-flow.jsx):
 *   screen: 'login' | 'register' | 'forgot' | 'reset' | 'verify' | '2fa'
 *   state:  'default' | 'loading' | 'error' | 'validation'
 *
 * CANONICAL COMPONENT PICK (multi-route consolidation, DS-17 Phase C-1 spec § 6):
 *   We render `LoginForm` (apps/web/src/components/auth/LoginForm.tsx) as the
 *   "hero" component for the stage matrix because:
 *     1. It is the smallest pure client component (no router / no Suspense).
 *     2. Storybook 10.4 client boundary does NOT tolerate `useSearchParams`
 *        that the route _content.tsx wrappers depend on.
 *     3. Frame matrix exposes the 6 screens via `argTypes.screen` for designer
 *        review even though only LoginForm renders. Other screens are
 *        documentation-only (refined Phase 2 human iteration step → split into
 *        dedicated stories if needed).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';
import { fn } from 'storybook/test';

import { LoginForm } from '@/components/auth/LoginForm';

import {
  MOCK_AUTH_FLOW_LOGIN_DEFAULT,
  MOCK_AUTH_FLOW_LOGIN_ERROR,
  mswForState,
} from '@/__tests__/fixtures/mockup-pilots/auth/auth-flow';

import type { Meta, StoryObj } from '@storybook/react';

type AuthScreen = 'login' | 'register' | 'forgot' | 'reset' | 'verify' | '2fa';
type AuthState = 'default' | 'loading' | 'error' | 'validation';

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
// Frames 02-06 represent the multi-route axis: refined Phase 2 iteration
// may split into dedicated stories per screen if designer requests.

export const Frame01_Login: Story = {
  name: '01 · Login — default',
  args: { loading: false, error: undefined },
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame02_Register: Story = {
  name: '02 · Registrazione — strength meter + OAuth',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Real RegisterForm has username + strength meter + terms checkbox. Phase 2 iteration may split into dedicated story (apps/web/src/components/auth/RegisterForm.stories.tsx already exists).',
      },
    },
  },
};

export const Frame03_ForgotPassword: Story = {
  name: '03 · Recupero password — email input + success state',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Real ResetPassword content (_content.tsx) drives /reset-password route con email submit → "email sent" success card.',
      },
    },
  },
};

export const Frame04_ResetPassword: Story = {
  name: '04 · Reset password — new pwd + confirm + token hidden',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForState('default') },
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
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. /verification-pending route uses VerificationPending component con resend cooldown + Cambia email CTA.',
      },
    },
  },
};

export const Frame06_TwoFactorSetup: Story = {
  name: '06 · Setup 2FA — QR + codice manuale + PIN 6 cifre',
  args: { loading: false, error: undefined },
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. TwoFactorSetup component (apps/web/src/components/auth/TwoFactorSetup.tsx) renders QR + manual code + 6-digit PIN inline confirm.',
      },
    },
  },
};

// ── State variant frames (axis = AuthState) ─────────────────────────────────

export const Loading: Story = {
  name: 'State · Loading',
  args: { loading: true, error: undefined },
  parameters: { msw: { handlers: mswForState('loading') } },
};

export const Error: Story = {
  name: 'State · Error (invalid credentials)',
  args: {
    loading: false,
    error: 'Email o password non validi. Riprova.',
  },
  parameters: { msw: { handlers: mswForState('error') } },
};
