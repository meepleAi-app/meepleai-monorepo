'use client';

/**
 * OverviewTab - Session overview with players, status, and actions
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 */

import React from 'react';

import { Calendar, Clock, Hash, Pause, Play, RotateCcw, Save, UserPlus, Users } from 'lucide-react';

import { cn } from '@/lib/utils';

import { PLAYER_COLOR_BG } from '../../meeple-card-features/session-types';

import type {
  OverviewTabData,
  SessionStatus,
  SessionActionHandlers,
  SessionPlayerInfo,
} from '../types';

// ============================================================================
// Sub-components
// ============================================================================

function PlayerAvatar({ player }: { player: SessionPlayerInfo }) {
  const bgColor = PLAYER_COLOR_BG[player.color] ?? 'bg-slate-400';
  return (
    <div className="flex items-center gap-2.5 rounded-lg bg-white/60 p-2">
      <div
        className={cn(
          'flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold text-white',
          bgColor
        )}
      >
        {player.displayName.charAt(0).toUpperCase()}
      </div>
      <div className="min-w-0 flex-1">
        <p className="truncate font-nunito text-sm font-semibold text-slate-800">
          {player.displayName}
        </p>
        <p className="text-xs text-slate-500 capitalize">{player.role}</p>
      </div>
      {player.totalScore !== undefined && (
        <span className="font-mono text-sm font-bold text-indigo-600">{player.totalScore}</span>
      )}
    </div>
  );
}

function StatItem({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-center gap-2 rounded-lg bg-white/50 px-3 py-2">
      <Icon className="h-4 w-4 text-indigo-400 flex-shrink-0" />
      <div className="min-w-0">
        <p className="text-[10px] uppercase tracking-wider text-slate-400 font-nunito">{label}</p>
        <p className="text-sm font-semibold text-slate-700 font-nunito truncate">{value}</p>
      </div>
    </div>
  );
}

function ActionButton({
  icon: Icon,
  label,
  onClick,
  variant = 'default',
}: {
  icon: React.ElementType;
  label: string;
  onClick?: () => void;
  variant?: 'default' | 'primary' | 'danger';
}) {
  if (!onClick) return null;
  return (
    <button
      onClick={onClick}
      className={cn(
        'flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium font-nunito transition-colors',
        variant === 'primary' && 'bg-indigo-500 text-white hover:bg-indigo-600',
        variant === 'danger' && 'bg-red-50 text-red-600 hover:bg-red-100',
        variant === 'default' && 'bg-slate-100 text-slate-700 hover:bg-slate-200'
      )}
    >
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {label}
    </button>
  );
}

// ============================================================================
// Main Component
// ============================================================================

interface OverviewTabProps {
  data?: OverviewTabData;
  status: SessionStatus;
  actions?: SessionActionHandlers;
}

export function OverviewTab({ data, status, actions }: OverviewTabProps) {
  if (!data) {
    return (
      <div className="flex h-48 items-center justify-center text-sm text-slate-400 font-nunito">
        No session data available
      </div>
    );
  }

  const startDate = new Date(data.startedAt);
  const formattedDate = startDate.toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <div className="space-y-4">
      {/* Quick Stats */}
      <div className="grid grid-cols-2 gap-2">
        <StatItem icon={Calendar} label="Started" value={formattedDate} />
        {data.durationMinutes != null && (
          <StatItem
            icon={Clock}
            label="Duration"
            value={`${Math.floor(data.durationMinutes / 60)}h ${data.durationMinutes % 60}m`}
          />
        )}
        {data.sessionCode && <StatItem icon={Hash} label="Session Code" value={data.sessionCode} />}
        {data.currentRound != null && (
          <StatItem
            icon={RotateCcw}
            label="Round"
            value={
              data.totalRounds ? `${data.currentRound}/${data.totalRounds}` : `${data.currentRound}`
            }
          />
        )}
      </div>

      {/* Players */}
      <div>
        <div className="mb-2 flex items-center gap-1.5">
          <Users className="h-3.5 w-3.5 text-indigo-500" />
          <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 font-nunito">
            Players ({data.players.length})
          </h3>
        </div>
        <div className="space-y-1.5">
          {data.players.map(player => (
            <PlayerAvatar key={player.id} player={player} />
          ))}
        </div>
      </div>

      {/* Actions */}
      {actions && (
        <div className="flex flex-wrap gap-2 border-t border-slate-100 pt-3">
          {status === 'setup' && (
            <>
              <ActionButton icon={Play} label="Start" onClick={actions.onStart} variant="primary" />
              <ActionButton icon={UserPlus} label="Add Player" onClick={actions.onAddPlayer} />
            </>
          )}
          {status === 'inProgress' && (
            <>
              <ActionButton icon={Pause} label="Pause" onClick={actions.onPause} />
              <ActionButton icon={Save} label="Save" onClick={actions.onSave} />
            </>
          )}
          {status === 'paused' && (
            <ActionButton icon={Play} label="Resume" onClick={actions.onResume} variant="primary" />
          )}
          {status === 'completed' && (
            <>
              <ActionButton
                icon={RotateCcw}
                label="Rematch"
                onClick={actions.onRematch}
                variant="primary"
              />
              <ActionButton icon={Play} label="View Record" onClick={actions.onViewPlayRecord} />
            </>
          )}
        </div>
      )}
    </div>
  );
}
