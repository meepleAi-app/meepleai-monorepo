'use client';

import { useEffect, useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Check, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

type Theme = 'light' | 'dark' | 'system';

export function PreferencesSection(): React.JSX.Element {
  const queryClient = useQueryClient();
  const prefsQuery = useQuery({
    queryKey: ['preferences'],
    queryFn: () => api.auth.getPreferences(),
  });

  const [theme, setTheme] = useState<Theme>('system');
  const [language, setLanguage] = useState('it');
  const [emailNotifications, setEmailNotifications] = useState(true);
  const [feedback, setFeedback] = useState<{ kind: 'ok' | 'error'; msg: string } | null>(null);

  useEffect(() => {
    if (prefsQuery.data) {
      setTheme((prefsQuery.data.theme as Theme) ?? 'system');
      setLanguage(prefsQuery.data.language ?? 'it');
      setEmailNotifications(prefsQuery.data.emailNotifications ?? true);
    }
  }, [prefsQuery.data]);

  const update = useMutation({
    mutationFn: () => api.auth.updatePreferences({ theme, language, emailNotifications }),
    onSuccess: () => {
      setFeedback({ kind: 'ok', msg: 'Preferences updated' });
      void queryClient.invalidateQueries({ queryKey: ['preferences'] });
      void queryClient.invalidateQueries({ queryKey: ['profile'] });
    },
    onError: (err: Error) => setFeedback({ kind: 'error', msg: err.message ?? 'Update failed' }),
  });

  if (prefsQuery.isLoading) {
    return (
      <div className="bg-card border border-border rounded-lg p-5 flex items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" aria-label="Loading" />
      </div>
    );
  }

  if (prefsQuery.isError) {
    return (
      <div
        role="alert"
        className="bg-destructive/5 border border-destructive/30 rounded-lg p-5 text-destructive"
      >
        Errore caricamento preferenze.
      </div>
    );
  }

  return (
    <section className="bg-card border border-border rounded-lg p-5 space-y-4">
      <h3 className="font-quicksand font-bold text-foreground">Preferences</h3>

      <div className="space-y-2">
        <label
          htmlFor="pref-theme"
          className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
        >
          Theme
        </label>
        <select
          id="pref-theme"
          value={theme}
          onChange={e => setTheme(e.target.value as Theme)}
          className="block w-full h-10 px-3 py-2 rounded-md border border-border bg-background text-sm text-foreground"
        >
          <option value="light">Light</option>
          <option value="dark">Dark</option>
          <option value="system">System</option>
        </select>
      </div>

      <div className="space-y-2">
        <label
          htmlFor="pref-language"
          className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
        >
          Language
        </label>
        <select
          id="pref-language"
          value={language}
          onChange={e => setLanguage(e.target.value)}
          className="block w-full h-10 px-3 py-2 rounded-md border border-border bg-background text-sm text-foreground"
        >
          <option value="it">Italiano</option>
          <option value="en">English</option>
          <option value="fr">Français</option>
        </select>
      </div>

      <label className="flex items-center gap-3 p-3 rounded-md bg-muted cursor-pointer select-none">
        <input
          type="checkbox"
          checked={emailNotifications}
          onChange={e => setEmailNotifications(e.target.checked)}
          className="w-[18px] h-[18px]"
        />
        <span className="text-sm text-foreground">Ricevi notifiche via email</span>
      </label>

      {feedback && (
        <div
          role="alert"
          className={
            feedback.kind === 'ok'
              ? 'flex items-center gap-2 p-2 rounded-md bg-success/10 text-success text-sm'
              : 'flex items-center gap-2 p-2 rounded-md bg-destructive/10 text-destructive text-sm'
          }
        >
          {feedback.kind === 'ok' ? (
            <Check className="h-4 w-4 shrink-0" aria-hidden />
          ) : (
            <AlertCircle className="h-4 w-4 shrink-0" aria-hidden />
          )}
          <span>{feedback.msg}</span>
        </div>
      )}

      <div className="flex justify-end pt-2">
        <Button
          data-testid="save-preferences-button"
          onClick={() => {
            setFeedback(null);
            update.mutate();
          }}
          disabled={update.isPending}
        >
          {update.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
          Save preferences
        </Button>
      </div>
    </section>
  );
}
