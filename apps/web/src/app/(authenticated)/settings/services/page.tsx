/**
 * Connected Services Settings Page (M6 follow-up — Fase 2.4.b)
 *
 * Stub per integrazioni con servizi esterni (Google, Discord, BoardGameGeek).
 * Source of truth: admin-mockups/design_files/settings.jsx ServicesPanel (line ~780).
 *
 * API constraints: backend non espone ancora endpoint OAuth link/unlink per servizi esterni.
 * Questa pagina mostra una UI coerente con il mockup ma con stato "Prossimamente".
 */

'use client';

import { useRouter } from 'next/navigation';

import { SettingsList } from '@/components/ui/v2/settings-list';
import { SettingsRow } from '@/components/ui/v2/settings-row';

type Service = {
  readonly id: string;
  readonly icon: string;
  readonly label: string;
  readonly description: string;
};

const SERVICES: readonly Service[] = [
  {
    id: 'google',
    icon: '🌐',
    label: 'Google',
    description: 'Sincronizza calendario partite e contatti',
  },
  {
    id: 'discord',
    icon: '💬',
    label: 'Discord',
    description: 'Invia inviti e aggiornamenti al tuo server',
  },
  {
    id: 'bgg',
    icon: '🎲',
    label: 'BoardGameGeek',
    description: 'Importa collezione e sincronizza rating',
  },
];

const COMING_SOON_BADGE = (
  <span className="rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
    Prossimamente
  </span>
);

export default function ConnectedServicesPage() {
  const router = useRouter();

  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto max-w-2xl px-4 py-8">
        <header className="mb-8">
          <h1 className="text-3xl font-bold tracking-tight text-foreground">Servizi connessi</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Collega account esterni per estendere le funzionalità di MeepleAI.
          </p>
        </header>

        <SettingsList ariaLabel="Servizi disponibili">
          {SERVICES.map(service => (
            <SettingsRow
              key={service.id}
              icon={service.icon}
              label={service.label}
              description={service.description}
              trailing={COMING_SOON_BADGE}
              disabled
            />
          ))}
        </SettingsList>

        <p className="mt-6 text-xs text-muted-foreground">
          Le integrazioni saranno abilitate nelle prossime release. Segui la{' '}
          <button
            type="button"
            onClick={() => router.push('/')}
            className="underline hover:text-foreground"
          >
            home
          </button>{' '}
          per aggiornamenti.
        </p>

        <div className="mt-8 border-t border-border pt-6">
          <button
            type="button"
            onClick={() => router.push('/settings')}
            className="text-sm text-muted-foreground transition-colors hover:text-foreground"
          >
            ← Impostazioni
          </button>
        </div>
      </div>
    </div>
  );
}
