/**
 * GameDetailSpecsCard - Wave C.1 follow-up (Issue #1463)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (SpecsCard, lines 381-420).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573); design-handoff PILOT_GAP_REPORT § 2.1
 *
 * Pure presentational grid of N (default 8) game specs items. Each item renders a
 * uppercase mono label + display-font value. Consumer formats `value` upstream via
 * `buildSpecsItems(detail, t)` helper sibling (mirror `buildKpiCards()` pattern).
 *
 * AC: T A V (no animation, no V change beyond responsive grid).
 * Pattern conformance vs sibling `GameDetailKpiCards.tsx` (8 invariants — see AC-3).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface GameDetailSpecsItem {
  readonly key: string;
  readonly label: string;
  readonly value: string;
}

export interface GameDetailSpecsCardProps {
  readonly items: ReadonlyArray<GameDetailSpecsItem>;
  /**
   * Localized card title (e.g. via `t('pages.gameDetail.info.specsTitle')`).
   * Required: caller MUST resolve i18n string upstream to keep the component
   * pure and locale-agnostic (no Italian hardcoded fallback).
   */
  readonly title: string;
  readonly className?: string;
}

export function GameDetailSpecsCard(props: GameDetailSpecsCardProps): ReactElement {
  const { items, title, className } = props;

  return (
    <section
      data-slot="game-detail-specs-card"
      className={clsx('rounded-2xl border border-border bg-card p-[18px] shadow-sm', className)}
    >
      <h3 className="mb-3.5 font-display text-[15px] font-extrabold text-foreground">{title}</h3>
      <div
        role="list"
        aria-label={title}
        className="grid grid-cols-[repeat(auto-fill,minmax(140px,1fr))] gap-3"
      >
        {items.map(item => (
          <div
            key={item.key}
            role="listitem"
            data-slot="game-detail-specs-item"
            data-spec-key={item.key}
          >
            <div className="mb-[3px] font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
              {item.label}
            </div>
            <div className="line-clamp-1 font-display text-[14px] font-extrabold text-foreground">
              {item.value}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
