/**
 * RecentActivityRail — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (RecentActivityRail sidebar, jsx:949-1017).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md §3.2
 * PR2 Task 2.4 (#1585 follow-up) — mockup re-skin:
 *   - Sidebar 280px with `bg-card`, left border, header with 🕐 emoji
 *   - Timeline: per-item entity-colored 22px circle + connector line (n-1)
 *   - Right column: timestamp (font-mono uppercase 9px) + body line with
 *     entity-colored anchor for `entityTitle`
 *   - Keyboard shortcuts box at bottom (kbd /, f, ?)
 *
 * Data contract (preserved, NOT changed by Task 2.4):
 *   `ActivityItem = { id, kind, entityTitle, timestamp }`.
 *   The mockup's `who`/`what`/`ref` triple is demo-only; the production DTO
 *   (`ActivityItemDto`, BE-3 #1590) exposes only the four fields above. We
 *   render `timestamp` in the upper position and `entityTitle` as the
 *   entity-colored body anchor, skipping the `<strong>{who}</strong> {what}`
 *   prefix since the BE does not surface those fields.
 *
 * Phase 1 contract (locked by AC-7):
 *   No backend endpoint `GET /api/v1/library/activity` exists yet, so the
 *   orchestrator (LibraryHub) ALWAYS passes `items={[]}`. The component
 *   renders the placeholder copy "Activity feed prossimamente". A loading
 *   variant (`isLoading`) renders 3 skeleton lines so the sidebar reserves
 *   space and avoids layout shift if/when the endpoint lands in Phase 2.
 *
 * Phase 2 readiness:
 *   Even though `items` is currently always empty, the component already
 *   renders a list when given data. This avoids a rewrite when the backend
 *   endpoint ships — the orchestrator just stops short-circuiting and the
 *   sidebar lights up. Each item carries `data-activity-kind` so per-kind
 *   icon/styling can be layered without touching the component contract.
 */

import { useState, type ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useTranslation } from '@/hooks/useTranslation';

export type ActivityKind =
  | 'play'
  | 'add'
  | 'kb-indexed'
  | 'rating-changed'
  | 'removed'
  | 'agent'
  | 'chat';

export interface ActivityItem {
  readonly id: string;
  readonly kind: ActivityKind;
  readonly entityTitle: string;
  readonly timestamp: string;
}

export interface RecentActivityRailProps {
  readonly items: ReadonlyArray<ActivityItem>;
  readonly isLoading?: boolean;
  readonly error?: Error | null;
  readonly className?: string;
}

const SKELETON_LINES = 3;
const MAX_TIMELINE_ITEMS = 6;

const KIND_ICON: Record<ActivityKind, string> = {
  play: '🎯',
  add: '🎲',
  'kb-indexed': '📚',
  'rating-changed': '⭐',
  removed: '🗑️',
  agent: '🤖',
  chat: '💬',
};

/**
 * Maps each `ActivityKind` to a Tailwind `entity-{name}` color slot. Slots
 * available: `game`, `agent`, `kb`, `session`, `chat`. Tailwind needs literal
 * class names in source (no dynamic concat) for the JIT scanner, so the full
 * triplet (bg/14, text, border/30) is materialized here.
 */
type EntitySlot = 'game' | 'agent' | 'kb' | 'session' | 'chat';

const ACTIVITY_KIND_ENTITY: Record<ActivityKind, EntitySlot> = {
  play: 'session',
  add: 'game',
  'kb-indexed': 'kb',
  'rating-changed': 'game',
  removed: 'game',
  agent: 'agent',
  chat: 'chat',
};

const ENTITY_CIRCLE_CLASS: Record<EntitySlot, string> = {
  game: 'bg-entity-game/15 text-entity-game border-entity-game/30',
  agent: 'bg-entity-agent/15 text-entity-agent border-entity-agent/30',
  kb: 'bg-entity-kb/15 text-entity-kb border-entity-kb/30',
  session: 'bg-entity-session/15 text-entity-session border-entity-session/30',
  chat: 'bg-entity-chat/15 text-entity-chat border-entity-chat/30',
};

const ENTITY_ANCHOR_CLASS: Record<EntitySlot, string> = {
  game: 'text-entity-game-text',
  agent: 'text-entity-agent',
  kb: 'text-entity-kb-text',
  session: 'text-entity-session',
  chat: 'text-entity-chat',
};

type RailState = 'loading' | 'empty' | 'populated' | 'error';

export function RecentActivityRail({
  items,
  isLoading = false,
  error = null,
  className,
}: RecentActivityRailProps): ReactElement {
  const { t } = useTranslation();
  const [collapsed, setCollapsed] = useState(false);
  const state: RailState = error
    ? 'error'
    : isLoading
      ? 'loading'
      : items.length === 0
        ? 'empty'
        : 'populated';

  const visibleItems = items.slice(0, MAX_TIMELINE_ITEMS);

  return (
    <aside
      data-slot="library-activity-rail"
      data-state={state}
      data-collapsed={collapsed || undefined}
      aria-busy={isLoading || undefined}
      aria-live="polite"
      className={clsx(
        // Mockup jsx:950-954 — sidebar 280px, bg-card, left border, padded scrollable.
        // Hidden under `lg` so the rail collapses on tablet/mobile (preserves
        // pre-Task-2.4 responsive contract). When collapsed (#564) the aside
        // narrows to a rail strip; being a flex sibling of the library content
        // means the grid reclaims the freed width — no parent coordination.
        'hidden lg:flex lg:flex-shrink-0 lg:flex-col',
        collapsed ? 'lg:w-[52px] lg:items-center px-2' : 'lg:w-[280px] px-4',
        'overflow-y-auto border-l border-border bg-card py-4',
        className
      )}
    >
      {/* Header (jsx:955-969) — collapse/expand disclosure toggle (#564). */}
      <div
        className={clsx(
          'flex items-center',
          collapsed ? 'flex-col gap-2' : 'mb-3.5 justify-between'
        )}
      >
        {collapsed ? (
          <span aria-hidden="true" className="text-[13px]">
            🕐
          </span>
        ) : (
          <h3 className="m-0 flex items-center gap-1.5 font-display text-[13px] font-extrabold text-foreground">
            <span aria-hidden="true">🕐</span>
            {t('pages.library.activityRail.title')}
          </h3>
        )}
        <button
          type="button"
          aria-label={t(
            collapsed
              ? 'pages.library.activityRail.expandAriaLabel'
              : 'pages.library.activityRail.collapseAriaLabel'
          )}
          aria-expanded={!collapsed}
          onClick={() => setCollapsed(prev => !prev)}
          className="h-6 w-6 cursor-pointer rounded-sm border border-border bg-transparent text-[11px] text-muted-foreground"
        >
          <span aria-hidden="true">{collapsed ? '‹' : '›'}</span>
        </button>
      </div>

      {collapsed ? null : (
        <>
          {state === 'loading' ? (
            <div className="flex flex-col gap-2">
              {Array.from({ length: SKELETON_LINES }, (_, index) => (
                <Skeleton
                  key={index}
                  data-slot="library-activity-skeleton"
                  data-testid="library-activity-skeleton"
                  className="h-12 w-full rounded-lg"
                />
              ))}
            </div>
          ) : null}

          {state === 'empty' ? (
            <p
              className="text-sm text-muted-foreground"
              data-slot="library-activity-empty"
              data-testid="library-activity-empty-text"
            >
              {t('pages.library.activityRail.empty')}
            </p>
          ) : null}

          {state === 'error' ? (
            <p
              className="text-sm text-destructive"
              role="alert"
              data-slot="library-activity-error"
              data-testid="library-activity-error"
            >
              {t('pages.library.activityRail.error')}
            </p>
          ) : null}

          {state === 'populated' ? (
            <div className="relative">
              {visibleItems.map((item, index) => {
                const entity = ACTIVITY_KIND_ENTITY[item.kind];
                const isLast = index === visibleItems.length - 1;
                return (
                  <div
                    key={item.id}
                    data-slot="library-activity-item"
                    data-activity-kind={item.kind}
                    className={clsx('flex gap-2.5', !isLast && 'mb-2.5')}
                  >
                    {/* Left rail: circle + connector */}
                    <div className="flex flex-shrink-0 flex-col items-center">
                      <div
                        data-slot="library-activity-rail-circle"
                        aria-hidden="true"
                        className={clsx(
                          'flex h-[22px] w-[22px] items-center justify-center rounded-full border text-[10px]',
                          ENTITY_CIRCLE_CLASS[entity]
                        )}
                      >
                        {KIND_ICON[item.kind]}
                      </div>
                      {!isLast ? (
                        <div
                          data-slot="library-activity-rail-connector"
                          aria-hidden="true"
                          className="mt-0.5 min-h-3.5 w-[1.5px] flex-1 bg-border"
                        />
                      ) : null}
                    </div>
                    {/* Right column: timestamp + body line */}
                    <div className="min-w-0 flex-1 pb-0.5">
                      <div className="font-mono text-[9px] font-bold uppercase tracking-[0.06em] text-muted-foreground">
                        {item.timestamp}
                      </div>
                      <div className="text-[11.5px] leading-[1.4] text-foreground">
                        <span
                          className={clsx('font-bold no-underline', ENTITY_ANCHOR_CLASS[entity])}
                        >
                          {item.entityTitle}
                        </span>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : null}

          {/* Keyboard shortcuts box (jsx:1004-1015). Rendered in every
          expanded state (loading/empty/populated) for a consistent footer;
          hidden when the rail is collapsed (#564). */}
          <div className="mt-3 rounded-md bg-muted px-3 py-2.5 text-[11px] leading-relaxed text-muted-foreground">
            <strong className="mb-0.5 block text-foreground/80">
              <span aria-hidden="true">⌨ </span>
              {t('pages.library.activityRail.shortcuts.heading')}
            </strong>
            <div className="flex flex-col gap-0.5 font-mono text-[10px]">
              <span>
                <kbd className="mr-1 rounded-[3px] border border-border bg-card px-1.5 font-mono font-extrabold text-foreground/80">
                  /
                </kbd>
                {t('pages.library.activityRail.shortcuts.focusSearch')}
              </span>
              <span>
                <kbd className="mr-1 rounded-[3px] border border-border bg-card px-1.5 font-mono font-extrabold text-foreground/80">
                  f
                </kbd>
                {t('pages.library.activityRail.shortcuts.advancedFilters')}
              </span>
              <span>
                <kbd className="mr-1 rounded-[3px] border border-border bg-card px-1.5 font-mono font-extrabold text-foreground/80">
                  ?
                </kbd>
                {t('pages.library.activityRail.shortcuts.allShortcuts')}
              </span>
            </div>
          </div>
        </>
      )}
    </aside>
  );
}
