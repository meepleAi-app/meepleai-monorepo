/**
 * Preferences Settings Page (M6 follow-up — Fase 2.3.b)
 *
 * Tema, lingua, notifiche, data retention e privacy.
 * Source of truth: admin-mockups/design_files/settings.jsx PreferencesPanel (line 416).
 *
 * API constraints: UpdatePreferencesRequest non supporta timezone/density del mockup.
 * Lingue supportate: it/en (estendibile). Retention: 30/90/180/365 giorni.
 */

'use client';

import { useEffect, useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { SettingsList } from '@/components/ui/v2/settings-list';
import { SettingsRow } from '@/components/ui/v2/settings-row';
import { ToggleSwitch } from '@/components/ui/v2/toggle-switch';
import { userKeys } from '@/hooks/queries/useCurrentUser';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';

type Theme = 'light' | 'dark' | 'system';

interface PrefsFormState {
  theme: Theme;
  language: string;
  emailNotifications: boolean;
  dataRetentionDays: number;
  showProfile: boolean;
  showActivity: boolean;
  showLibrary: boolean;
}

const THEME_OPTIONS: readonly { value: Theme; label: string; hint: string }[] = [
  { value: 'system', label: 'Sistema', hint: 'Segui il tema del dispositivo' },
  { value: 'light', label: 'Chiaro', hint: 'Sempre chiaro' },
  { value: 'dark', label: 'Scuro', hint: 'Sempre scuro' },
];

const LANGUAGE_OPTIONS: readonly { value: string; label: string }[] = [
  { value: 'it', label: 'Italiano' },
  { value: 'en', label: 'English' },
];

const RETENTION_OPTIONS: readonly { value: number; label: string }[] = [
  { value: 30, label: '30 giorni' },
  { value: 90, label: '90 giorni' },
  { value: 180, label: '6 mesi' },
  { value: 365, label: '1 anno' },
];

export default function PreferencesSettingsPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: prefs, isLoading } = useQuery({
    queryKey: [...userKeys.current(), 'preferences'],
    queryFn: () => api.auth.getPreferences(),
    staleTime: 5 * 60 * 1000,
  });

  const [form, setForm] = useState<PrefsFormState | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (prefs) {
      setForm({
        theme: prefs.theme,
        language: prefs.language,
        emailNotifications: prefs.emailNotifications,
        dataRetentionDays: prefs.dataRetentionDays,
        showProfile: prefs.showProfile ?? true,
        showActivity: prefs.showActivity ?? true,
        showLibrary: prefs.showLibrary ?? true,
      });
    }
  }, [prefs]);

  async function handleSave() {
    if (!form) return;
    setIsSaving(true);
    try {
      await api.auth.updatePreferences({
        theme: form.theme,
        language: form.language,
        emailNotifications: form.emailNotifications,
        dataRetentionDays: form.dataRetentionDays,
        showProfile: form.showProfile,
        showActivity: form.showActivity,
        showLibrary: form.showLibrary,
      });
      await queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast({ title: 'Preferenze salvate', description: 'Le tue impostazioni sono aggiornate.' });
    } catch (err) {
      toast({
        title: 'Errore',
        description: err instanceof Error ? err.message : 'Impossibile salvare.',
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  }

  const update = <K extends keyof PrefsFormState>(key: K, value: PrefsFormState[K]) => {
    setForm(prev => (prev ? { ...prev, [key]: value } : prev));
  };

  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto max-w-2xl px-4 py-8">
        <header className="mb-8">
          <h1 className="text-3xl font-bold tracking-tight text-foreground">Preferenze</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Personalizza aspetto, lingua e privacy.
          </p>
        </header>

        {isLoading || !form ? (
          <div className="rounded-xl border border-border bg-card p-8 text-center text-sm text-muted-foreground">
            Caricamento preferenze...
          </div>
        ) : (
          <div className="space-y-8">
            {/* Appearance */}
            <section className="space-y-4">
              <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                Aspetto
              </h2>
              <div className="space-y-3 rounded-xl border border-border bg-card p-5">
                <Label>Tema</Label>
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                  {THEME_OPTIONS.map(opt => {
                    const active = form.theme === opt.value;
                    return (
                      <button
                        key={opt.value}
                        type="button"
                        onClick={() => update('theme', opt.value)}
                        className={`rounded-lg border p-3 text-left transition-colors ${
                          active
                            ? 'border-primary bg-primary/10 text-foreground'
                            : 'border-border bg-card hover:bg-muted/40'
                        }`}
                        aria-pressed={active}
                      >
                        <div className="text-sm font-medium">{opt.label}</div>
                        <div className="mt-0.5 text-xs text-muted-foreground">{opt.hint}</div>
                      </button>
                    );
                  })}
                </div>
              </div>

              <div className="space-y-3 rounded-xl border border-border bg-card p-5">
                <Label htmlFor="language">Lingua</Label>
                <select
                  id="language"
                  value={form.language}
                  onChange={e => update('language', e.target.value)}
                  disabled={isSaving}
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/40"
                >
                  {LANGUAGE_OPTIONS.map(lang => (
                    <option key={lang.value} value={lang.value}>
                      {lang.label}
                    </option>
                  ))}
                </select>
              </div>
            </section>

            {/* Notifications */}
            <section className="space-y-4">
              <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                Notifiche
              </h2>
              <SettingsList ariaLabel="Preferenze notifiche">
                <SettingsRow
                  icon="📧"
                  label="Notifiche email"
                  description="Ricevi email per eventi importanti"
                  trailing={
                    <ToggleSwitch
                      checked={form.emailNotifications}
                      onCheckedChange={(checked: boolean) => update('emailNotifications', checked)}
                      ariaLabel="Attiva notifiche email"
                    />
                  }
                />
              </SettingsList>
              <p className="text-xs text-muted-foreground">
                Preferenze dettagliate per PDF e sessioni:{' '}
                <button
                  type="button"
                  onClick={() => router.push('/settings/notifications')}
                  className="underline hover:text-foreground"
                >
                  gestisci notifiche
                </button>
                .
              </p>
            </section>

            {/* Privacy */}
            <section className="space-y-4">
              <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                Privacy
              </h2>
              <SettingsList ariaLabel="Preferenze privacy">
                <SettingsRow
                  icon="👤"
                  label="Profilo pubblico"
                  description="Altri utenti possono vedere il tuo profilo"
                  trailing={
                    <ToggleSwitch
                      checked={form.showProfile}
                      onCheckedChange={(checked: boolean) => update('showProfile', checked)}
                      ariaLabel="Mostra profilo"
                    />
                  }
                />
                <SettingsRow
                  icon="📊"
                  label="Attività visibile"
                  description="Mostra le tue partite nel feed attività"
                  trailing={
                    <ToggleSwitch
                      checked={form.showActivity}
                      onCheckedChange={(checked: boolean) => update('showActivity', checked)}
                      ariaLabel="Mostra attività"
                    />
                  }
                />
                <SettingsRow
                  icon="📚"
                  label="Libreria visibile"
                  description="Altri utenti possono sfogliare la tua libreria"
                  trailing={
                    <ToggleSwitch
                      checked={form.showLibrary}
                      onCheckedChange={(checked: boolean) => update('showLibrary', checked)}
                      ariaLabel="Mostra libreria"
                    />
                  }
                />
              </SettingsList>
            </section>

            {/* Data retention */}
            <section className="space-y-4">
              <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                Conservazione dati
              </h2>
              <div className="space-y-3 rounded-xl border border-border bg-card p-5">
                <Label htmlFor="retention">Periodo di conservazione attività</Label>
                <select
                  id="retention"
                  value={form.dataRetentionDays}
                  onChange={e => {
                    const next = Number.parseInt(e.target.value, 10);
                    if (!Number.isNaN(next)) update('dataRetentionDays', next);
                  }}
                  disabled={isSaving}
                  className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/40"
                >
                  {RETENTION_OPTIONS.map(opt => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-muted-foreground">
                  Dopo questo periodo, log e attività vengono automaticamente archiviati.
                </p>
              </div>
            </section>

            {/* Actions */}
            <div className="flex items-center justify-between border-t border-border pt-6">
              <button
                type="button"
                onClick={() => router.push('/settings')}
                className="text-sm text-muted-foreground transition-colors hover:text-foreground"
              >
                ← Impostazioni
              </button>
              <Button onClick={handleSave} disabled={isSaving}>
                {isSaving ? 'Salvataggio...' : 'Salva preferenze'}
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export type { PrefsFormState };
