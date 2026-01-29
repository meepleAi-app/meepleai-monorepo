'use client';

import React, { useState } from 'react';
import { Copy, Check, MoreVertical, Pause, Play, Share2, Flag } from 'lucide-react';
import { Session } from './types';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';

interface SessionHeaderProps {
  session: Session;
  onPause?: () => void;
  onFinalize?: () => void;
  onShare?: () => void;
}

export function SessionHeader({ session, onPause, onFinalize, onShare }: SessionHeaderProps) {
  const [copied, setCopied] = useState(false);

  const copySessionCode = async () => {
    await navigator.clipboard.writeText(session.sessionCode);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const statusColors = {
    Active: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20',
    Paused: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
    Finalized: 'bg-slate-500/10 text-slate-600 dark:text-slate-400 border-slate-500/20',
  };

  return (
    <header className="sticky top-0 z-50 border-b border-amber-900/20 bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 backdrop-blur-xl">
      {/* Subtle wood grain texture overlay */}
      <div className="absolute inset-0 opacity-[0.03] dark:opacity-[0.05] bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZmlsdGVyIGlkPSJub2lzZSI+PGZlVHVyYnVsZW5jZSB0eXBlPSJmcmFjdGFsTm9pc2UiIGJhc2VGcmVxdWVuY3k9IjAuOCIgbnVtT2N0YXZlcz0iNCIgc3RpdGNoVGlsZXM9InN0aXRjaCIvPjwvZmlsdGVyPjxyZWN0IHdpZHRoPSIxMDAlIiBoZWlnaHQ9IjEwMCUiIGZpbHRlcj0idXJsKCNub2lzZSkiIG9wYWNpdHk9IjAuNSIvPjwvc3ZnPg==')] pointer-events-none" />

      <div className="relative px-4 py-3 sm:px-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          {/* Left: Session Info */}
          <div className="flex items-center gap-3">
            {session.gameIcon && (
              <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-amber-600 to-orange-700 text-2xl shadow-lg ring-2 ring-amber-900/20 dark:from-amber-500 dark:to-orange-600">
                {session.gameIcon}
              </div>
            )}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 flex-wrap">
                <h1 className="font-bold text-lg sm:text-xl text-slate-900 dark:text-amber-50 truncate tracking-tight">
                  {session.sessionType === 'GameSpecific' && session.gameName
                    ? session.gameName
                    : 'Game Session'}
                </h1>
                <Badge
                  variant="outline"
                  className={`text-xs font-medium border ${statusColors[session.status]} backdrop-blur-sm`}
                >
                  {session.status}
                </Badge>
              </div>
              <div className="flex items-center gap-3 mt-1 text-xs text-slate-600 dark:text-slate-400">
                <time className="font-medium tabular-nums">
                  {session.sessionDate.toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric',
                    hour: '2-digit',
                    minute: '2-digit'
                  })}
                </time>
                <span className="flex items-center gap-1">
                  <span className="inline-block h-1 w-1 rounded-full bg-amber-600 dark:bg-amber-400" />
                  {session.participantCount} {session.participantCount === 1 ? 'player' : 'players'}
                </span>
              </div>
            </div>
          </div>

          {/* Right: Session Code & Actions */}
          <div className="flex items-center gap-2 sm:gap-3">
            {/* Session Code */}
            <button
              onClick={copySessionCode}
              className="group relative flex items-center gap-2 rounded-lg border border-amber-900/20 bg-white/80 dark:bg-slate-800/80 px-3 py-2 backdrop-blur-sm transition-all hover:border-amber-600/40 hover:bg-white dark:hover:bg-slate-800 hover:shadow-md active:scale-95"
              aria-label="Copy session code"
            >
              <span className="font-mono text-sm font-bold tracking-wider text-amber-900 dark:text-amber-400">
                {session.sessionCode}
              </span>
              {copied ? (
                <Check className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
              ) : (
                <Copy className="h-4 w-4 text-slate-400 transition-colors group-hover:text-amber-600 dark:group-hover:text-amber-400" />
              )}
              {copied && (
                <span className="absolute -top-8 left-1/2 -translate-x-1/2 rounded bg-slate-900 dark:bg-slate-100 px-2 py-1 text-xs text-white dark:text-slate-900 shadow-lg animate-in fade-in slide-in-from-bottom-2">
                  Copied!
                </span>
              )}
            </button>

            {/* Actions Menu */}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="outline"
                  size="icon"
                  className="h-9 w-9 border-amber-900/20 bg-white/80 dark:bg-slate-800/80 hover:bg-white dark:hover:bg-slate-800 hover:border-amber-600/40 active:scale-95 transition-all"
                >
                  <MoreVertical className="h-4 w-4 text-slate-600 dark:text-slate-400" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                align="end"
                className="w-48 bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border-amber-900/20"
              >
                {session.status === 'Active' && onPause && (
                  <DropdownMenuItem onClick={onPause} className="gap-2">
                    <Pause className="h-4 w-4" />
                    Pause Session
                  </DropdownMenuItem>
                )}
                {session.status === 'Paused' && onPause && (
                  <DropdownMenuItem onClick={onPause} className="gap-2">
                    <Play className="h-4 w-4" />
                    Resume Session
                  </DropdownMenuItem>
                )}
                {onShare && (
                  <DropdownMenuItem onClick={onShare} className="gap-2">
                    <Share2 className="h-4 w-4" />
                    Share Results
                  </DropdownMenuItem>
                )}
                {session.status === 'Active' && onFinalize && (
                  <DropdownMenuItem onClick={onFinalize} className="gap-2 text-amber-700 dark:text-amber-400">
                    <Flag className="h-4 w-4" />
                    Finalize Session
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </div>
    </header>
  );
}
