'use client';

/**
 * PlayerAvatar — Circular avatar showing player initial with their color.
 */

import { cn } from '@/lib/utils';

export interface PlayerAvatarProps {
  name: string;
  color: string;
  active?: boolean;
  size?: 'sm' | 'md';
  onClick?: () => void;
}

const SIZE_MAP = {
  sm: 'h-7 w-7 text-xs',
  md: 'h-9 w-9 text-sm',
} as const;

export function PlayerAvatar({ name, color, active, size = 'md', onClick }: PlayerAvatarProps) {
  const initial = name.charAt(0).toUpperCase();

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      className={cn(
        'flex shrink-0 items-center justify-center rounded-full font-bold text-white transition-shadow',
        SIZE_MAP[size],
        active && 'ring-2 ring-white ring-offset-2',
        onClick ? 'cursor-pointer' : 'cursor-default'
      )}
      style={{ backgroundColor: color }}
      aria-label={`${name}${active ? ' (turno attivo)' : ''}`}
      data-testid={`player-avatar-${name}`}
    >
      {initial}
    </button>
  );
}
