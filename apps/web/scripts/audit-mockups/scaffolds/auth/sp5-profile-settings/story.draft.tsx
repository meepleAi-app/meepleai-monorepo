/**
 * @mockup admin-mockups/design_files/sp5-profile-settings.html
 *
 * SP5 Profile Settings + 2FA Wizard argTypes matrix story — DS-17 Phase C-1
 * (sub-issue #2160).
 *
 * Multi-route mockup covering 1 route + 6 query-param section variants:
 *   /profile?tab=settings (default → section=profile)
 *   /profile?tab=settings&section=security (D2 — 2FA OFF)
 *   /profile?tab=settings&section=security · modal:setup (D3 — Wizard step 1/3 QR)
 *   /profile?tab=settings&section=security · modal:verify (D4 — Wizard step 2/3 Verify)
 *   /profile?tab=settings&section=security · modal:codes (D5 — Wizard step 3/3 Backup codes)
 *   /profile?tab=settings&section=security (D6 — 2FA ON)
 *
 * Stage axis del mockup (6 Desktop frames + 2 Mobile PhoneShell frames):
 *   tab: 'overview' | 'achievements' | 'activity' | 'settings'
 *   section: SettingsSectionId
 *   wizardStep: null | 'setup' | 'verify' | 'codes'
 *   twoFactorEnabled: boolean
 *   view: 'desktop' | 'mobile'
 *
 * CANONICAL COMPONENT PICK (multi-route consolidation, DS-17 Phase C-1 spec § 6):
 *   We render `ProfilePageContent` (apps/web/src/app/(authenticated)/profile/
 *   _components/ProfilePageContent.tsx) — the full Profile tab orchestrator
 *   that owns tab state + settings section state via URL params (useSearchParams).
 *
 *   The 2FA wizard (D3/D4/D5) is rendered as a Drawer/Modal inside
 *   SecuritySection via `TwoFactorSetup` component. State managed by
 *   `SecuritySection` internal hooks.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { ProfilePageContent } from '@/app/(authenticated)/profile/_components/ProfilePageContent';

import { mswForState } from '@/__tests__/fixtures/mockup-pilots/auth/sp5-profile-settings';

import type { Meta, StoryObj } from '@storybook/react';

type ProfileTab = 'overview' | 'achievements' | 'activity' | 'settings';
type WizardStep = 'setup' | 'verify' | 'codes' | null;
type Sp5State =
  | 'default'
  | 'tfa-off'
  | 'tfa-on'
  | 'wizard-setup'
  | 'wizard-verify'
  | 'wizard-codes';

const meta: Meta<typeof ProfilePageContent> = {
  title: 'Pages/Auth/SP5 Profile Settings',
  component: ProfilePageContent,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp5-profile-settings.jsx stage frames D1-D6 (Desktop only Phase C-1; M1/M2 Mobile DEFERRED Phase 4). Mockup covers tab=settings hub + section=security with 2FA wizard 3-step (setup → verify → codes) + 2FA ON state. Entity color = --c-kb (teal) per security domain.',
      },
    },
  },
  argTypes: {
    // ProfilePageContent reads tab + section from useSearchParams() — not
    // controllable via props. argTypes are documentation only for designer
    // review. State variants drive MSW handler scenario instead.
  },
  args: {},
  decorators: [
    Story => (
      <div className="min-h-dvh bg-background">
        <Story />
      </div>
    ),
  ],
};
export default meta;

type Story = StoryObj<typeof ProfilePageContent>;

// ── Stage frame canonicals (mapped 1:1 ai 6 Desktop frames mockup) ────────
// Mobile frames M1+M2 DEFERRED Phase 4.
// Mockup frame labels mirror exactly the mockup JSX `<Frame label="...">`.

export const FrameD1_ProfileLandingSettings: Story = {
  name: 'D1 · Profile landing — tab Settings (Profile section default)',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Default state: lands on /profile?tab=settings with section=profile (DEFAULT_SECTION). URL handled via useSearchParams + isValidSection guard.',
      },
    },
  },
};

export const FrameD2_SecuritySection2FaOff: Story = {
  name: 'D2 · Section Security — 2FA OFF',
  parameters: {
    msw: { handlers: mswForState('tfa-off') },
    docs: {
      description: {
        story:
          'URL: /profile?tab=settings&section=security. Shows SecuritySection with 2FA toggle in OFF state + "Attiva 2FA" CTA + active sessions list.',
      },
    },
  },
};

export const FrameD3_Wizard2FaStep1Qr: Story = {
  name: 'D3 · Wizard 2FA — Step 1/3 (QR + manual code)',
  parameters: {
    msw: { handlers: mswForState('wizard-setup') },
    docs: {
      description: {
        story:
          'TwoFactorSetup modal/drawer opens on "Attiva 2FA" click. Step 1: shows QR pattern + manual code MFSA-K7P2-W9NB-4XLQ + "Scansiona con app authenticator" hint.',
      },
    },
  },
};

export const FrameD4_Wizard2FaStep2Verify: Story = {
  name: 'D4 · Wizard 2FA — Step 2/3 (PIN verify 6 digits)',
  parameters: {
    msw: { handlers: mswForState('wizard-verify') },
    docs: {
      description: {
        story:
          'Step 2: 6-digit PIN input with auto-focus + Backspace nav + numeric inputMode. POST /api/v1/auth/2fa/verify-setup on confirm.',
      },
    },
  },
};

export const FrameD5_Wizard2FaStep3Codes: Story = {
  name: 'D5 · Wizard 2FA — Step 3/3 (Backup codes 10×)',
  parameters: {
    msw: { handlers: mswForState('wizard-codes') },
    docs: {
      description: {
        story:
          'Step 3: 10 recovery codes shown (1-time view). Download/Copy CTAs + "Ho salvato i codici" confirm gates close. TwoFactorRecoveryCodes component owns this.',
      },
    },
  },
};

export const FrameD6_SecuritySection2FaOn: Story = {
  name: 'D6 · Section Security — 2FA ON',
  parameters: {
    msw: { handlers: mswForState('tfa-on') },
    docs: {
      description: {
        story:
          'Post-wizard: 2FA toggle ON, shows "Disattiva 2FA" CTA + "Rigenera codici" + enabled date + trusted devices list (1 entry).',
      },
    },
  },
};

// ── Profile tab variants (top-level TabBar axis) ───────────────────────────

export const TabOverview: Story = {
  name: 'Tab · Overview (achievements + activity + quick actions)',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Default tab on /profile (no ?tab=…). Documentation only — not in mockup stage frames.',
      },
    },
  },
};

export const TabAchievements: Story = {
  name: 'Tab · Achievements (12 badges grid)',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story: 'URL: /profile?tab=achievements. Documentation only — not in mockup stage.',
      },
    },
  },
};

export const TabActivity: Story = {
  name: 'Tab · Activity (recent sessions feed)',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story: 'URL: /profile?tab=activity. Documentation only — not in mockup stage.',
      },
    },
  },
};
