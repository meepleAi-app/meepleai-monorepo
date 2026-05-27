'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Monitor } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

interface SessionRow {
  readonly id: string;
  readonly userAgent: string | null;
  readonly ipAddress: string | null;
  readonly lastSeenAt: string | null;
  readonly isCurrent: boolean;
}

function parseDeviceLabel(userAgent: string | null): string {
  if (!userAgent) return 'Unknown device';
  if (/iphone|ios/i.test(userAgent)) return 'iPhone';
  if (/ipad/i.test(userAgent)) return 'iPad';
  if (/android/i.test(userAgent)) return 'Android';
  if (/edg/i.test(userAgent)) return 'Edge';
  if (/chrome/i.test(userAgent)) return 'Chrome';
  if (/firefox/i.test(userAgent)) return 'Firefox';
  if (/safari/i.test(userAgent)) return 'Safari';
  return userAgent.split('/')[0] || 'Unknown device';
}

function formatRelative(iso: string | null): string {
  if (!iso) return '—';
  const diffMs = Date.now() - new Date(iso).getTime();
  const min = Math.floor(diffMs / 60000);
  if (min < 1) return 'now';
  if (min < 60) return `${min} min ago`;
  const h = Math.floor(min / 60);
  if (h < 24) return `${h} h ago`;
  const d = Math.floor(h / 24);
  return `${d} d ago`;
}

export function ActiveSessionsCard(): React.JSX.Element {
  const queryClient = useQueryClient();

  const sessionsQuery = useQuery({
    queryKey: ['user-sessions'],
    queryFn: () => api.auth.getUserSessions(),
  });

  const revoke = useMutation({
    mutationFn: (sid: string) => api.auth.revokeSession(sid),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['user-sessions'] }),
  });

  const revokeAll = useMutation({
    mutationFn: () => api.auth.revokeAllSessions(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['user-sessions'] }),
  });

  // Derive active sessions sorted by lastSeenAt descending.
  // The most-recently-seen session is treated as the current one
  // (SessionStatusResponse has no session id field, so we use this heuristic).
  const activeSessions = (sessionsQuery.data ?? [])
    .filter(s => s.revokedAt === null)
    .slice()
    .sort((a, b) => {
      const ta = a.lastSeenAt ? new Date(a.lastSeenAt).getTime() : 0;
      const tb = b.lastSeenAt ? new Date(b.lastSeenAt).getTime() : 0;
      return tb - ta;
    });

  const currentSessionId = activeSessions[0]?.id ?? null;

  const rows: SessionRow[] = activeSessions.map(s => ({
    id: s.id,
    userAgent: s.userAgent,
    ipAddress: s.ipAddress,
    lastSeenAt: s.lastSeenAt,
    isCurrent: s.id === currentSessionId,
  }));

  return (
    <section className="bg-card border border-border rounded-lg p-5">
      <header className="flex items-center justify-between mb-4">
        <h3 className="font-quicksand font-bold text-foreground">Active sessions</h3>
        {sessionsQuery.isLoading && (
          <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" aria-label="Loading" />
        )}
      </header>

      {sessionsQuery.isError && (
        <p className="text-sm text-destructive">Errore caricamento sessioni.</p>
      )}

      <ul className="divide-y divide-border">
        {rows.map(row => (
          <li key={row.id} className="flex items-center gap-3 py-3">
            <Monitor className="h-5 w-5 text-muted-foreground shrink-0" aria-hidden />
            <div className="flex-1 min-w-0">
              <p className="font-quicksand font-bold text-sm text-foreground">
                {parseDeviceLabel(row.userAgent)}
                {row.isCurrent && (
                  <span className="ml-2 font-mono text-[10px] font-bold px-1.5 py-0.5 rounded-sm bg-success/15 text-success">
                    CURRENT
                  </span>
                )}
              </p>
              <p className="font-mono text-xs text-muted-foreground truncate">
                {formatRelative(row.lastSeenAt)} · {row.ipAddress ?? 'unknown ip'}
              </p>
            </div>
            <Button
              size="sm"
              variant={row.isCurrent ? 'ghost' : 'destructive'}
              disabled={row.isCurrent || revoke.isPending}
              onClick={() => revoke.mutate(row.id)}
            >
              {row.isCurrent ? 'This session' : 'Revoke'}
            </Button>
          </li>
        ))}
      </ul>

      {rows.length > 1 && (
        <div className="flex justify-end mt-4">
          <Button
            size="sm"
            variant="outline"
            disabled={revokeAll.isPending}
            onClick={() => revokeAll.mutate()}
          >
            Sign out all other sessions
          </Button>
        </div>
      )}
    </section>
  );
}
