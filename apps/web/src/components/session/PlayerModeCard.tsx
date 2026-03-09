/**
 * PlayerModeCard - Full interactive session card for Players/Hosts
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 *
 * Shows all interactive controls: score editing, dice, cards, ready button.
 * Wraps ExtraMeepleCard with full action capabilities.
 */

'use client';

import { useState, useCallback } from 'react';

import { Check, Dice5, Pause, Play, Trophy } from 'lucide-react';

import type { Participant, ScoreEntry } from '@/components/session/types';

interface PlayerModeCardProps {
  sessionId: string;
  currentParticipant: Participant;
  participants: Participant[];
  scores: ScoreEntry[];
  sessionStatus: 'Active' | 'Paused' | 'Finalized';
  isHost: boolean;
  onUpdateScore?: (participantId: string, scoreValue: number) => void;
  onMarkReady?: () => void;
  onRollDice?: (formula: string) => void;
  onPauseSession?: () => void;
  onResumeSession?: () => void;
  className?: string;
}

export function PlayerModeCard({
  sessionId: _sessionId,
  currentParticipant,
  participants,
  scores: _scores,
  sessionStatus,
  isHost,
  onUpdateScore,
  onMarkReady,
  onRollDice,
  onPauseSession,
  onResumeSession,
  className = '',
}: PlayerModeCardProps) {
  const [scoreInput, setScoreInput] = useState('');
  const [isReady, setIsReady] = useState(false);

  const handleSubmitScore = useCallback(() => {
    const value = parseInt(scoreInput, 10);
    if (!isNaN(value) && isFinite(value) && value >= 0 && onUpdateScore) {
      onUpdateScore(currentParticipant.id, value);
      setScoreInput('');
    }
  }, [scoreInput, currentParticipant.id, onUpdateScore]);

  const handleReady = useCallback(() => {
    setIsReady(true);
    onMarkReady?.();
  }, [onMarkReady]);

  const isActive = sessionStatus === 'Active';
  const isFinalized = sessionStatus === 'Finalized';

  return (
    <div className={`space-y-4 ${className}`} data-testid="player-mode-card">
      {/* Quick Actions Bar */}
      <div className="flex flex-wrap items-center gap-2">
        {/* Score Input */}
        {isActive && (
          <div className="flex items-center gap-1.5">
            <input
              type="number"
              min="0"
              value={scoreInput}
              onChange={e => setScoreInput(e.target.value)}
              placeholder="Score"
              className="w-20 rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:border-indigo-500 focus:outline-none"
              aria-label="Score value"
            />
            <button
              onClick={handleSubmitScore}
              disabled={!scoreInput || isFinalized}
              className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-indigo-700 disabled:opacity-50"
              aria-label="Submit score"
            >
              <Trophy className="h-4 w-4" />
            </button>
          </div>
        )}

        {/* Dice Roll */}
        {isActive && (
          <button
            onClick={() => onRollDice?.('1d6')}
            className="flex items-center gap-1 rounded-md bg-amber-100 px-3 py-1.5 text-sm font-medium text-amber-800 transition-colors hover:bg-amber-200"
            aria-label="Roll dice"
          >
            <Dice5 className="h-4 w-4" />
            Roll
          </button>
        )}

        {/* Ready Button */}
        {isActive && !isHost && (
          <button
            onClick={handleReady}
            disabled={isReady}
            className={`flex items-center gap-1 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
              isReady
                ? 'bg-emerald-100 text-emerald-700'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
            aria-label={isReady ? 'Ready' : 'Mark as ready'}
          >
            <Check className="h-4 w-4" />
            {isReady ? 'Ready' : 'Ready Up'}
          </button>
        )}

        {/* Host Controls */}
        {isHost && isActive && (
          <button
            onClick={onPauseSession}
            className="flex items-center gap-1 rounded-md bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-200"
            aria-label="Pause session"
          >
            <Pause className="h-4 w-4" />
            Pause
          </button>
        )}

        {isHost && sessionStatus === 'Paused' && (
          <button
            onClick={onResumeSession}
            className="flex items-center gap-1 rounded-md bg-emerald-100 px-3 py-1.5 text-sm font-medium text-emerald-700 transition-colors hover:bg-emerald-200"
            aria-label="Resume session"
          >
            <Play className="h-4 w-4" />
            Resume
          </button>
        )}
      </div>

      {/* Participants List */}
      <div className="space-y-1.5">
        <h3 className="text-xs font-semibold uppercase tracking-wider text-gray-500">
          Players ({participants.length})
        </h3>
        <div className="space-y-1">
          {participants.map(p => (
            <div
              key={p.id}
              className={`flex items-center justify-between rounded-md px-2.5 py-1.5 text-sm ${
                p.id === currentParticipant.id
                  ? 'bg-indigo-50 font-medium text-indigo-700'
                  : 'text-gray-600'
              }`}
            >
              <span className="flex items-center gap-1.5">
                <span
                  className="inline-block h-2.5 w-2.5 rounded-full"
                  style={{ backgroundColor: p.avatarColor }}
                />
                {p.displayName}
                {p.isOwner && (
                  <span className="rounded bg-amber-100 px-1 text-[10px] font-semibold uppercase text-amber-700">
                    Host
                  </span>
                )}
                {p.id === currentParticipant.id && (
                  <span className="text-[10px] text-gray-400">(you)</span>
                )}
              </span>
              <span className="font-mono text-xs tabular-nums">{p.totalScore}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
