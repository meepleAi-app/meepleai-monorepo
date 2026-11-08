'use client';

import type React from 'react';

import { useQuery } from '@tanstack/react-query';

import { NotificationPreferences } from '@/components/notifications/NotificationPreferences';
import { api } from '@/lib/api';

import { AiConsentSection } from './sections/AiConsentSection';
import { ApiKeysSection } from './sections/ApiKeysSection';
import { PreferencesSection } from './sections/PreferencesSection';
import { ProfileSection } from './sections/ProfileSection';
import { SectionPlaceholder } from './sections/SectionPlaceholder';
import { SecuritySection } from './sections/SecuritySection';
import { SETTINGS_SECTIONS, type SettingsSectionId } from './settings-sections';
import { SettingsSubNav } from './SettingsSubNav';

interface Props {
  readonly activeSection: SettingsSectionId;
  readonly onChangeSection: (id: SettingsSectionId) => void;
}

export function SettingsTab({ activeSection, onChangeSection }: Props): React.JSX.Element {
  const { data: status } = useQuery({
    queryKey: ['2fa-status'],
    queryFn: () => api.auth.getTwoFactorStatus(),
  });
  const def = SETTINGS_SECTIONS.find(s => s.id === activeSection)!;
  return (
    <div className="flex flex-col md:flex-row gap-6">
      <SettingsSubNav
        active={activeSection}
        onSelect={onChangeSection}
        twoFactorEnabled={status?.isEnabled ?? false}
      />
      <div className="flex-1 min-w-0">
        {def.placeholder ? (
          <SectionPlaceholder section={def} />
        ) : activeSection === 'security' ? (
          <SecuritySection />
        ) : activeSection === 'profile' ? (
          <ProfileSection />
        ) : activeSection === 'preferences' ? (
          <PreferencesSection />
        ) : activeSection === 'api-keys' ? (
          <ApiKeysSection />
        ) : activeSection === 'ai-consent' ? (
          <AiConsentSection />
        ) : activeSection === 'notifications' ? (
          // #3961: this section was a placeholder while the real preferences
          // lived at `/notifications/preferences`. The footer of every email
          // links to `/settings/notifications`, so that link landed on
          // "Settings UI in development". The component is reused as-is — it
          // takes no props and no routing hooks.
          <NotificationPreferences />
        ) : null}
      </div>
    </div>
  );
}
