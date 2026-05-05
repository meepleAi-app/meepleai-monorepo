/**
 * Settings Hub (M6 follow-up — Fase 2.2)
 *
 * Landing page for settings categories. Uses SettingsList + SettingsRow v2 primitives.
 * Source of truth: admin-mockups/design_files/settings.jsx MobileHub + CATEGORIES.
 *
 * Categories marked "Prossimamente" will be enabled in Fase 2.3/2.4.
 */

'use client';

import { useRouter } from 'next/navigation';

import { SettingsList } from '@/components/ui/v2/settings-list';
import { SettingsRow } from '@/components/ui/v2/settings-row';

type Category = {
  readonly id: string;
  readonly icon: string;
  readonly label: string;
  readonly description: string;
  readonly href?: string;
  readonly disabled?: boolean;
};

// Categories mirror admin-mockups/design_files/settings.jsx CATEGORIES (line 823).
// Routes:
//   - /settings/security       — "account" in mockup (password, 2FA) — EXISTS
//   - /settings/notifications  — EXISTS
//   - /settings/ai-consent     — bonus (not in mockup)
//   - /settings/profile, /settings/preferences, /settings/api-keys, /settings/services
//     → stub for Fase 2.3/2.4
const CATEGORIES: readonly Category[] = [
  {
    id: 'profile',
    icon: '👤',
    label: 'Profilo',
    description: 'Avatar, nome visualizzato, email',
    href: '/settings/profile',
  },
  {
    id: 'security',
    icon: '🔐',
    label: 'Account',
    description: 'Password, 2FA, sicurezza',
    href: '/settings/security',
  },
  {
    id: 'preferences',
    icon: '🎨',
    label: 'Preferenze',
    description: 'Tema, lingua, privacy',
    href: '/settings/preferences',
  },
  {
    id: 'notifications',
    icon: '🔔',
    label: 'Notifiche',
    description: 'Gestisci le notifiche',
    href: '/settings/notifications',
  },
  {
    id: 'api-keys',
    icon: '🗝️',
    label: 'API Keys',
    description: 'Gestisci le tue chiavi API',
    href: '/settings/api-keys',
  },
  {
    id: 'services',
    icon: '🔗',
    label: 'Servizi connessi',
    description: 'Google, Discord, BGG',
    href: '/settings/services',
  },
  {
    id: 'ai-consent',
    icon: '🤖',
    label: 'Consenso AI',
    description: 'Come usiamo i tuoi dati con l\u2019AI',
    href: '/settings/ai-consent',
  },
];

const COMING_SOON_BADGE = (
  <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
    Prossimamente
  </span>
);

export default function SettingsHubPage() {
  const router = useRouter();

  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto max-w-2xl px-4 py-8">
        <header className="mb-8">
          <h1 className="text-3xl font-bold tracking-tight text-foreground">Impostazioni</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Personalizza il tuo account MeepleAI.
          </p>
        </header>

        <SettingsList ariaLabel="Categorie impostazioni">
          {CATEGORIES.map(cat => (
            <SettingsRow
              key={cat.id}
              icon={cat.icon}
              label={cat.label}
              description={cat.description}
              href={cat.disabled ? undefined : cat.href}
              disabled={cat.disabled}
              trailing={cat.disabled ? COMING_SOON_BADGE : undefined}
            />
          ))}
        </SettingsList>

        <div className="mt-8 border-t border-border pt-6">
          <button
            type="button"
            onClick={() => router.push('/')}
            className="text-sm text-muted-foreground transition-colors hover:text-foreground"
          >
            ← Torna alla Home
          </button>
        </div>
      </div>
    </div>
  );
}
