'use client';

import React from 'react';
import { Crown, Loader2 } from 'lucide-react';
import { Participant } from './types';

interface ParticipantCardProps {
  participant: Participant;
  variant?: 'compact' | 'full';
}

export function ParticipantCard({ participant, variant = 'full' }: ParticipantCardProps) {
  const rankEmojis: Record<number, string> = {
    1: '🥇',
    2: '🥈',
    3: '🥉',
  };

  const getInitials = (name: string) => {
    const cleanName = name.replace(/\s*\(io\)\s*/i, '').trim();
    return cleanName
      .split(' ')
      .map(n => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  };

  if (variant === 'compact') {
    return (
      <div
        className={`group relative flex items-center gap-3 rounded-xl border p-3 transition-all ${
          participant.isCurrentUser
            ? 'border-amber-600/40 bg-gradient-to-br from-amber-50 to-orange-50 dark:from-amber-950/30 dark:to-orange-950/20 shadow-md shadow-amber-900/5'
            : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/50 hover:border-slate-300 dark:hover:border-slate-600'
        }`}
      >
        {/* Rank Badge - Top Left Corner */}
        {participant.rank && participant.rank <= 3 && (
          <div className="absolute -top-2 -left-2 text-2xl drop-shadow-lg animate-in zoom-in duration-500">
            {rankEmojis[participant.rank]}
          </div>
        )}

        {/* Avatar */}
        <div
          className="relative flex h-10 w-10 shrink-0 items-center justify-center rounded-lg font-bold text-white shadow-inner ring-1 ring-black/10 dark:ring-white/10"
          style={{
            background: `linear-gradient(135deg, ${participant.avatarColor} 0%, ${participant.avatarColor}dd 100%)`
          }}
        >
          <span className="text-sm tracking-wide">{getInitials(participant.displayName)}</span>

          {participant.isOwner && (
            <div className="absolute -top-1 -right-1 flex h-4 w-4 items-center justify-center rounded-full bg-amber-500 shadow-lg">
              <Crown className="h-2.5 w-2.5 text-white" />
            </div>
          )}
        </div>

        {/* Name & Score */}
        <div className="flex-1 min-w-0">
          <div className="flex items-baseline gap-1.5">
            <span className="truncate text-sm font-semibold text-slate-900 dark:text-slate-100">
              {participant.displayName}
            </span>
            {participant.isTyping && (
              <span className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400 animate-pulse">
                <Loader2 className="h-3 w-3 animate-spin" />
              </span>
            )}
          </div>
          <div className="mt-0.5 font-mono text-xs text-slate-500 dark:text-slate-400 tabular-nums">
            Score: {participant.totalScore.toLocaleString()}
          </div>
        </div>

        {/* Current Rank (if not top 3) */}
        {participant.rank && participant.rank > 3 && (
          <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-slate-100 dark:bg-slate-700 font-mono text-xs font-bold text-slate-600 dark:text-slate-400">
            #{participant.rank}
          </div>
        )}
      </div>
    );
  }

  // Full variant
  return (
    <div
      className={`group relative overflow-hidden rounded-2xl border transition-all ${
        participant.isCurrentUser
          ? 'border-amber-600/40 bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100 dark:from-amber-950/40 dark:via-orange-950/30 dark:to-amber-950/20 shadow-xl shadow-amber-900/10'
          : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/50 hover:shadow-lg'
      }`}
    >
      {/* Wood grain texture */}
      <div className="absolute inset-0 opacity-[0.02] dark:opacity-[0.03] bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZmlsdGVyIGlkPSJub2lzZSI+PGZlVHVyYnVsZW5jZSB0eXBlPSJmcmFjdGFsTm9pc2UiIGJhc2VGcmVxdWVuY3k9IjAuOCIgbnVtT2N0YXZlcz0iNCIgc3RpdGNoVGlsZXM9InN0aXRjaCIvPjwvZmlsdGVyPjxyZWN0IHdpZHRoPSIxMDAlIiBoZWlnaHQ9IjEwMCUiIGZpbHRlcj0idXJsKCNub2lzZSkiIG9wYWNpdHk9IjAuNSIvPjwvc3ZnPg==')] pointer-events-none" />

      {/* Rank Badge - Floating */}
      {participant.rank && participant.rank <= 3 && (
        <div className="absolute -top-3 -right-3 text-5xl drop-shadow-2xl animate-in zoom-in duration-700 slide-in-from-top-4">
          {rankEmojis[participant.rank]}
        </div>
      )}

      <div className="relative p-5 sm:p-6">
        <div className="flex items-start gap-4">
          {/* Avatar */}
          <div
            className="relative flex h-16 w-16 shrink-0 items-center justify-center rounded-xl font-bold text-white shadow-lg ring-2 ring-black/10 dark:ring-white/10 transition-transform group-hover:scale-105"
            style={{
              background: `linear-gradient(135deg, ${participant.avatarColor} 0%, ${participant.avatarColor}cc 100%)`
            }}
          >
            <span className="text-xl tracking-wide">{getInitials(participant.displayName)}</span>

            {participant.isOwner && (
              <div className="absolute -top-2 -right-2 flex h-6 w-6 items-center justify-center rounded-full bg-gradient-to-br from-amber-400 to-amber-600 shadow-lg ring-2 ring-amber-100 dark:ring-amber-900">
                <Crown className="h-3.5 w-3.5 text-white drop-shadow" />
              </div>
            )}
          </div>

          {/* Info */}
          <div className="flex-1 min-w-0">
            <div className="flex items-baseline gap-2 flex-wrap">
              <h3 className="font-bold text-lg text-slate-900 dark:text-slate-100 tracking-tight">
                {participant.displayName}
              </h3>
              {participant.isTyping && (
                <span className="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400 animate-pulse">
                  <Loader2 className="h-3 w-3 animate-spin" />
                  <span className="font-medium">typing...</span>
                </span>
              )}
            </div>

            {/* Score Display - Embossed Number Style */}
            <div className="mt-3 flex items-baseline gap-2">
              <div className="relative">
                {/* Shadow layer for embossed effect */}
                <div
                  className="absolute inset-0 translate-y-0.5 font-mono text-4xl font-black tabular-nums text-slate-900/10 dark:text-black/20"
                  aria-hidden="true"
                >
                  {participant.totalScore.toLocaleString()}
                </div>
                {/* Main number */}
                <div className="relative font-mono text-4xl font-black tabular-nums text-slate-900 dark:text-amber-50">
                  {participant.totalScore.toLocaleString()}
                </div>
              </div>
              <span className="text-sm font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">
                pts
              </span>
            </div>

            {/* Rank indicator */}
            {participant.rank && (
              <div className="mt-2 inline-flex items-center gap-1.5 rounded-full bg-slate-100 dark:bg-slate-700/50 px-3 py-1 text-xs font-bold text-slate-700 dark:text-slate-300">
                <span className="text-amber-600 dark:text-amber-400">#</span>
                <span className="tabular-nums">{participant.rank}</span>
                <span className="text-slate-400 dark:text-slate-500">rank</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Typing indicator pulse effect */}
      {participant.isTyping && (
        <div className="absolute inset-0 rounded-2xl ring-2 ring-amber-400 dark:ring-amber-500 animate-pulse pointer-events-none" />
      )}
    </div>
  );
}
