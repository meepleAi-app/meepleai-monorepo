'use client';

import { hashToHue, extractInitials } from '@/lib/games/cover-utils';
import { cn } from '@/lib/utils';

/**
 * Locally-rendered, deterministic game cover placeholder.
 *
 * Renders a colored gradient (hue derived from `gameId`) with the game's
 * initials centered on top, plus a faint meeple silhouette decoration. The
 * output is fully self-contained — no network requests, no third-party assets,
 * SSR-safe.
 *
 * Used by {@link Cover} whenever the upstream `imageUrl` is missing, blocked,
 * or fails to load. See issue #1822 (umbrella #1821) for the BGG-elimination
 * context.
 */
export interface GameCoverPlaceholderProps {
  /**
   * Stable id used to derive the hue. Any string — game id, share token, etc.
   * Same id → same color across sessions.
   */
  gameId: string;
  /** Title used to extract 1-3 character initials shown in the center. */
  title: string;
  /** Optional className passed to the outer container (e.g. aspect ratio). */
  className?: string;
}

export function GameCoverPlaceholder({ gameId, title, className }: GameCoverPlaceholderProps) {
  const hue = hashToHue(gameId || title || 'fallback');
  const initials = extractInitials(title);
  const secondaryHue = (hue + 35) % 360;

  return (
    <div
      data-testid="game-cover-placeholder"
      data-game-id={gameId}
      aria-hidden="true"
      className={cn(
        'relative flex h-full w-full items-center justify-center overflow-hidden text-white',
        className
      )}
      style={{
        background: `linear-gradient(135deg, hsl(${hue} 55% 32%) 0%, hsl(${secondaryHue} 65% 22%) 100%)`,
      }}
    >
      {/* Decorative meeple silhouette — subtle, behind the initials */}
      <svg
        viewBox="0 0 64 64"
        aria-hidden="true"
        className="absolute h-[65%] w-[65%] opacity-[0.12]"
        fill="currentColor"
        xmlns="http://www.w3.org/2000/svg"
      >
        {/* Stylized meeple silhouette: head + body + legs */}
        <circle cx="32" cy="14" r="9" />
        <path d="M14 60 L14 38 Q14 28 22 28 L42 28 Q50 28 50 38 L50 60 L40 60 L37 44 L32 60 L27 60 L24 44 L21 60 Z" />
      </svg>

      {/* Initials — main visual anchor */}
      <span
        className="relative z-10 select-none font-quicksand font-bold tracking-wider"
        style={{
          fontSize: 'clamp(1.5rem, 14cqi, 4rem)',
          textShadow: '0 1px 2px rgba(0,0,0,0.35)',
        }}
      >
        {initials}
      </span>
    </div>
  );
}
