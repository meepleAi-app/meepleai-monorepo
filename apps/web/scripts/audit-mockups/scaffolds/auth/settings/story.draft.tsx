/**
 * @mockup admin-mockups/design_files/settings.html
 *
 * Settings argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Multi-route mockup covering 8 settings routes:
 *   /settings (hub),
 *   /settings/ai-consent, /settings/api-keys, /settings/notifications,
 *   /settings/preferences, /settings/profile, /settings/security,
 *   /settings/services
 *
 * Stage axis del mockup:
 *   section: 'profile' | 'account' | 'preferences' | 'notifications' |
 *            'apikeys' | 'services' (mockup ids, mapped to canonical SettingsSectionId)
 *   view: 'desktop' (sidebar + content) | 'mobile' (phone hub list)
 *   state: 'default' | 'loading' | 'error'
 *
 * Desktop frame + 4 mobile PhoneShell frames (Mobile DEFERRED Phase 4).
 *
 * CANONICAL COMPONENT PICK (multi-route consolidation, DS-17 Phase C-1 spec § 6):
 *   We render `SettingsTab` (apps/web/src/components/features/settings/
 *   SettingsTab.tsx) which composes `SettingsSubNav` + active section
 *   component (`ProfileSection` | `SecuritySection` | etc.). It already
 *   handles section switching internally via `activeSection` prop.
 *
 *   Canonical SettingsSectionId mapping (settings-sections.ts:84):
 *     - DEFAULT_SECTION = 'profile' → Frame01
 *     - 'security' → Frame02
 *     - 'preferences' → Frame03
 *     - 'notifications' → SectionPlaceholder (settings-sections.ts placeholder flag)
 *     - 'api-keys' → Frame04
 *     - 'services' → SectionPlaceholder
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { useState } from 'react';

import { SettingsTab } from '@/components/features/settings/SettingsTab';
import type { SettingsSectionId } from '@/components/features/settings/settings-sections';

import { mswForState } from '@/__tests__/fixtures/mockup-pilots/auth/settings';

import type { Meta, StoryObj } from '@storybook/react';

type SettingsMockupSection =
  | 'profile'
  | 'account'
  | 'preferences'
  | 'notifications'
  | 'apikeys'
  | 'services';
type SettingsView = 'desktop' | 'mobile';
type SettingsState = 'default' | 'loading' | 'error';

// Mockup section id → canonical SettingsSectionId map
const MOCKUP_TO_CANONICAL: Record<SettingsMockupSection, SettingsSectionId> = {
  profile: 'profile',
  account: 'security', // mockup "account" maps to "security" canonical
  preferences: 'preferences',
  notifications: 'notifications',
  apikeys: 'api-keys',
  services: 'services',
};

// Thin wrapper to drive activeSection state for Storybook (SettingsTab is
// controlled — needs onChangeSection handler).
function SettingsTabWrapper({ initialSection }: { initialSection?: SettingsSectionId }) {
  const [activeSection, setActiveSection] = useState<SettingsSectionId>(
    initialSection ?? 'profile'
  );
  return <SettingsTab activeSection={activeSection} onChangeSection={setActiveSection} />;
}

const meta: Meta<typeof SettingsTabWrapper> = {
  title: 'Pages/Auth/Settings',
  component: SettingsTabWrapper,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di settings.jsx stage frames (Desktop hub + 4 mobile PhoneShell). Renders SettingsTab con SettingsSubNav + active section. Mobile PhoneShell frames (M1-M4) DEFERRED Phase 4. Mockup section ids mapped to canonical SettingsSectionId via MOCKUP_TO_CANONICAL.',
      },
    },
  },
  argTypes: {
    initialSection: {
      control: 'select',
      options: [
        'profile',
        'security',
        'preferences',
        'ai-consent',
        'api-keys',
        'notifications',
        'services',
      ],
      description: 'Initial active section. SettingsTab is controlled; wrapper holds local state.',
    },
  },
  args: { initialSection: 'profile' },
  decorators: [
    Story => (
      <div className="min-h-dvh bg-background p-8">
        <div className="container max-w-6xl mx-auto">
          <h1 className="text-3xl font-bold mb-6">Settings</h1>
          <Story />
        </div>
      </div>
    ),
  ],
};
export default meta;

type Story = StoryObj<typeof SettingsTabWrapper>;

// ── Stage frame canonicals (mapped 1:1 ai 6 mockup sections, Desktop only) ──
// Mobile PhoneShell frames M1-M4 DEFERRED Phase 4.

export const Frame01_ProfileSection: Story = {
  name: '01 · Desktop · Profile section default',
  args: { initialSection: 'profile' },
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame02_SecuritySection: Story = {
  name: '02 · Desktop · Security section (mockup "account") · 2FA + sessions',
  args: { initialSection: 'security' },
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame03_PreferencesSection: Story = {
  name: '03 · Desktop · Preferences (theme + lingua + timezone)',
  args: { initialSection: 'preferences' },
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame04_ApiKeysSection: Story = {
  name: '04 · Desktop · API Keys (3 active + create new)',
  args: { initialSection: 'api-keys' },
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame05_AiConsentSection: Story = {
  name: '05 · Desktop · AI & Data Consent (GDPR + retention)',
  args: { initialSection: 'ai-consent' },
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame06_NotificationsPlaceholder: Story = {
  name: '06 · Desktop · Notifications section (placeholder)',
  args: { initialSection: 'notifications' },
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Section flagged `placeholder: true` in SETTINGS_SECTIONS — renders SectionPlaceholder card. Real notification preferences live at /notifications/preferences (separate route).',
      },
    },
  },
};

export const Frame07_ServicesPlaceholder: Story = {
  name: '07 · Desktop · Connected services (placeholder)',
  args: { initialSection: 'services' },
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Section flagged `placeholder: true`. Mockup shows Google/Discord/BGG connections — codebase deferred BGG per ADR #1903.',
      },
    },
  },
};

// ── State variants ─────────────────────────────────────────────────────────

export const Loading: Story = {
  name: 'State · Loading (2FA status fetch in-flight)',
  args: { initialSection: 'security' },
  parameters: { msw: { handlers: mswForState('loading') } },
};

export const Error: Story = {
  name: 'State · Error (2FA status fetch failed)',
  args: { initialSection: 'security' },
  parameters: { msw: { handlers: mswForState('error') } },
};
