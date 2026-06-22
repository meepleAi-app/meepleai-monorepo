'use client';

import type React from 'react';

import { SettingsList } from '@/components/ui/settings-list';
import { SettingsRow } from '@/components/ui/settings-row';

import { SETTINGS_SECTIONS, type SettingsSectionId } from './settings-sections';

interface Props {
  readonly active: SettingsSectionId;
  readonly onSelect: (id: SettingsSectionId) => void;
  readonly twoFactorEnabled: boolean;
}

export function SettingsSubNav({ active, onSelect, twoFactorEnabled }: Props): React.JSX.Element {
  return (
    <div className="md:w-60 md:shrink-0">
      <SettingsList ariaLabel="Settings sections">
        {SETTINGS_SECTIONS.map(sec => {
          const Icon = sec.icon;
          const showBadge = sec.id === 'security' && !twoFactorEnabled;
          return (
            <SettingsRow
              key={sec.id}
              icon={<Icon className="h-5 w-5" aria-hidden />}
              label={sec.label}
              description={sec.subtitle}
              onClick={() => onSelect(sec.id)}
              aria-current={active === sec.id ? 'page' : undefined}
              trailing={
                showBadge ? (
                  <span
                    data-testid="subnav-2fa-badge"
                    className="font-mono text-[10px] font-bold px-1.5 py-0.5 rounded-sm bg-warning/15 text-warning"
                  >
                    ⚠ 2FA off
                  </span>
                ) : undefined
              }
            />
          );
        })}
      </SettingsList>
    </div>
  );
}
