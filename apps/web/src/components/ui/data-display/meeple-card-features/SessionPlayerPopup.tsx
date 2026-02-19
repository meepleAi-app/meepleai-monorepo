/**
 * SessionPlayerPopup - Mini MeepleCard Player Popup
 * Issue #4751 - MeepleCard Session Front
 *
 * Shows mini player cards on hover over the Players navigation link.
 * Features:
 * - Player color as background accent
 * - Avatar, name, score, role (Host/Player)
 * - Click navigates to player page
 * - Popover positioning
 */

'use client';

import React, { useCallback, useEffect, useRef, useState } from 'react';

import { Crown, Eye, User } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

import {
  PLAYER_COLOR_MAP,
  type SessionPlayerInfo,
} from './session-types';

// ============================================================================
// Types
// ============================================================================

export interface SessionPlayerPopupProps {
  players: SessionPlayerInfo[];
  /** Build player page href */
  buildPlayerHref?: (playerId: string) => string;
  children: React.ReactNode;
  className?: string;
}

// ============================================================================
// Role icon mapping
// ============================================================================

const roleIcons = {
  host: Crown,
  player: User,
  spectator: Eye,
} as const;

const roleLabels = {
  host: 'Host',
  player: 'Player',
  spectator: 'Spectator',
} as const;

// ============================================================================
// Component
// ============================================================================

export function SessionPlayerPopup({
  players,
  buildPlayerHref,
  children,
  className,
}: SessionPlayerPopupProps) {
  const [open, setOpen] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout>>(null);

  const handleEnter = useCallback(() => {
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
    setOpen(true);
  }, []);

  const handleLeave = useCallback(() => {
    timeoutRef.current = setTimeout(() => setOpen(false), 150);
  }, []);

  // Clear timeout on unmount to prevent memory leak
  useEffect(() => {
    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
  }, []);

  return (
    <div
      className={cn('relative inline-block', className)}
      onMouseEnter={handleEnter}
      onMouseLeave={handleLeave}
      onFocus={handleEnter}
      onBlur={handleLeave}
      data-testid="session-player-popup-trigger"
    >
      {children}

      {/* Popup */}
      {open && players.length > 0 && (
        <div
          className={cn(
            'absolute bottom-full left-1/2 -translate-x-1/2 mb-2 z-50',
            'bg-card/95 backdrop-blur-md rounded-xl shadow-lg border border-border',
            'p-2 min-w-[180px] max-w-[240px]',
            'animate-in fade-in-0 zoom-in-95 slide-in-from-bottom-2 duration-200',
          )}
          onMouseEnter={handleEnter}
          onMouseLeave={handleLeave}
          data-testid="session-player-popup"
        >
          <div className="flex flex-col gap-1.5">
            {players.map((player) => {
              // eslint-disable-next-line security/detect-object-injection
              const colorHsl = PLAYER_COLOR_MAP[player.color];
              // eslint-disable-next-line security/detect-object-injection
              const RoleIcon = roleIcons[player.role];
              // eslint-disable-next-line security/detect-object-injection
              const roleLabel = roleLabels[player.role];
              const href = buildPlayerHref?.(player.userId ?? player.id);

              const content = (
                <div
                  className={cn(
                    'flex items-center gap-2 px-2.5 py-1.5 rounded-lg',
                    'transition-colors duration-150',
                    href && 'hover:bg-muted/60 cursor-pointer',
                  )}
                  style={{
                    borderLeft: `3px solid hsl(${colorHsl})`,
                  }}
                  data-testid={`player-popup-${player.id}`}
                >
                  {/* Avatar or initial */}
                  {player.avatarUrl ? (
                    <img
                      src={player.avatarUrl}
                      alt={player.displayName}
                      className="w-6 h-6 rounded-full object-cover flex-shrink-0"
                    />
                  ) : (
                    <span
                      className="flex items-center justify-center w-6 h-6 rounded-full text-[10px] font-bold text-white flex-shrink-0"
                      style={{ backgroundColor: `hsl(${colorHsl})` }}
                    >
                      {player.displayName.charAt(0).toUpperCase()}
                    </span>
                  )}

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <p className="text-xs font-semibold text-card-foreground truncate">
                      {player.displayName}
                    </p>
                    <p className="flex items-center gap-1 text-[10px] text-muted-foreground">
                      <RoleIcon className="w-2.5 h-2.5" />
                      {roleLabel}
                      <span className="mx-0.5">•</span>
                      <span className="font-medium tabular-nums">{player.totalScore} pts</span>
                    </p>
                  </div>
                </div>
              );

              if (href) {
                return (
                  <Link
                    key={player.id}
                    href={href}
                    onClick={(e) => e.stopPropagation()}
                    className="no-underline"
                  >
                    {content}
                  </Link>
                );
              }

              return <React.Fragment key={player.id}>{content}</React.Fragment>;
            })}
          </div>

          {/* Arrow indicator */}
          <div className="absolute left-1/2 -translate-x-1/2 -bottom-1.5 w-3 h-3 rotate-45 bg-card border-r border-b border-border" />
        </div>
      )}
    </div>
  );
}
