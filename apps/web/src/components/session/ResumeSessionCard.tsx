'use client';

/**
 * ResumeSessionCard
 *
 * Task 4 — Session Pause/Resume Flow
 *
 * Displays a paused session as a rich card with amber styling.
 * Navigates to the scoreboard page when the user resumes.
 */

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Camera, Pause, Users } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';

export interface ResumeSessionCardProps {
  /** Session ID used to build the resume link. */
  sessionId: string;
  /** Display name of the game. */
  gameName: string;
  /** ISO datetime when the session was paused. */
  pausedAt: string;
  /** Number of players in the session. */
  playerCount: number;
  /** Short invite/join code for the session. */
  sessionCode: string;
  /** Optional number of photos saved for this session. */
  photoCount?: number;
}

export function ResumeSessionCard({
  sessionId,
  gameName,
  pausedAt,
  playerCount,
  sessionCode,
  photoCount,
}: ResumeSessionCardProps) {
  const timeAgo = formatDistanceToNow(new Date(pausedAt), {
    addSuffix: false,
    locale: it,
  });

  const hasPhotos = typeof photoCount === 'number' && photoCount > 0;

  return (
    <div className="rounded-xl border border-amber-200 bg-gradient-to-r from-amber-50 to-orange-50 p-4 transition-shadow hover:shadow-md dark:from-amber-950/20 dark:to-orange-950/20 dark:border-amber-800">
      <div className="flex items-center justify-between gap-3">
        {/* Left: icon + session info */}
        <div className="flex items-center gap-3 min-w-0">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-amber-100 dark:bg-amber-900/40">
            <Pause className="h-5 w-5 text-amber-600" aria-hidden="true" />
          </div>

          <div className="min-w-0">
            {/* Status badge + elapsed time */}
            <div className="flex items-center gap-2 flex-wrap">
              <Badge
                variant="outline"
                className="bg-amber-100 text-amber-700 border-0 text-xs font-medium dark:bg-amber-900/30 dark:text-amber-300"
              >
                In pausa
              </Badge>
              <span className="text-xs text-muted-foreground">{timeAgo} fa</span>
            </div>

            {/* Game name */}
            <h3 className="font-semibold font-quicksand text-foreground truncate mt-0.5">
              {gameName}
            </h3>

            {/* Meta row: players · session code */}
            <div className="flex items-center gap-2 text-xs text-muted-foreground mt-0.5 flex-wrap">
              <Users className="h-3 w-3 shrink-0" aria-hidden="true" />
              <span>{playerCount} giocatori</span>
              <span>·</span>
              <span>{sessionCode}</span>
              {hasPhotos && (
                <>
                  <span>·</span>
                  <Camera className="h-3 w-3 shrink-0" aria-hidden="true" />
                  <span>{photoCount} foto salvate</span>
                </>
              )}
            </div>
          </div>
        </div>

        {/* Right: resume button */}
        <Button asChild size="sm" className="shrink-0 bg-amber-600 hover:bg-amber-700 text-white">
          <Link href={`/sessions/${sessionId}/scoreboard`}>Riprendi partita</Link>
        </Button>
      </div>
    </div>
  );
}
