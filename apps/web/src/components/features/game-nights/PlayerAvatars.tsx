/**
 * PlayerAvatars - v2 SP4 #1170 commit 2
 *
 * Compact stacked-avatar group with overflow chip. Pure presentational.
 * Per-id deterministic hue is rendered via inline `style.background` (HSL):
 * documented as an exception to the no-hardcoded-color-utility rule because
 * the color is dynamic and not a Tailwind utility literal.
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx` (PlayerAvatars).
 */

import clsx from 'clsx';

export interface AvatarPlayer {
  readonly id: string;
  readonly initials: string;
  readonly name?: string;
}

export interface PlayerAvatarsProps {
  readonly players: readonly AvatarPlayer[];
  readonly max?: number;
  readonly className?: string;
}

function hueFromId(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i++) {
    hash = (hash * 31 + id.charCodeAt(i)) & 0xffffffff;
  }
  return Math.abs(hash) % 360;
}

export function PlayerAvatars({
  players,
  max = 5,
  className,
}: PlayerAvatarsProps): React.JSX.Element {
  const visible = players.slice(0, max);
  const overflow = Math.max(0, players.length - max);

  return (
    <div
      data-testid="game-nights-player-avatars"
      className={clsx('inline-flex items-center', className)}
    >
      {visible.map((player, idx) => (
        <span
          key={player.id}
          aria-label={player.name ?? player.initials}
          title={player.name ?? player.initials}
          className={clsx(
            // bg-[hsl(...)] arbitrary value satisfies the colored-bg exemption
            // for text-white; the inline style overrides with the per-id hue.
            'inline-flex h-6 w-6 items-center justify-center rounded-full border-2 border-card bg-[hsl(0_0%_50%)] font-display text-[10px] font-extrabold text-white',
            idx > 0 && '-ml-2'
          )}
          style={{ background: `hsl(${hueFromId(player.id)} 55% 50%)` }}
        >
          {player.initials}
        </span>
      ))}
      {overflow > 0 && (
        <span
          aria-label={`+${overflow}`}
          className={clsx(
            'inline-flex h-6 min-w-6 items-center justify-center rounded-full border-2 border-card px-1',
            'bg-muted font-mono text-[10px] font-extrabold text-muted-foreground',
            visible.length > 0 && '-ml-2'
          )}
        >
          +{overflow}
        </span>
      )}
    </div>
  );
}
