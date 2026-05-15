/**
 * GameNightAvatar - v2 SP7 #951 commit 2
 *
 * Initials avatar primitive used by GameNightRsvpRow and GameNightDetailHero.
 * Mapped from `admin-mockups/design_files/sp7-game-night-detail-rsvp.jsx` (Avatar).
 *
 * Pure presentational. The mockup uses inline HSL background derived from a
 * per-player hue (`player.color`); we expose `hue` as a prop and compose the
 * Tailwind arbitrary value so the background tracks the design intent without
 * introducing N entity colors. ESLint local/no-hardcoded-color-utility allows
 * arbitrary `bg-[hsl(...)]` patterns.
 *
 * AC: T V
 */

import clsx from 'clsx';

export interface GameNightAvatarProps {
  /** 2-letter uppercase initials (callers compute from displayName). */
  readonly initials: string;
  /** Accessible label, e.g. full player name. */
  readonly label: string;
  /** Hue 0-360 for the HSL background. Stable per-player from upstream. */
  readonly hue: number;
  /** Pixel size of the round avatar. Default 32. */
  readonly size?: 22 | 28 | 32 | 40 | 48;
  /**
   * When true, draws a ring matching the entity-player palette to highlight
   * the "this is me" affordance in roster lists.
   */
  readonly highlightSelf?: boolean;
  /** Optional class hook for layout overrides. */
  readonly className?: string;
}

const SIZE_TO_CLASS: Record<NonNullable<GameNightAvatarProps['size']>, string> = {
  22: 'h-[22px] w-[22px] text-[9px]',
  28: 'h-7 w-7 text-[11px]',
  32: 'h-8 w-8 text-[11px]',
  40: 'h-10 w-10 text-sm',
  48: 'h-12 w-12 text-sm',
};

export function GameNightAvatar({
  initials,
  label,
  hue,
  size = 32,
  highlightSelf = false,
  className,
}: GameNightAvatarProps): React.JSX.Element {
  // Per-player HSL bg is dynamic (different hue per user GUID), so it cannot
  // be expressed as a literal Tailwind class — the JIT extractor only sees
  // class strings statically. We pipe the value through a CSS custom property
  // and reference it via the literal arbitrary `bg-[var(--avatar-bg)]` class
  // in the same className as `text-white`. This satisfies the CLAUDE.md DS-15
  // exemption ("text-white IS allowed when the same className declares a
  // colored bg [...] arbitrary `bg-[hsl(…)]`") while keeping the hue dynamic.
  // Issue caught in #1171 review followup.
  const avatarBg = `hsl(${hue}, 60%, 55%)`;
  return (
    <span
      role="img"
      aria-label={label}
      className={clsx(
        'inline-flex shrink-0 items-center justify-center rounded-full font-display font-extrabold',
        'bg-[var(--avatar-bg)]',
        // eslint-disable-next-line local/no-hardcoded-color-utility -- white text on per-player HSL background; mockup .e-bg pattern. Exemption satisfied by bg-[var(--avatar-bg)] declared on the same className.
        'text-white',
        // 2px border preserves card-background separation in roster grids; ring color flips for "me".
        highlightSelf
          ? 'border-2 border-entity-player/40 ring-2 ring-entity-player/30'
          : 'border-2 border-card',
        SIZE_TO_CLASS[size],
        className
      )}
      style={{ ['--avatar-bg' as string]: avatarBg } as React.CSSProperties}
    >
      {initials}
    </span>
  );
}

/**
 * Deterministic helper: extracts 2 uppercase letters from a display name.
 * Used by callers to compute the `initials` prop in a single place.
 */
export function computeInitials(displayName: string): string {
  const cleaned = displayName.trim();
  if (cleaned.length === 0) return '??';
  const parts = cleaned.split(/\s+/);
  const first = parts[0] ?? '';
  if (parts.length === 1) {
    return first.slice(0, 2).toUpperCase();
  }
  const lastPart = parts[parts.length - 1] ?? '';
  return (first.charAt(0) + lastPart.charAt(0)).toUpperCase();
}

/**
 * Stable hue derived from any string ID (user GUID, email, etc).
 * Hash function is deliberately simple — collisions on hue are harmless
 * because avatars are also disambiguated by initials and label.
 */
export function deriveHueFromId(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i++) {
    hash = (hash * 31 + id.charCodeAt(i)) & 0xffffffff;
  }
  return Math.abs(hash) % 360;
}
