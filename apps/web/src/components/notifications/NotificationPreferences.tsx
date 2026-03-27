/**
 * NotificationPreferences Component (US-41)
 *
 * Client component that fetches and manages notification preferences
 * via the modular API client. Renders toggle switches grouped by
 * category (Documents, Game Nights).
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { Bell, Calendar, Loader2, Mail, MessageSquare, Save, Smartphone } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { NotificationPreferences as NotificationPreferencesType } from '@/lib/api/schemas/notifications.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

type PreferenceKey = keyof Omit<NotificationPreferencesType, 'userId' | 'hasPushSubscription'>;

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

  const handleSave = useCallback(async () => {
    if (!prefs) return;

    setIsSaving(true);
    try {
      const { userId: _userId, ...prefsWithoutUserId } = prefs;
      await api.notifications.updatePreferences(prefsWithoutUserId);
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
    </div>
  );
}
