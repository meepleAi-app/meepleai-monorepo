/**
 * SpectatorModeCard - Read-only session view for Spectators
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 *
 * Shows live scores and session info without interactive controls.
 * Includes "Request to Play" button to ask host for promotion.
 */

'use client';

import { useState, useCallback } from 'react';

import { Eye, Hand, MessageSquare } from 'lucide-react';

import type { Participant, ScoreEntry } from '@/components/session/types';

interface SpectatorModeCardProps {
  sessionId: string;
  participants: Participant[];
  scores: ScoreEntry[];
  sessionStatus: 'Active' | 'Paused' | 'Finalized';
  onRequestToPlay?: () => void;
  className?: string;
}

export function SpectatorModeCard({
  sessionId: _sessionId,
  participants,
  scores: _scores,
  sessionStatus,
  onRequestToPlay,
  className = '',
}: SpectatorModeCardProps) {
  const [hasRequested, setHasRequested] = useState(false);

  const handleRequestToPlay = useCallback(() => {
    setHasRequested(true);
    onRequestToPlay?.();
  }, [onRequestToPlay]);

  const sortedParticipants = [...participants].sort((a, b) => b.totalScore - a.totalScore);
  const isActive = sessionStatus === 'Active';

  return (
    <div className={`space-y-4 ${className}`} data-testid="spectator-mode-card">
      {/* Spectator Banner */}
      <div className="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2">
        <div className="flex items-center gap-2 text-sm text-gray-600">
          <Eye className="h-4 w-4" />
          <span className="font-medium">Spectator Mode</span>
        </div>

        {isActive && !hasRequested && (
          <button
            onClick={handleRequestToPlay}
            className="flex items-center gap-1 rounded-md bg-indigo-100 px-2.5 py-1 text-xs font-medium text-indigo-700 transition-colors hover:bg-indigo-200"
            aria-label="Request to play"
          >
            <Hand className="h-3 w-3" />
            Request to Play
          </button>
        )}

        {hasRequested && <span className="text-xs text-gray-400">Request sent</span>}
      </div>

      {/* Live Scoreboard (Read-Only) */}
      <div className="space-y-1.5">
        <h3 className="text-xs font-semibold uppercase tracking-wider text-gray-500">
          Live Scoreboard
        </h3>
        <div className="space-y-1">
          {sortedParticipants.map((p, index) => (
            <div
              key={p.id}
              className="flex items-center justify-between rounded-md bg-white px-2.5 py-2 text-sm shadow-sm"
            >
              <span className="flex items-center gap-2">
                <span className="w-5 text-center font-mono text-xs text-gray-400">
                  #{index + 1}
                </span>
                <span
                  className="inline-block h-2.5 w-2.5 rounded-full"
                  style={{ backgroundColor: p.avatarColor }}
                />
                <span>{p.displayName}</span>
                {p.isOwner && (
                  <span className="rounded bg-amber-100 px-1 text-[10px] font-semibold uppercase text-amber-700">
                    Host
                  </span>
                )}
              </span>
              <span className="font-mono text-sm font-semibold tabular-nums">{p.totalScore}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Chat allowed hint */}
      <div className="flex items-center gap-1.5 text-xs text-gray-400">
        <MessageSquare className="h-3 w-3" />
        <span>Chat is available while spectating</span>
      </div>
    </div>
  );
}
