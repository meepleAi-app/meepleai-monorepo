/**
 * NotificationPreferences Component (US-41)
 *
 * Client component that fetches and manages notification preferences
 * via the modular API client. Renders toggle switches grouped by
 * category (Documents, Game Nights).
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { Bell, Calendar, Loader2, Mail, MessageSquare, Moon, Save, Smartphone } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { NotificationPreferences as NotificationPreferencesType } from '@/lib/api/schemas/notifications.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

// Boolean-only preference keys (Switch rows). Quiet-hours fields are string/nullable and handled by
// the dedicated Quiet Hours section, so they are excluded here.
type PreferenceKey = keyof Omit<
  NotificationPreferencesType,
  'userId' | 'hasPushSubscription' | 'timeZone' | 'quietHoursStart' | 'quietHoursEnd'
>;

// Curated IANA timezone options for the quiet-hours picker (the user's current value is always merged in).
const COMMON_TIMEZONES = [
  'UTC',
  'Europe/Rome',
  'Europe/London',
  'Europe/Paris',
  'Europe/Berlin',
  'Europe/Madrid',
  'America/New_York',
  'America/Chicago',
  'America/Los_Angeles',
  'America/Sao_Paulo',
  'Asia/Tokyo',
  'Australia/Sydney',
] as const;

interface PreferenceRow {
  key: PreferenceKey;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  description: string;
}

interface PreferenceCategory {
  id: string;
  title: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
  iconColor: string;
  rows: PreferenceRow[];
}

// ============================================================================
// Configuration
// ============================================================================

const PREFERENCE_CATEGORIES: PreferenceCategory[] = [
  {
    id: 'document-ready',
    title: 'Documento pronto',
    description: 'Notifiche quando il PDF viene elaborato con successo',
    icon: Bell,
    iconColor: 'bg-green-100 dark:bg-green-900/20 text-green-600 dark:text-green-400',
    rows: [
      {
        key: 'emailOnDocumentReady',
        icon: Mail,
        label: 'Email',
        description: 'Ricevi email quando il documento è pronto',
      },
      {
        key: 'pushOnDocumentReady',
        icon: Smartphone,
        label: 'Push',
        description: 'Notifica push del browser',
      },
      {
        key: 'inAppOnDocumentReady',
        icon: MessageSquare,
        label: 'In-App',
        description: 'Mostra nel centro notifiche',
      },
    ],
  },
  {
    id: 'document-failed',
    title: 'Elaborazione fallita',
    description: 'Notifiche quando si verifica un errore',
    icon: Bell,
    iconColor: 'bg-red-100 dark:bg-red-900/20 text-red-600 dark:text-red-400',
    rows: [
      {
        key: 'emailOnDocumentFailed',
        icon: Mail,
        label: 'Email',
        description: 'Ricevi email con dettagli errore',
      },
      {
        key: 'pushOnDocumentFailed',
        icon: Smartphone,
        label: 'Push',
        description: 'Notifica push del browser',
      },
      {
        key: 'inAppOnDocumentFailed',
        icon: MessageSquare,
        label: 'In-App',
        description: 'Mostra errore nel centro notifiche',
      },
    ],
  },
  {
    id: 'game-night-invitations',
    title: 'Serate Giochi — Inviti',
    description: 'Notifiche quando vieni invitato a una serata giochi',
    icon: Calendar,
    iconColor: 'bg-amber-100 dark:bg-amber-900/20 text-amber-600 dark:text-amber-400',
    rows: [
      {
        key: 'emailOnGameNightInvitation',
        icon: Mail,
        label: 'Email',
        description: 'Ricevi email per nuovi inviti',
      },
      {
        key: 'pushOnGameNightInvitation',
        icon: Smartphone,
        label: 'Push',
        description: 'Notifica push per nuovi inviti',
      },
      {
        key: 'inAppOnGameNightInvitation',
        icon: MessageSquare,
        label: 'In-App',
        description: 'Mostra nel centro notifiche',
      },
    ],
  },
  {
    id: 'game-night-reminders',
    title: 'Serate Giochi — Promemoria',
    description: 'Promemoria 24h e 1h prima della serata',
    icon: Calendar,
    iconColor: 'bg-amber-100 dark:bg-amber-900/20 text-amber-600 dark:text-amber-400',
    rows: [
      {
        key: 'emailOnGameNightReminder',
        icon: Mail,
        label: 'Email',
        description: 'Ricevi email di promemoria',
      },
      {
        key: 'pushOnGameNightReminder',
        icon: Smartphone,
        label: 'Push',
        description: 'Notifica push di promemoria',
      },
    ],
  },
  {
    id: 'retry-available',
    title: 'Retry disponibile',
    description: 'Notifiche quando il sistema ritenta un documento fallito',
    icon: Bell,
    iconColor: 'bg-blue-100 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400',
    rows: [
      {
        key: 'emailOnRetryAvailable',
        icon: Mail,
        label: 'Email',
        description: 'Ricevi email quando inizia il retry',
      },
      {
        key: 'pushOnRetryAvailable',
        icon: Smartphone,
        label: 'Push',
        description: 'Notifica push del browser',
      },
      {
        key: 'inAppOnRetryAvailable',
        icon: MessageSquare,
        label: 'In-App',
        description: 'Mostra stato retry nel centro notifiche',
      },
    ],
  },
  {
    // #535 / #2832: admin-only opt-in email when a mechanic card is suppressed.
    id: 'card-suppressed',
    title: 'Scheda meccanica soppressa (admin)',
    description: 'Email quando una scheda meccaniche viene soppressa (auto o manuale)',
    icon: Bell,
    iconColor: 'bg-amber-100 dark:bg-amber-900/20 text-amber-600 dark:text-amber-400',
    rows: [
      {
        key: 'emailOnCardSuppressed',
        icon: Mail,
        label: 'Email',
        description: 'Ricevi email quando una scheda meccaniche viene soppressa',
      },
    ],
  },
  {
    // Issue #2994: Slack channel preferences (persisted via the dedicated Slack endpoint).
    id: 'slack',
    title: 'Slack',
    description: 'Ricevi notifiche come messaggio diretto su Slack (richiede un account collegato)',
    icon: MessageSquare,
    iconColor: 'bg-purple-100 dark:bg-purple-900/20 text-purple-600 dark:text-purple-400',
    rows: [
      {
        key: 'slackEnabled',
        icon: MessageSquare,
        label: 'Slack attivo',
        description: 'Abilita globalmente le notifiche Slack',
      },
      {
        key: 'slackOnDocumentReady',
        icon: MessageSquare,
        label: 'Documento pronto',
        description: 'Messaggio Slack quando il documento è pronto',
      },
      {
        key: 'slackOnDocumentFailed',
        icon: MessageSquare,
        label: 'Elaborazione fallita',
        description: 'Messaggio Slack in caso di errore',
      },
      {
        key: 'slackOnRetryAvailable',
        icon: MessageSquare,
        label: 'Retry disponibile',
        description: 'Messaggio Slack quando inizia il retry',
      },
      {
        key: 'slackOnGameNightInvitation',
        icon: MessageSquare,
        label: 'Inviti serata',
        description: 'Messaggio Slack per nuovi inviti a serate giochi',
      },
      {
        key: 'slackOnGameNightReminder',
        icon: MessageSquare,
        label: 'Promemoria serata',
        description: 'Messaggio Slack di promemoria serata',
      },
      {
        key: 'slackOnShareRequestCreated',
        icon: MessageSquare,
        label: 'Share request creata',
        description: 'Messaggio Slack per nuove share request',
      },
      {
        key: 'slackOnShareRequestApproved',
        icon: MessageSquare,
        label: 'Share request approvata',
        description: 'Messaggio Slack quando una share request è approvata',
      },
      {
        key: 'slackOnBadgeEarned',
        icon: MessageSquare,
        label: 'Badge ottenuto',
        description: 'Messaggio Slack quando ottieni un badge',
      },
    ],
  },
];

// ============================================================================
// Component
// ============================================================================

export function NotificationPreferences() {
  const [prefs, setPrefs] = useState<NotificationPreferencesType | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { toast } = useToast();

  // Fetch preferences on mount
  useEffect(() => {
    const load = async () => {
      try {
        setError(null);
        const data = await api.notifications.getPreferences();
        setPrefs(data);
      } catch (_err) {
        setError('Impossibile caricare le preferenze');
        toast({
          title: 'Errore',
          description: 'Impossibile caricare le preferenze di notifica',
          variant: 'destructive',
        });
      } finally {
        setIsLoading(false);
      }
    };

    void load();
  }, [toast]);

  const updatePref = useCallback(
    (key: PreferenceKey, value: boolean) => {
      if (!prefs) return;
      setPrefs({ ...prefs, [key]: value });
    },
    [prefs]
  );

  // Quiet hours (ADR-076 / #2995): "enabled" is derived from both bounds being set.
  const quietHoursEnabled = Boolean(prefs?.quietHoursStart && prefs?.quietHoursEnd);

  const setQuietHoursEnabled = useCallback(
    (enabled: boolean) => {
      if (!prefs) return;
      setPrefs({
        ...prefs,
        // Enabling defaults to a 22:00 → 08:00 overnight window; disabling clears both bounds.
        quietHoursStart: enabled ? (prefs.quietHoursStart ?? '22:00') : null,
        quietHoursEnd: enabled ? (prefs.quietHoursEnd ?? '08:00') : null,
      });
    },
    [prefs]
  );

  const setQuietHoursField = useCallback(
    (key: 'timeZone' | 'quietHoursStart' | 'quietHoursEnd', value: string) => {
      if (!prefs) return;
      setPrefs({ ...prefs, [key]: value });
    },
    [prefs]
  );

  const timezoneOptions = useMemo(() => {
    const base = [...COMMON_TIMEZONES] as string[];
    const current = prefs?.timeZone;
    return current && !base.includes(current) ? [current, ...base] : base;
  }, [prefs?.timeZone]);

  const handleSave = useCallback(async () => {
    if (!prefs) return;

    setIsSaving(true);
    try {
      const { userId: _userId, ...prefsWithoutUserId } = prefs;
      await api.notifications.updatePreferences(prefsWithoutUserId);
      // #2832: card-suppression email opt-in persists via its dedicated endpoint (the generic PUT
      // above only handles the Document preferences and would otherwise ignore this field).
      await api.notifications.updateCardSuppressionEmailPreference(
        prefs.emailOnCardSuppressed ?? false
      );
      // #2994: Slack + quiet-hours each round-trip through their own endpoint (the generic PUT ignores them).
      await api.notifications.updateSlackPreferences({
        slackEnabled: prefs.slackEnabled ?? true,
        slackOnDocumentReady: prefs.slackOnDocumentReady ?? true,
        slackOnDocumentFailed: prefs.slackOnDocumentFailed ?? true,
        slackOnRetryAvailable: prefs.slackOnRetryAvailable ?? false,
        slackOnGameNightInvitation: prefs.slackOnGameNightInvitation ?? true,
        slackOnGameNightReminder: prefs.slackOnGameNightReminder ?? true,
        slackOnShareRequestCreated: prefs.slackOnShareRequestCreated ?? true,
        slackOnShareRequestApproved: prefs.slackOnShareRequestApproved ?? true,
        slackOnBadgeEarned: prefs.slackOnBadgeEarned ?? true,
      });
      await api.notifications.updateQuietHours({
        timeZone: prefs.timeZone ?? 'UTC',
        quietHoursStart: prefs.quietHoursStart ?? null,
        quietHoursEnd: prefs.quietHoursEnd ?? null,
      });
      toast({
        title: 'Salvato',
        description: 'Preferenze di notifica aggiornate',
      });
    } catch (_err) {
      toast({
        title: 'Errore',
        description: 'Impossibile salvare le preferenze',
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  }, [prefs, toast]);

  // Loading state
  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  // Error state
  if (error && !prefs) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <Bell className="h-12 w-12 text-muted-foreground/30 mb-3" />
        <p className="text-sm text-destructive">{error}</p>
        <Button
          variant="outline"
          size="sm"
          className="mt-4"
          onClick={() => {
            setIsLoading(true);
            setError(null);
            void api.notifications
              .getPreferences()
              .then(setPrefs)
              .catch(() => {
                setError('Impossibile caricare le preferenze');
              })
              .finally(() => setIsLoading(false));
          }}
        >
          Riprova
        </Button>
      </div>
    );
  }

  if (!prefs) return null;

  return (
    <div className="space-y-6">
      {/* Save button */}
      <div className="flex justify-end">
        <Button
          onClick={() => void handleSave()}
          disabled={isSaving}
          data-testid="save-preferences"
        >
          {isSaving ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Salvataggio...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Salva preferenze
            </>
          )}
        </Button>
      </div>

      {/* Categories */}
      {PREFERENCE_CATEGORIES.map(category => {
        const CategoryIcon = category.icon;
        return (
          <div
            key={category.id}
            className="bg-card rounded-xl p-6 border border-border/50"
            data-testid={`pref-category-${category.id}`}
          >
            <div className="flex items-start gap-4 mb-6">
              <div className={cn('p-2 rounded-lg', category.iconColor)}>
                <CategoryIcon className="h-6 w-6" />
              </div>
              <div className="flex-1">
                <h2 className="text-xl font-bold mb-1">{category.title}</h2>
                <p className="text-sm text-muted-foreground">{category.description}</p>
              </div>
            </div>

            <div className="space-y-4">
              {category.rows.map(row => {
                const RowIcon = row.icon;
                const value = prefs[row.key];
                return (
                  <div key={row.key} className="flex items-center justify-between py-2">
                    <div className="flex items-center gap-3">
                      <RowIcon className="h-5 w-5 text-muted-foreground" />
                      <div>
                        <p className="font-medium">{row.label}</p>
                        <p className="text-sm text-muted-foreground">{row.description}</p>
                      </div>
                    </div>
                    <Switch
                      checked={typeof value === 'boolean' ? value : true}
                      onCheckedChange={checked => updatePref(row.key, checked)}
                      data-testid={`pref-${row.key}`}
                    />
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}

      {/* Quiet hours (ADR-076 / #2995) — custom section: time-range + timezone, not boolean toggles */}
      <div
        className="bg-card rounded-xl p-6 border border-border/50"
        data-testid="pref-category-quiet-hours"
      >
        <div className="flex items-start gap-4 mb-6">
          <div className="p-2 rounded-lg bg-indigo-100 dark:bg-indigo-900/20 text-indigo-600 dark:text-indigo-400">
            <Moon className="h-6 w-6" />
          </div>
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-1">Ore di silenzio</h2>
            <p className="text-sm text-muted-foreground">
              Sospendi email e Slack durante una fascia oraria (le notifiche in-app restano sempre
              visibili)
            </p>
          </div>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between py-2">
            <div className="flex items-center gap-3">
              <Moon className="h-5 w-5 text-muted-foreground" />
              <div>
                <p className="font-medium">Attiva ore di silenzio</p>
                <p className="text-sm text-muted-foreground">
                  Email e Slack non verranno inviati nella fascia impostata
                </p>
              </div>
            </div>
            <Switch
              checked={quietHoursEnabled}
              onCheckedChange={setQuietHoursEnabled}
              data-testid="pref-quietHoursEnabled"
            />
          </div>

          {quietHoursEnabled && (
            <div className="grid gap-4 sm:grid-cols-3">
              <label className="flex flex-col gap-1">
                <span className="text-sm font-medium">Fuso orario</span>
                <select
                  value={prefs.timeZone ?? 'UTC'}
                  onChange={e => setQuietHoursField('timeZone', e.target.value)}
                  data-testid="pref-quietHoursTimeZone"
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground"
                >
                  {timezoneOptions.map(tz => (
                    <option key={tz} value={tz}>
                      {tz}
                    </option>
                  ))}
                </select>
              </label>
              <label className="flex flex-col gap-1">
                <span className="text-sm font-medium">Inizio</span>
                <input
                  type="time"
                  value={prefs.quietHoursStart ?? '22:00'}
                  onChange={e => setQuietHoursField('quietHoursStart', e.target.value)}
                  data-testid="pref-quietHoursStart"
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground"
                />
              </label>
              <label className="flex flex-col gap-1">
                <span className="text-sm font-medium">Fine</span>
                <input
                  type="time"
                  value={prefs.quietHoursEnd ?? '08:00'}
                  onChange={e => setQuietHoursField('quietHoursEnd', e.target.value)}
                  data-testid="pref-quietHoursEnd"
                  className="rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground"
                />
              </label>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
