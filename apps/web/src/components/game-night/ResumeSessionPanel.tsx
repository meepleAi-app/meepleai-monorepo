'use client';

/**
 * ResumeSessionPanel — Rich resume experience with recap, scores, and photos.
 *
 * Fetches session resume context and displays a summary card
 * for paused sessions, allowing users to quickly resume.
 *
 * Issue #122 — Enhanced Save/Resume
 */

import { useQuery } from '@tanstack/react-query';
import { Camera, Clock, Play, Trophy, Users } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { SessionResumeContext } from '@/lib/api/schemas/save-resume.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface ResumeSessionPanelProps {
  sessionId: string;
  onResume: () => void;
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function formatTimeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'adesso';
  if (minutes < 60) return `${minutes} min fa`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h fa`;
  const days = Math.floor(hours / 24);
  return `${days}g fa`;
}

// ============================================================================
// Component
// ============================================================================

export function ResumeSessionPanel({ sessionId, onResume, className }: ResumeSessionPanelProps) {
  const { data: context, isLoading } = useQuery<SessionResumeContext>({
    queryKey: ['session-resume-context', sessionId],
    queryFn: () => api.liveSessions.getResumeContext(sessionId),
  });

  if (isLoading || !context) return null;

  return (
    <div
      className={cn(
        'rounded-xl border border-amber-500/30 bg-amber-50/50 dark:bg-amber-900/10',
        'p-4 sm:p-6 space-y-3',
        className
      )}
      data-testid="resume-session-panel"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="font-quicksand font-bold text-base sm:text-lg text-slate-900 dark:text-slate-100">
          {context.gameTitle}
        </h3>
        <span className="text-xs text-muted-foreground flex items-center gap-1">
          <Clock className="h-3 w-3" aria-hidden="true" />
          {formatTimeAgo(context.pausedAt)}
        </span>
      </div>

      {/* Recap */}
      <p className="text-sm text-muted-foreground">{context.recap}</p>

      {/* Score summary */}
      {context.playerScores.length > 0 && (
        <div className="flex items-center gap-2 flex-wrap" data-testid="resume-scores">
          <Users className="h-4 w-4 text-muted-foreground flex-shrink-0" aria-hidden="true" />
          {context.playerScores.map(p => (
            <span
              key={p.playerId}
              className={cn(
                'text-xs sm:text-sm px-2 py-0.5 rounded-full',
                p.rank === 1
                  ? 'bg-amber-200/60 dark:bg-amber-500/20 text-amber-900 dark:text-amber-300 font-medium'
                  : 'bg-slate-100 dark:bg-slate-700/50 text-slate-700 dark:text-slate-300'
              )}
            >
              {p.rank === 1 && <Trophy className="inline h-3 w-3 mr-0.5" aria-hidden="true" />}
              {p.name}: {p.totalScore}
            </span>
          ))}
        </div>
      )}

      {/* Photo thumbnails */}
      {context.photos.length > 0 && (
        <div className="flex items-center gap-2" data-testid="resume-photos">
          <Camera className="h-4 w-4 text-muted-foreground flex-shrink-0" aria-hidden="true" />
          <div className="flex gap-1">
            {context.photos.slice(0, 4).map(photo => (
              <div
                key={photo.attachmentId}
                className="h-10 w-10 rounded bg-slate-200 dark:bg-slate-700 overflow-hidden"
              >
                {photo.thumbnailUrl && (
                  <img
                    src={photo.thumbnailUrl}
                    alt={photo.caption ?? 'Foto sessione'}
                    className="h-full w-full object-cover"
                  />
                )}
              </div>
            ))}
            {context.photos.length > 4 && (
              <span className="text-xs text-muted-foreground self-center">
                +{context.photos.length - 4}
              </span>
            )}
          </div>
        </div>
      )}

      {/* Resume button */}
      <Button
        onClick={onResume}
        className="w-full bg-amber-600 hover:bg-amber-700 text-white"
        data-testid="resume-session-button"
      >
        <Play className="h-4 w-4 mr-2" aria-hidden="true" />
        Riprendi Partita (turno {context.currentTurn})
      </Button>
    </div>
  );
}
