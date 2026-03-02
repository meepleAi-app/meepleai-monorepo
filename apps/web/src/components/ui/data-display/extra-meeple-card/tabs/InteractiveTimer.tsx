'use client';

/**
 * InteractiveTimer - Circular countdown timer with per-player support
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
 */

import React from 'react';

import { Timer, Play, Pause, RotateCcw, AlertTriangle } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { TimerState, TimerActions, TimerStatus } from '../types';

interface InteractiveTimerProps {
  state?: TimerState;
  actions?: TimerActions;
  /** Player names for per-player display (keyed by playerId) */
  playerNames?: Record<string, string>;
}

/** Format seconds to mm:ss or hh:mm:ss */
function formatTime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  const pad = (n: number) => n.toString().padStart(2, '0');
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
}

/** Timer status colors */
const STATUS_STYLES: Record<TimerStatus, { ring: string; text: string; bg: string }> = {
  idle: { ring: 'stroke-slate-300', text: 'text-slate-500', bg: 'bg-slate-50' },
  running: { ring: 'stroke-indigo-500', text: 'text-indigo-700', bg: 'bg-indigo-50' },
  paused: { ring: 'stroke-amber-400', text: 'text-amber-700', bg: 'bg-amber-50' },
  warning: { ring: 'stroke-orange-500', text: 'text-orange-700', bg: 'bg-orange-50' },
  expired: { ring: 'stroke-red-500', text: 'text-red-700', bg: 'bg-red-50' },
};

/** Circular progress ring */
function TimerRing({
  remainingSeconds,
  totalSeconds,
  status,
  size = 120,
}: {
  remainingSeconds: number;
  totalSeconds: number;
  status: TimerStatus;
  size?: number;
}) {
  const strokeWidth = 6;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const progress = totalSeconds > 0 ? remainingSeconds / totalSeconds : 0;
  const dashOffset = circumference * (1 - progress);
  const styles = STATUS_STYLES[status];

  return (
    <div className="relative" style={{ width: size, height: size }}>
      <svg aria-hidden="true" className="transform -rotate-90" width={size} height={size}>
        {/* Background ring */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          className="text-slate-200"
        />
        {/* Progress ring */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={dashOffset}
          className={cn(styles.ring, 'transition-[stroke-dashoffset] duration-1000 ease-linear')}
        />
      </svg>
      {/* Center content */}
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span
          className={cn('font-mono text-xl font-bold', styles.text)}
          data-testid="timer-display"
        >
          {formatTime(remainingSeconds)}
        </span>
        {status === 'expired' && <AlertTriangle className="h-4 w-4 text-red-500 mt-0.5" />}
        {status === 'warning' && (
          <span className="font-nunito text-[10px] text-orange-500 animate-pulse">Low time!</span>
        )}
      </div>
    </div>
  );
}

/** Per-player timer row */
function PlayerTimerRow({
  playerId,
  playerName,
  remainingSeconds,
  status,
  isActive,
}: {
  playerId: string;
  playerName: string;
  remainingSeconds: number;
  status: TimerStatus;
  isActive: boolean;
}) {
  const styles = STATUS_STYLES[status];

  return (
    <div
      className={cn(
        'flex items-center justify-between rounded-lg px-3 py-2 transition-colors',
        isActive ? styles.bg : 'bg-white/40'
      )}
      data-testid={`player-timer-${playerId}`}
    >
      <div className="flex items-center gap-2">
        {isActive && <span className="h-2 w-2 rounded-full bg-green-500 animate-pulse" />}
        <span
          className={cn(
            'font-nunito text-xs font-medium',
            isActive ? 'text-slate-800 font-semibold' : 'text-slate-500'
          )}
        >
          {playerName}
        </span>
      </div>
      <span
        className={cn(
          'font-mono text-sm font-bold tabular-nums',
          isActive ? styles.text : 'text-slate-400'
        )}
      >
        {formatTime(remainingSeconds)}
      </span>
    </div>
  );
}

export function InteractiveTimer({ state, actions, playerNames }: InteractiveTimerProps) {
  if (!state) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-slate-400">
        <Timer className="h-8 w-8 mb-2 opacity-50" />
        <p className="font-nunito text-sm">No timer active</p>
      </div>
    );
  }

  const styles = STATUS_STYLES[state.status];

  return (
    <div className="space-y-3" data-testid="interactive-timer">
      {/* Timer header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <Timer className="h-3.5 w-3.5 text-indigo-500" />
          <span className="font-nunito text-xs font-bold text-slate-700">{state.toolName}</span>
          <span className="font-nunito text-[10px] text-slate-400">{state.timerType}</span>
        </div>
        <span
          className={cn(
            'rounded-full px-2 py-0.5 font-nunito text-[10px] font-semibold capitalize',
            styles.bg,
            styles.text
          )}
          data-testid="timer-status-badge"
        >
          {state.status}
        </span>
      </div>

      {/* Main timer ring */}
      <div className="flex justify-center">
        <TimerRing
          remainingSeconds={state.remainingSeconds}
          totalSeconds={state.totalSeconds}
          status={state.status}
        />
      </div>

      {/* Control buttons */}
      <div className="flex justify-center gap-2">
        {state.status === 'idle' && actions?.onStart && (
          <button
            onClick={actions.onStart}
            className="flex items-center gap-1 rounded-full bg-indigo-500 px-4 py-1.5 font-nunito text-xs font-medium text-white hover:bg-indigo-600 transition-colors"
            data-testid="timer-action-start"
          >
            <Play className="h-3 w-3" />
            Start
          </button>
        )}
        {state.status === 'paused' && (actions?.onResume ?? actions?.onStart) && (
          <button
            onClick={actions?.onResume ?? actions?.onStart}
            className="flex items-center gap-1 rounded-full bg-indigo-500 px-4 py-1.5 font-nunito text-xs font-medium text-white hover:bg-indigo-600 transition-colors"
            data-testid="timer-action-start"
          >
            <Play className="h-3 w-3" />
            Resume
          </button>
        )}
        {(state.status === 'running' || state.status === 'warning') && actions?.onPause && (
          <button
            onClick={actions.onPause}
            className={cn(
              'flex items-center gap-1 rounded-full px-4 py-1.5 font-nunito text-xs font-medium transition-colors',
              state.status === 'warning'
                ? 'bg-orange-100 text-orange-700 hover:bg-orange-200'
                : 'bg-amber-100 text-amber-700 hover:bg-amber-200'
            )}
            data-testid="timer-action-pause"
          >
            <Pause className="h-3 w-3" />
            Pause
          </button>
        )}
        {actions?.onReset && state.status !== 'idle' && (
          <button
            onClick={actions.onReset}
            className="flex items-center gap-1 rounded-full bg-slate-100 px-4 py-1.5 font-nunito text-xs font-medium text-slate-600 hover:bg-slate-200 transition-colors"
            data-testid="timer-action-reset"
          >
            <RotateCcw className="h-3 w-3" />
            Reset
          </button>
        )}
      </div>

      {/* Per-player timers */}
      {state.isPerPlayer && state.playerTimers && (
        <div>
          <h4 className="mb-1.5 font-nunito text-[10px] font-semibold text-slate-500 uppercase tracking-wider">
            Player Timers
          </h4>
          <div className="space-y-1">
            {Object.entries(state.playerTimers).map(([playerId, timer]) => (
              <PlayerTimerRow
                key={playerId}
                playerId={playerId}
                playerName={playerNames?.[playerId] ?? playerId}
                remainingSeconds={timer.remainingSeconds}
                status={timer.status}
                isActive={state.activePlayerId === playerId}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
