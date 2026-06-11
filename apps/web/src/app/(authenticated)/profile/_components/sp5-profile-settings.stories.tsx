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
 *   /profile?tab=settings&section=security · modal:verify (D4 — Wizard step 2/3)
 *   /profile?tab=settings&section=security · modal:codes (D5 — Wizard step 3/3)
 *   /profile?tab=settings&section=security (D6 — 2FA ON)
 *
 * Stage axis (6 Desktop frames D1-D6; Mobile M1/M2 DEFERRED Phase 4):
 *   tab, section, wizardStep, twoFactorEnabled
 *
 * CANONICAL COMPONENT PICK: `ProfilePageContent`. Reads tab + section from
 * useSearchParams (Storybook nextjs framework provides parameters.nextjs.navigation).
 * Modal/wizard state internal to SecuritySection → driven by MSW handlers for
 * 2FA status + setup endpoints.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { mswForSp5State } from '@/__tests__/fixtures/mockup-pilots/auth/sp5-profile-settings';

import { ProfilePageContent } from './ProfilePageContent';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ProfilePageContent> = {
  title: 'Pages/Auth/SP5 Profile Settings',
  component: ProfilePageContent,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp5-profile-settings.jsx stage frames D1-D6 (Desktop only Phase C-1; M1/M2 Mobile DEFERRED Phase 4). Mockup covers tab=settings hub + section=security with 2FA wizard 3-step + 2FA ON state. Entity color = --c-kb (teal) per security domain.',
      },
    },
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

export const FrameD1_ProfileLandingSettings: Story = {
  name: 'D1 · Profile landing — tab Settings (Profile section default)',
  parameters: {
    msw: { handlers: mswForSp5State('default') },
    nextjs: {
      navigation: { pathname: '/profile', query: { tab: 'settings' } },
    },
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
    msw: { handlers: mswForSp5State('tfa-off') },
    nextjs: {
      navigation: { pathname: '/profile', query: { tab: 'settings', section: 'security' } },
    },
    docs: {
      description: {
        story:
          'URL: /profile?tab=settings&section=security. SecuritySection with 2FA toggle OFF + "Attiva 2FA" CTA + active sessions list.',
      },
    },
  },
};

export const FrameD3_Wizard2FaStep1Qr: Story = {
  name: 'D3 · Wizard 2FA — Step 1/3 (QR + manual code)',
  parameters: {
    msw: { handlers: mswForSp5State('wizard-setup') },
    nextjs: {
      navigation: { pathname: '/profile', query: { tab: 'settings', section: 'security' } },
    },
    docs: {
      description: {
        story:
          'TwoFactorSetup modal opens on "Attiva 2FA" click. Step 1: shows QR pattern + manual code MFSA-K7P2-W9NB-4XLQ. Documentation-only — modal state internal; Storybook play() can trigger via toggle click.',
      },
    },
  },
};

export const FrameD4_Wizard2FaStep2Verify: Story = {
  name: 'D4 · Wizard 2FA — Step 2/3 (PIN verify 6 digits)',
  parameters: {
    msw: { handlers: mswForSp5State('wizard-verify') },
    nextjs: {
      navigation: { pathname: '/profile', query: { tab: 'settings', section: 'security' } },
    },
    docs: {
      description: {
        story:
          'Step 2: 6-digit PIN input with auto-focus + Backspace nav. POST /api/v1/auth/2fa/verify-setup on confirm.',
      },
    },
  },
};

export const FrameD5_Wizard2FaStep3Codes: Story = {
  name: 'D5 · Wizard 2FA — Step 3/3 (Backup codes 10×)',
  parameters: {
    msw: { handlers: mswForSp5State('wizard-codes') },
    nextjs: {
      navigation: { pathname: '/profile', query: { tab: 'settings', section: 'security' } },
    },
    docs: {
      description: {
        story:
          'Step 3: 10 recovery codes shown (1-time view). Download/Copy CTAs + "Ho salvato i codici" confirm gate.',
      },
    },
  },
};

export const FrameD6_SecuritySection2FaOn: Story = {
  name: 'D6 · Section Security — 2FA ON',
  parameters: {
    msw: { handlers: mswForSp5State('tfa-on') },
    nextjs: {
      navigation: { pathname: '/profile', query: { tab: 'settings', section: 'security' } },
    },
    docs: {
      description: {
        story:
          '2FA toggle ON. Shows "Disattiva 2FA" CTA + "Rigenera codici" + enabled date + trusted devices list (1 entry).',
      },
    },
  },
};

export const TabOverview: Story = {
  name: 'Tab · Overview (achievements + activity + quick actions)',
  parameters: {
    msw: { handlers: mswForSp5State('default') },
    nextjs: { navigation: { pathname: '/profile', query: {} } },
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
    msw: { handlers: mswForSp5State('default') },
    nextjs: { navigation: { pathname: '/profile', query: { tab: 'achievements' } } },
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
    msw: { handlers: mswForSp5State('default') },
    nextjs: { navigation: { pathname: '/profile', query: { tab: 'activity' } } },
    docs: {
      description: {
        story: 'URL: /profile?tab=activity. Documentation only — not in mockup stage.',
      },
    },
  },
};
