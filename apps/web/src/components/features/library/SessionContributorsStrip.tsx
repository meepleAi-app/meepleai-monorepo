/* eslint-disable local/no-hardcoded-color-utility -- text-white on dynamic HSL avatar bg; mockup .e-bg pattern. DS-12 primitive — see sp3-shared-game-detail.jsx:660-690. */
'use client';

/**
 * SessionContributorsStrip (Issue #2036) — Mockup parity with
 * sp3-shared-game-detail.jsx:635-693.
 *
 * Avatar overlap stack of up to N registered users who participated in at
 * least one finalized session for the game, ordered by descending session
 * count. Renders nothing when the contributor list is empty so the strip
 * doesn't claim layout space on games with no recorded play.
 */

import type { ReactElement } from 'react';

import type { SessionContributorDto } from '@/lib/api/schemas';

export interface SessionContributorsStripProps {
  contributors: SessionContributorDto[];
  /** When true, slice visible avatars to 5 (mobile / dense layouts). */
  compact?: boolean;
  /** Cap on visible avatars before the overflow "+N" chip kicks in. Default 8 (mockup). */
  max?: number;
  className?: string;
}

/**
 * Deterministic hue [0, 360) derived from the UUID. Avoids name-clash
 * conventions (two "Mario Rossi" users still get distinct colours). The
 * mockup uses a curated palette per fake user — we map UUIDs to the full
 * 360° wheel so the production avatars stay distinguishable when names
 * collide.
 */
function hueFromUserId(userId: string): number {
  let hash = 0;
  for (let i = 0; i < userId.length; i += 1) {
    hash = (hash * 31 + userId.charCodeAt(i)) >>> 0;
  }
  return hash % 360;
}

export function SessionContributorsStrip({
  contributors,
  compact = false,
  max = 8,
  className = '',
}: SessionContributorsStripProps): ReactElement | null {
  if (!contributors || contributors.length === 0) {
    return null;
  }

  const visibleCount = compact ? Math.min(contributors.length, 5) : Math.min(contributors.length, max);
  const visible = contributors.slice(0, visibleCount);
  const overflow = contributors.length - visible.length;

  return (
    <div
      data-testid="session-contributors-strip"
      className={`flex items-center gap-3 rounded-lg border border-border bg-muted px-4 py-3.5 ${className}`}
    >
      <div className="flex-shrink-0">
        <div className="font-[var(--font-jetbrains)] text-[9px] font-bold uppercase tracking-[0.08em] text-muted-foreground mb-0.5">
          Top contributors
        </div>
        <div className="font-[var(--font-quicksand)] text-[13px] font-bold text-foreground">
          {contributors.length} player{contributors.length === 1 ? '' : 's'}
        </div>
      </div>
      <div className="flex flex-1 items-center min-w-0">
        {visible.map((contributor, idx) => {
          const hue = hueFromUserId(contributor.userId);
          return (
            <div
              key={contributor.userId}
              title={`${contributor.displayName} · ${contributor.sessionCount} session${contributor.sessionCount === 1 ? '' : 's'}`}
              aria-label={`${contributor.displayName}, ${contributor.sessionCount} sessions`}
              data-testid="session-contributor-avatar"
              className="relative inline-flex h-[34px] w-[34px] flex-shrink-0 items-center justify-center rounded-full border-2 border-background font-[var(--font-quicksand)] text-[11px] font-extrabold text-white"
              style={{
                background: `hsl(${hue} 60% 55%)`,
                marginLeft: idx === 0 ? 0 : -8,
                zIndex: visible.length - idx,
              }}
            >
              {contributor.initials}
            </div>
          );
        })}
        {overflow > 0 && (
          <div
            data-testid="session-contributors-overflow"
            aria-label={`${overflow} more contributors`}
            className="ml-[-8px] flex h-[34px] w-[34px] flex-shrink-0 items-center justify-center rounded-full border-2 border-background bg-card font-[var(--font-jetbrains)] text-[10px] font-extrabold text-muted-foreground"
          >
            +{overflow}
          </div>
        )}
      </div>
    </div>
  );
}
