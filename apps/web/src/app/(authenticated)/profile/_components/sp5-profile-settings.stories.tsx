/**
 * @mockup admin-mockups/design_files/sp5-profile-settings.html
 *
 * SP5 Settings hub + 2FA Wizard argTypes matrix story — DS-17 Phase C-1
 * (sub-issue #2160).
 *
 * The hub has an address of its own (#3938), so the frames the mockup described
 * as `/profile?tab=settings[&section=…]` now live on `/settings[/<section>]`:
 *   /settings                        (D1 — DEFAULT_SECTION `profile`)
 *   /settings/security               (D2 — 2FA OFF)
 *   /settings/security + open modal  (D3 — wizard step 1/3 QR)
 *   /settings/security + open modal  (D4 — wizard step 2/3 PIN)
 *   step 3/3 body                    (D5 — backup codes)
 *   /settings/security               (D6 — 2FA ON)
 *
 * Stage axis (6 Desktop frames D1-D6; Mobile M1/M2 DEFERRED Phase 4):
 *   section, wizardStep, twoFactorEnabled
 *
 * CANONICAL COMPONENT PICK: `SettingsPageContent` — the page component behind
 * `/settings` and `/settings/[section]`. It takes `activeSection` as a prop, so
 * there is no `?tab=`/`?section=` to read, and picking a section calls
 * `router.push('/settings/<id>')` — visible in the Actions panel, which is the
 * hub's navigation contract.
 *
 * Why no longer `ProfilePageContent`: #3938 removed the `settings` tab from
 * `/profile` (`VALID_TABS` = overview|achievements|activity), so the six frames
 * mounted on it rendered the profile Overview, and its courtesy effect fired
 * `router.replace('/settings…')` on every one of them. No gate noticed, and none
 * could: `lint:storybook-states` and `lint:fidelity` walk MOCKUPS_INDEX →
 * fidelity.json → story_path, and the source mockup was removed with the other
 * migrated page-mocks (DS-17-16, #2988) — no fidelity entry points here, so the
 * gates never reach this file, and they would score annotation coverage and
 * state naming anyway, not which component a story mounts. Broken by #3938 —
 * NOT by removing the `/settings*` redirects from `next.config.js`.
 *
 * Fixture caveat: `mswForSp5State` still targets `GET /api/v1/auth/2fa/status`
 * and `POST /api/v1/auth/2fa/verify-setup`, while the client calls
 * `GET /api/v1/users/me/2fa/status` and `POST /api/v1/auth/2fa/enable`, with
 * payloads that do not satisfy `TwoFactorStatusDtoSchema` /
 * `Enable2FAResultSchema`. Until the fixture is realigned the 2FA status query
 * fails and falls back to `isEnabled: false` — so D6 cannot show the ON state,
 * and the wizard cannot be walked into step 3 through the network. Hence D3/D4
 * mount the modal with `setupData` passed as a prop and D5 mounts the step-3
 * body: no frame is left depending on a handler the app never calls.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160, hub route #3938.
 */

import { useState } from 'react';

import { fn, userEvent, within } from 'storybook/test';

import {
  MOCK_AUTH_SP5_2FA_RECOVERY_CODES,
  MOCK_AUTH_SP5_2FA_SETUP_SECRET,
  mswForSp5State,
} from '@/__tests__/fixtures/mockup-pilots/auth/sp5-profile-settings';
import { SettingsPageContent } from '@/app/(authenticated)/settings/_components/SettingsPageContent';
import { BackupCodesView } from '@/components/features/settings/two-factor/BackupCodesView';
import { TwoFactorSetupModal } from '@/components/features/settings/two-factor/TwoFactorSetupModal';

import { ProfilePageContent } from './ProfilePageContent';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * `setupData` in the shape the setup endpoint returns (`TotpSetupResponse`). The
 * two halves are separate fixture constants because the mockup shows the secret
 * on D3 and the codes on D5.
 */
const WIZARD_SETUP_DATA = {
  secret: MOCK_AUTH_SP5_2FA_SETUP_SECRET.secret,
  qrCodeUrl: MOCK_AUTH_SP5_2FA_SETUP_SECRET.qrCodeUrl,
  backupCodes: MOCK_AUTH_SP5_2FA_RECOVERY_CODES.codes,
};

/** D5 body: `BackupCodesView` is controlled — the wizard owns the ack flag. */
function BackupCodesStep() {
  const [acked, setAcked] = useState(false);
  return (
    <div className="mx-auto max-w-md p-8">
      <BackupCodesView codes={WIZARD_SETUP_DATA.backupCodes} acked={acked} onAck={setAcked} />
    </div>
  );
}

const meta: Meta<typeof SettingsPageContent> = {
  title: 'Pages/Auth/SP5 Profile Settings',
  component: SettingsPageContent,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp5-profile-settings.jsx stage frames D1-D6 (Desktop only Phase C-1; M1/M2 Mobile DEFERRED Phase 4). Mockup covers the settings hub + section=security con 2FA wizard 3-step + 2FA ON state. Entity color = --c-kb (teal) per security domain. Indirizzi aggiornati a /settings/<section> (#3938).',
      },
    },
  },
  argTypes: {
    activeSection: {
      control: 'select',
      options: [
        'profile',
        'security',
        'ai-consent',
        'notifications',
        'preferences',
        'api-keys',
        'services',
      ],
      description:
        'Section the hub renders. `/settings` passes DEFAULT_SECTION, `/settings/[section]` passes the validated route segment.',
    },
  },
  args: { activeSection: 'profile' },
  decorators: [
    Story => (
      <div className="min-h-dvh bg-background">
        <Story />
      </div>
    ),
  ],
};
export default meta;

type Story = StoryObj<typeof SettingsPageContent>;

export const FrameD1_SettingsHubLanding: Story = {
  name: 'D1 · Hub landing — /settings (Profile section default)',
  args: { activeSection: 'profile' },
  parameters: {
    msw: { handlers: mswForSp5State('default') },
    nextjs: {
      navigation: { pathname: '/settings' },
    },
    docs: {
      description: {
        story:
          '/settings renders DEFAULT_SECTION (profile) in place. It must NOT redirect to /settings/profile: the a11y helper gotoChecked (#3917) asserts the landed pathname equals the requested one.',
      },
    },
  },
};

export const FrameD2_SecuritySection2FaOff: Story = {
  name: 'D2 · Section Security — 2FA OFF',
  args: { activeSection: 'security' },
  parameters: {
    msw: { handlers: mswForSp5State('tfa-off') },
    nextjs: {
      navigation: { pathname: '/settings/security' },
    },
    docs: {
      description: {
        story:
          'URL: /settings/security. TwoFactorStatusCard in the "Not enabled" state with the "Set up two-factor authentication" CTA, plus the Active sessions card. SettingsSubNav marks the active section with aria-current="page" on the row button — no role="tab" anywhere in the hub.',
      },
    },
  },
};

export const FrameD3_Wizard2FaStep1Qr: Story = {
  name: 'D3 · Wizard 2FA — Step 1/3 (QR + manual code)',
  args: { activeSection: 'security' },
  parameters: {
    msw: { handlers: mswForSp5State('wizard-setup') },
    nextjs: {
      navigation: { pathname: '/settings/security' },
    },
    docs: {
      description: {
        story:
          'Step 1: QR rendered client-side from the otpauth:// URI + the secret to enter by hand. The modal is mounted open with `setupData` as a prop — the very state SecuritySection holds after a successful setup call — instead of waiting on a setup handler the fixture does not provide.',
      },
    },
  },
  render: args => (
    <>
      <SettingsPageContent {...args} />
      <TwoFactorSetupModal open setupData={WIZARD_SETUP_DATA} onClose={fn()} />
    </>
  ),
};

export const FrameD4_Wizard2FaStep2Verify: Story = {
  name: 'D4 · Wizard 2FA — Step 2/3 (PIN verify 6 digits)',
  args: { activeSection: 'security' },
  parameters: {
    msw: { handlers: mswForSp5State('wizard-verify') },
    nextjs: {
      navigation: { pathname: '/settings/security' },
    },
    docs: {
      description: {
        story:
          'Step 2: 6-digit PIN input with auto-focus + Backspace nav. Reached through the same click a user makes — "Continue" on step 1 is local state, no request — so the frame walks the real wizard transition rather than describing it.',
      },
    },
  },
  render: args => (
    <>
      <SettingsPageContent {...args} />
      <TwoFactorSetupModal open setupData={WIZARD_SETUP_DATA} onClose={fn()} />
    </>
  ),
  play: async () => {
    // The dialog lives in a portal, outside canvasElement.
    const dialog = within(await within(document.body).findByRole('dialog'));
    await userEvent.click(dialog.getByRole('button', { name: 'Continue' }));
  },
};

export const FrameD5_Wizard2FaStep3Codes: Story = {
  name: 'D5 · Wizard 2FA — Step 3/3 (Backup codes 10×)',
  parameters: {
    nextjs: {
      navigation: { pathname: '/settings/security' },
    },
    docs: {
      description: {
        story:
          'Step 3: the 10 recovery codes (1-time view), "Copy all" / "Download .txt", and the "Ho salvato i recovery codes in un posto sicuro" checkbox that gates the wizard\'s Done button. Mounts the step body alone — so the Done button, which lives in TwoFactorWizardBody, is out of frame: the wizard enters this step only after POST /api/v1/auth/2fa/enable returns an Enable2FAResult, and the fixture answers a different endpoint with the setup payload, so walking the frame through the network would render the verify error instead of the codes.',
      },
    },
  },
  render: () => <BackupCodesStep />,
};

export const FrameD6_SecuritySection2FaOn: Story = {
  name: 'D6 · Section Security — 2FA ON',
  args: { activeSection: 'security' },
  parameters: {
    msw: { handlers: mswForSp5State('tfa-on') },
    nextjs: {
      navigation: { pathname: '/settings/security' },
    },
    docs: {
      description: {
        story:
          'Intended stage: 2FA ON — "✓ Enabled · <date>" pill with "Regenerate recovery codes" (disabled, coming soon) + "Disable 2FA". KNOWN GAP, the frame still renders the OFF state: mswForSp5State("tfa-on") answers GET /api/v1/auth/2fa/status while the client reads GET /api/v1/users/me/2fa/status, and the mock payload carries recoveryCodesCount/trustedDevices where TwoFactorStatusDtoSchema expects unusedBackupCodesCount. The fix belongs in the fixture, not here. (The mockup also showed a trusted-devices list: no component renders one — ActiveSessionsCard shows sessions.)',
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
          'Default tab on /profile (no ?tab=…). Documentation only — not in mockup stage frames. Mounts ProfilePageContent: since #3938 /profile keeps the public identity only, the settings hub moved to /settings.',
      },
    },
  },
  render: () => <ProfilePageContent />,
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
  render: () => <ProfilePageContent />,
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
  render: () => <ProfilePageContent />,
};
