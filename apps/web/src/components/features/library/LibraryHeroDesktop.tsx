/**
 * LibraryHeroDesktop — re-skinned to SP4 mockup (PR1 Task 1.2 of
 * docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx` lines 35–129
 * (the `LibraryHero` component). Pure component (no `useTranslation`); labels +
 * callbacks are injected by `LibraryHub`.
 *
 * Visual contract (mockup jsx:35–129):
 *   - 3-stop linear-gradient (game 10% / agent 6% / kb 8%) + decorative blob
 *   - Eyebrow pill (📚 Library · power-user view) above the h1 title
 *   - 4 stat pills (replacing the legacy large `dl` tiles), each with
 *     entity-colored border and entity-colored count
 *   - Action bar with 3 CTAs (Add primary / Import BGG secondary / Export icon)
 *   - `compact={true}` collapses subtitle + action bar (mobile mode)
 *
 * The `LibraryHeroStat.entity` discriminator was added in this re-skin
 * (PR1 Task 1.2 — Phase 1 of the SP4 conformance pass) so each stat pill
 * can render its own entity-colored border without a CSS lookup table
 * in the consumer.
 */

import type { CSSProperties, ReactElement } from 'react';

import clsx from 'clsx';

import { entityEmoji, type EntityType } from '@/lib/entity-color';

/**
 * Stat key discriminator for the hero stat chips. The 4 original game-only keys
 * (`totalGames|kbReady|wishlist|loaned`) are preserved for backward compatibility
 * with any code that still surfaces the legacy stats; Phase 2a (#1605) added the
 * hybrid hub stat keys (`agents|docs|chats`) — `LibraryHub` now passes those
 * alongside `totalGames` for the 4 cross-entity stat chips.
 */
export type LibraryHeroStatKey =
  | 'totalGames'
  | 'kbReady'
  | 'wishlist'
  | 'loaned'
  | 'agents'
  | 'docs'
  | 'chats';

export interface LibraryHeroDesktopLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly ctaAdd: string;
  readonly ctaImportBgg: string;
  readonly ctaExportAriaLabel: string;
  readonly eyebrow: string;
}

export interface LibraryHeroStat {
  readonly key: LibraryHeroStatKey;
  readonly label: string;
  readonly value: number;
  /**
   * Entity color discriminator for the pill's border + count color.
   * Added in PR1 Task 1.2 (SP4 mockup conformance). The narrow union mirrors
   * `MeepleEntityType` from `@/lib/entity-color` so the runtime emoji /
   * `--c-{entity}` HSL lookups stay type-safe.
   */
  readonly entity: EntityType;
}

export interface LibraryHeroDesktopProps {
  readonly labels: LibraryHeroDesktopLabels;
  readonly stats: readonly LibraryHeroStat[];
  readonly compact?: boolean;
  readonly onAddGame?: () => void;
  readonly onImportBgg?: () => void;
  readonly onExport?: () => void;
  readonly className?: string;
}

/**
 * Tailwind class lookup for the per-pill entity border. Tailwind v3 needs
 * full class strings to be statically discoverable, so we cannot interpolate
 * `border-entity-${entity}/20` inline — the JIT would miss the variants.
 */
const STAT_BORDER_CLASS: Record<EntityType, string> = {
  game: 'border-entity-game/20',
  agent: 'border-entity-agent/20',
  kb: 'border-entity-kb/20',
  chat: 'border-entity-chat/20',
  session: 'border-entity-session/20',
  event: 'border-entity-event/20',
  player: 'border-entity-player/20',
  toolkit: 'border-entity-toolkit/20',
  tool: 'border-entity-tool/20',
  // #1929 WP2: GameNight reuses event token (no separate Tailwind entity-gameNightEvent utility)
  gameNightEvent: 'border-entity-event/20',
};

const STAT_TEXT_CLASS: Record<EntityType, string> = {
  game: 'text-entity-game',
  agent: 'text-entity-agent',
  kb: 'text-entity-kb',
  chat: 'text-entity-chat',
  session: 'text-entity-session',
  event: 'text-entity-event',
  player: 'text-entity-player',
  toolkit: 'text-entity-toolkit',
  tool: 'text-entity-tool',
  gameNightEvent: 'text-entity-event',
};

// Mockup palette: 3-stop linear gradient on the hero container background.
const HERO_GRADIENT: CSSProperties = {
  background:
    'linear-gradient(135deg, hsl(var(--c-game) / 0.10) 0%, hsl(var(--c-agent) / 0.06) 50%, hsl(var(--c-kb) / 0.08) 100%)',
};

// Decorative radial blob, top-right, behind the content (aria-hidden).
const HERO_BLOB: CSSProperties = {
  background: 'radial-gradient(circle, hsl(var(--c-game) / 0.10) 0%, transparent 70%)',
};

// Primary CTA shadow — entity-glow (game) at 40% alpha.
const PRIMARY_CTA_SHADOW: CSSProperties = {
  boxShadow: '0 4px 14px hsl(var(--c-game) / 0.4)',
};

export function LibraryHeroDesktop({
  labels,
  stats,
  compact = false,
  onAddGame,
  onImportBgg,
  onExport,
  className,
}: LibraryHeroDesktopProps): ReactElement {
  return (
    <section
      data-slot="library-hero-desktop"
      style={HERO_GRADIENT}
      className={clsx(
        'relative overflow-hidden border-b border-border/60',
        compact ? 'px-4 py-4' : 'px-8 py-6',
        className
      )}
    >
      <div
        aria-hidden="true"
        style={HERO_BLOB}
        className="pointer-events-none absolute -right-10 -top-10 h-60 w-60 rounded-full"
      />
      <div
        className={clsx(
          'relative z-[1] flex flex-wrap',
          compact ? 'flex-col items-start gap-3.5' : 'items-end gap-6'
        )}
      >
        {/* ─── Title cluster ─── */}
        <div className="min-w-0 flex-1">
          <div
            data-slot="library-hero-eyebrow"
            className="mb-2 inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground"
          >
            <span aria-hidden="true">📚</span>
            {labels.eyebrow}
          </div>
          <h1
            className={clsx(
              'm-0 mb-1 font-quicksand font-extrabold leading-[1.05] tracking-[-0.02em] text-foreground',
              compact ? 'text-[26px]' : 'text-[38px]'
            )}
          >
            {labels.title}
          </h1>
          <p
            className={clsx(
              'm-0 font-body font-medium text-muted-foreground',
              compact ? 'text-[13px]' : 'text-[14.5px]'
            )}
          >
            {labels.subtitle}
          </p>
        </div>

        {/* ─── Stats inline pills ─── */}
        <div
          data-slot="library-hero-stats"
          className={clsx('flex flex-wrap', compact ? 'gap-2' : 'gap-3.5')}
        >
          {stats.map(stat => (
            <div
              key={stat.key}
              data-slot="library-hero-stat"
              data-stat-key={stat.key}
              data-entity={stat.entity}
              className={clsx(
                'flex items-center gap-1.5 rounded-md border bg-card px-3 py-[7px]',
                STAT_BORDER_CLASS[stat.entity]
              )}
            >
              <span aria-hidden="true" className="text-sm">
                {entityEmoji(stat.entity)}
              </span>
              <span
                data-slot="library-hero-stat-value"
                className={clsx(
                  'font-mono text-sm font-extrabold tabular-nums',
                  STAT_TEXT_CLASS[stat.entity]
                )}
              >
                {stat.value}
              </span>
              <span className="font-quicksand text-[11.5px] font-bold text-muted-foreground">
                {stat.label}
              </span>
            </div>
          ))}
        </div>

        {/* ─── Action bar (desktop only) ─── */}
        {compact ? null : (
          <div
            data-slot="library-hero-actions"
            className="flex w-full flex-shrink-0 justify-end gap-2"
          >
            <button
              type="button"
              onClick={onAddGame}
              style={PRIMARY_CTA_SHADOW}
              className="inline-flex cursor-pointer items-center gap-1.5 rounded-md border-0 bg-entity-game px-4 py-[9px] font-quicksand text-[13px] font-extrabold text-white"
            >
              {labels.ctaAdd}
            </button>
            {onImportBgg ? (
              <button
                type="button"
                onClick={onImportBgg}
                className="cursor-pointer rounded-md border border-border-strong bg-card px-[14px] py-[9px] font-quicksand text-[13px] font-bold text-foreground"
              >
                {labels.ctaImportBgg}
              </button>
            ) : null}
            <button
              type="button"
              onClick={onExport}
              disabled={onExport === undefined}
              aria-label={labels.ctaExportAriaLabel}
              className="min-w-[38px] cursor-pointer rounded-md border border-border-strong bg-card px-3 py-[9px] text-sm text-foreground disabled:cursor-not-allowed disabled:opacity-60"
            >
              ↗
            </button>
          </div>
        )}
      </div>
    </section>
  );
}
