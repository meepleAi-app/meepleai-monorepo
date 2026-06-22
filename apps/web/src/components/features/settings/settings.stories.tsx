/**
 * @mockup admin-mockups/design_files/settings.html
 *
 * Settings argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Multi-route mockup covering 8 settings routes (/settings hub + 7 subroutes).
 * All routes resolve to /profile?tab=settings&section=<id> in the canonical
 * codebase (no standalone /settings/* routes).
 *
 * Stage axis (settings.jsx MENU + sections, Desktop only Phase C-1; Mobile
 * PhoneShell DEFERRED a Phase 4):
 *   section: 'profile' | 'security' | 'preferences' | 'notifications' |
 *            'api-keys' | 'ai-consent' | 'services'
 *   state: 'default' | 'loading' | 'error'
 *
 * Mockup section id `account` maps to canonical `security` (see axis-discovery.md).
 *
 * CANONICAL COMPONENT PICK: `SettingsTab` from features/settings. Controlled
 * component requiring activeSection + onChangeSection — wrapped via local
 * thin client wrapper to drive state.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { useState } from 'react';

import { mswForSettingsState } from '@/__tests__/fixtures/mockup-pilots/auth/settings';
import type { SettingsSectionId } from '@/components/features/settings/settings-sections';
import { SettingsTab } from '@/components/features/settings/SettingsTab';

import type { Meta, StoryObj } from '@storybook/react';

function SettingsTabWrapper({ initialSection }: { initialSection: SettingsSectionId }) {
  const [activeSection, setActiveSection] = useState<SettingsSectionId>(initialSection);
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
          'Pixel-faithful matrix di settings.jsx stage frames (Desktop hub + 7 section frames). Renders SettingsTab con SettingsSubNav + active section. Mobile PhoneShell frames DEFERRED Phase 4. Mockup section id `account` mapped to canonical `security`.',
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

export const Frame01_ProfileSection: Story = {
  name: '01 · Desktop · Profile section default',
  args: { initialSection: 'profile' },
  parameters: { msw: { handlers: mswForSettingsState('default') } },
};

export const Frame02_SecuritySection: Story = {
  name: '02 · Desktop · Security section (mockup "account") · 2FA + sessions',
  args: { initialSection: 'security' },
  parameters: { msw: { handlers: mswForSettingsState('default') } },
};

export const Frame03_PreferencesSection: Story = {
  name: '03 · Desktop · Preferences (theme + lingua + timezone)',
  args: { initialSection: 'preferences' },
  parameters: { msw: { handlers: mswForSettingsState('default') } },
};

export const Frame04_ApiKeysSection: Story = {
  name: '04 · Desktop · API Keys (3 active + create new)',
  args: { initialSection: 'api-keys' },
  parameters: { msw: { handlers: mswForSettingsState('default') } },
};

export const Frame05_AiConsentSection: Story = {
  name: '05 · Desktop · AI & Data Consent (GDPR + retention)',
  args: { initialSection: 'ai-consent' },
  parameters: { msw: { handlers: mswForSettingsState('default') } },
};

export const Frame06_NotificationsPlaceholder: Story = {
  name: '06 · Desktop · Notifications section (placeholder)',
  args: { initialSection: 'notifications' },
  parameters: {
    msw: { handlers: mswForSettingsState('default') },
    docs: {
      description: {
        story:
          'Section flagged placeholder: true in SETTINGS_SECTIONS. Real notification preferences live at /notifications/preferences (separate route).',
      },
    },
  },
};

export const Frame07_ServicesPlaceholder: Story = {
  name: '07 · Desktop · Connected services (placeholder)',
  args: { initialSection: 'services' },
  parameters: {
    msw: { handlers: mswForSettingsState('default') },
    docs: {
      description: {
        story:
          'Section flagged placeholder: true. Mockup shows Google/Discord/BGG connections — codebase deferred BGG per ADR #1903.',
      },
    },
  },
};

export const Loading: Story = {
  name: 'State · Loading (2FA status fetch in-flight)',
  args: { initialSection: 'security' },
  parameters: { msw: { handlers: mswForSettingsState('loading') } },
};

export const ErrorState: Story = {
  name: 'State · Error (2FA status fetch failed)',
  args: { initialSection: 'security' },
  parameters: { msw: { handlers: mswForSettingsState('error') } },
};
