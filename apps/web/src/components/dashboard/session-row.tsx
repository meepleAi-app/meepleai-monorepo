/**
 * SessionRow Component - Issue #4581
 * Single session display for Recent Sessions list
 */

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';

import { Button } from '@/components/ui/primitives/button';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

interface SessionRowProps {
  session: SessionSummaryDto;
  onViewDetails?: (id: string) => void;
}

export function SessionRow({ session, onViewDetails }: SessionRowProps) {
  const sessionDate = new Date(session.sessionDate);
  const relativeTime = formatDistanceToNow(sessionDate, { addSuffix: true, locale: it });

  const formatDuration = (duration?: string): string => {
    if (!duration) return '';
    const parts = duration.split(':');
    const hours = parseInt(parts[0] || '0', 10);
    const minutes = parseInt(parts[1] || '0', 10);
    return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
  };

  return (
    <div className="flex items-center gap-4 p-4 rounded-lg hover:bg-accent/50 transition-colors">
      {/* Game thumbnail */}
      {session.gameImageUrl && (
        <div className="flex-shrink-0 w-16 h-16 rounded-md overflow-hidden bg-muted">
          <img
            src={session.gameImageUrl}
            alt={session.gameName}
            className="w-full h-full object-cover"
          />
        </div>
      )}

      {/* Session info */}
      <div className="flex-1 min-w-0">
        <h4 className="font-quicksand font-semibold text-base truncate">{session.gameName}</h4>
        <div className="flex items-center gap-3 text-sm text-muted-foreground font-nunito mt-1">
          <span>{relativeTime}</span>
          <span>•</span>
          <span>{session.playerCount} giocatori</span>
          {session.duration && (
            <>
              <span>•</span>
              <span>{formatDuration(session.duration)}</span>
            </>
          )}
          {session.averageScore !== null && session.averageScore !== undefined && (
            <>
              <span>•</span>
              <span>⭐ {session.averageScore}/100</span>
            </>
          )}
        </div>
        {session.winnerName && (
          <div className="text-xs text-muted-foreground mt-1">Vittoria: {session.winnerName}</div>
        )}
      </div>

      {/* Actions */}
      {onViewDetails && (
        <Button
          variant="ghost"
          size="sm"
          onClick={() => onViewDetails(session.id)}
          className="flex-shrink-0"
        >
          Dettagli →
        </Button>
      )}
    </div>
  );
}
