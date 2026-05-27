'use client';

import { useEffect, useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Check, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';

export function ProfileSection(): React.JSX.Element {
  const queryClient = useQueryClient();
  const profileQuery = useQuery({
    queryKey: ['profile'],
    queryFn: () => api.auth.getProfile(),
  });

  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [feedback, setFeedback] = useState<{ kind: 'ok' | 'error'; msg: string } | null>(null);

  // Hydrate local form state when profile loads / refetches.
  useEffect(() => {
    if (profileQuery.data) {
      setDisplayName(profileQuery.data.displayName ?? '');
      setEmail(profileQuery.data.email ?? '');
    }
  }, [profileQuery.data]);

  const update = useMutation({
    mutationFn: () => api.auth.updateProfile({ displayName, email }),
    onSuccess: result => {
      if (result.ok) {
        setFeedback({ kind: 'ok', msg: result.message || 'Profile updated' });
        void queryClient.invalidateQueries({ queryKey: ['profile'] });
      } else {
        setFeedback({ kind: 'error', msg: result.message || 'Update failed' });
      }
    },
    onError: (err: Error) => setFeedback({ kind: 'error', msg: err.message ?? 'Update failed' }),
  });

  if (profileQuery.isLoading) {
    return (
      <div className="bg-card border border-border rounded-lg p-5 flex items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" aria-label="Loading" />
      </div>
    );
  }

  if (profileQuery.isError) {
    return (
      <div
        role="alert"
        className="bg-destructive/5 border border-destructive/30 rounded-lg p-5 text-destructive"
      >
        Errore caricamento profilo.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <section className="bg-card border border-border rounded-lg p-5 space-y-4">
        <h3 className="font-quicksand font-bold text-foreground">Profile</h3>

        <div className="space-y-2">
          <label
            htmlFor="profile-display-name"
            className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
          >
            Display name
          </label>
          <Input
            id="profile-display-name"
            value={displayName}
            onChange={e => setDisplayName(e.target.value)}
            maxLength={64}
          />
        </div>

        <div className="space-y-2">
          <label
            htmlFor="profile-email"
            className="text-xs font-mono font-bold uppercase text-muted-foreground tracking-wide"
          >
            Email
          </label>
          <Input
            id="profile-email"
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
          />
        </div>

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
            data-testid="save-profile-button"
            onClick={() => {
              setFeedback(null);
              update.mutate();
            }}
            disabled={update.isPending}
          >
            {update.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
            Save changes
          </Button>
        </div>
      </section>
    </div>
  );
}
