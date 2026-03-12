'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Pause, Play, Trash2, FileText, Bot } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/data-display/card';

export interface PausedSession {
  id: string;
  sessionDate: string;
  currentTurn?: number;
  totalTurns?: number;
  participants: { displayName: string; score: number }[];
  hasPhotos: boolean;
  hasNotes: boolean;
  hasAgentSummary: boolean;
}

interface PausedSessionCardProps {
  session: PausedSession;
  onResume: (sessionId: string) => void;
  onAbandon: (sessionId: string) => void;
}

const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000;

export function PausedSessionCard({ session, onResume, onAbandon }: PausedSessionCardProps) {
  const isOld = Date.now() - new Date(session.sessionDate).getTime() > THIRTY_DAYS_MS;

  return (
    <Card className="border-amber-500/30 bg-amber-500/5">
      <CardContent className="p-4">
        <div className="flex items-center gap-2 mb-2">
          <Pause className="h-4 w-4 text-amber-500" />
          <span className="font-medium">Partita in pausa</span>
          {isOld && (
            <span className="text-xs bg-amber-100 dark:bg-amber-900 text-amber-700 dark:text-amber-300 px-2 py-0.5 rounded">
              Vecchia
            </span>
          )}
        </div>

        <p className="text-sm text-muted-foreground mb-1">
          {formatDistanceToNow(new Date(session.sessionDate), { addSuffix: true, locale: it })}
          {session.currentTurn != null && session.totalTurns != null && (
            <>
              {' '}
              &middot; Turno {session.currentTurn} di {session.totalTurns}
            </>
          )}
        </p>

        <p className="text-sm mb-3">
          {session.participants.map(p => `${p.displayName}: ${p.score}`).join(' | ')}
        </p>

        <div className="flex items-center gap-2 text-xs text-muted-foreground mb-3">
          {session.hasNotes && (
            <span className="flex items-center gap-1">
              <FileText className="h-3 w-3" /> Note
            </span>
          )}
          {session.hasAgentSummary && (
            <span className="flex items-center gap-1">
              <Bot className="h-3 w-3" /> Riepilogo AI
            </span>
          )}
        </div>

        <div className="flex gap-2">
          <Button
            size="sm"
            onClick={() => {
              if (isOld) {
                const confirmed = window.confirm(
                  'Questa partita è stata messa in pausa più di 30 giorni fa. Vuoi ancora riprenderla?'
                );
                if (!confirmed) return;
              }
              onResume(session.id);
            }}
          >
            <Play className="mr-1 h-3 w-3" /> Riprendi
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="text-destructive"
            onClick={() => onAbandon(session.id)}
          >
            <Trash2 className="mr-1 h-3 w-3" /> Abbandona
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
