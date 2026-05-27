import { User, Shield, FileCheck, Bell, Settings as Cog, Key, Link2 } from 'lucide-react';

import type { LucideIcon } from 'lucide-react';

export type SettingsSectionId =
  | 'profile'
  | 'security'
  | 'ai-consent'
  | 'notifications'
  | 'preferences'
  | 'api-keys'
  | 'services';

export interface SettingsSectionDef {
  id: SettingsSectionId;
  label: string;
  subtitle: string;
  /** Static entity utility prefix, e.g. `entity-kb` → `bg-entity-kb`. NEVER interpolate at runtime. */
  entity:
    | 'entity-player'
    | 'entity-kb'
    | 'entity-chat'
    | 'entity-tool'
    | 'entity-agent'
    | 'entity-toolkit';
  icon: LucideIcon;
  placeholder?: boolean;
}

export const SETTINGS_SECTIONS: SettingsSectionDef[] = [
  {
    id: 'profile',
    label: 'Profile',
    subtitle: 'Avatar, display name, lingua',
    entity: 'entity-player',
    icon: User,
  },
  {
    id: 'security',
    label: 'Security',
    subtitle: 'Manage 2FA, sessioni',
    entity: 'entity-kb',
    icon: Shield,
  },
  {
    id: 'ai-consent',
    label: 'AI & data consent',
    subtitle: 'GDPR, data retention',
    entity: 'entity-kb',
    icon: FileCheck,
  },
  {
    id: 'notifications',
    label: 'Notifications',
    subtitle: 'Email, push, digest',
    entity: 'entity-chat',
    icon: Bell,
    placeholder: true,
  },
  {
    id: 'preferences',
    label: 'Preferences',
    subtitle: 'Theme, lingua',
    entity: 'entity-tool',
    icon: Cog,
  },
  {
    id: 'api-keys',
    label: 'API keys',
    subtitle: 'Token per integrazioni',
    entity: 'entity-agent',
    icon: Key,
  },
  {
    id: 'services',
    label: 'Connected services',
    subtitle: 'BGG, Discord',
    entity: 'entity-toolkit',
    icon: Link2,
    placeholder: true,
  },
];

export const DEFAULT_SECTION: SettingsSectionId = 'profile';

const VALID = new Set<string>(SETTINGS_SECTIONS.map(s => s.id));
export function isValidSection(v: string | null | undefined): v is SettingsSectionId {
  return v != null && VALID.has(v);
}
