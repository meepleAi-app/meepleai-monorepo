/**
 * RecentSessions Component - Issue #4581
 * List of recent gaming sessions
 */

import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/primitives/button';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

import { EmptyState } from './empty-states';
import { SessionRow } from './session-row';

interface RecentSessionsProps {
  sessions: SessionSummaryDto[];
  isLoading?: boolean;
  onNewSession?: () => void;
  onViewAll?: () => void;
}

export function RecentSessions({
  sessions,
  isLoading,
  onNewSession,
  onViewAll,
}: RecentSessionsProps) {
  if (isLoading) {
    return (
      <Card className="bg-white/70 backdrop-blur-md">
        <CardHeader className="flex flex-row items-center justify-between">
          <h2 className="text-xl font-quicksand font-semibold">🕐 Sessioni Recenti</h2>
        </CardHeader>
        <CardContent className="space-y-2">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="h-20 animate-pulse rounded-lg bg-muted" />
          ))}
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-white/70 backdrop-blur-md">
      <CardHeader className="flex flex-row items-center justify-between">
        <h2 className="text-xl font-quicksand font-semibold">🕐 Sessioni Recenti</h2>
        <div className="flex gap-2">
          {onNewSession && (
            <Button onClick={onNewSession} size="sm" variant="outline">
              + Nuova Partita
            </Button>
          )}
          {onViewAll && sessions.length > 0 && (
            <Button onClick={onViewAll} size="sm" variant="ghost">
              Vedi tutte
            </Button>
          )}
        </div>
      </CardHeader>

      <CardContent>
        {sessions.length === 0 ? (
          <EmptyState variant="no-sessions" />
        ) : (
          <div className="space-y-1">
            {sessions.map(session => (
              <SessionRow key={session.id} session={session} />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
