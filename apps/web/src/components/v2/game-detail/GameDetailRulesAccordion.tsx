/**
 * GameDetailRulesAccordion - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (rules section).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Native `<details>`/`<summary>` accordion for rule sections — provides
 * keyboard support and ARIA semantics out of the box. Animation is gated by
 * `motion-safe:` so reduced-motion preference collapses it instantly.
 *
 * AC: T A M V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export interface GameDetailRuleSection {
  readonly id: string;
  readonly title: string;
  readonly summary: string;
}

export interface GameDetailRulesAccordionLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly viewAll: string;
  readonly viewAllAriaLabel: string;
  readonly empty: string;
}

export interface GameDetailRulesAccordionProps {
  readonly sections: ReadonlyArray<GameDetailRuleSection>;
  readonly viewAllHref: string;
  readonly labels: GameDetailRulesAccordionLabels;
  readonly defaultOpen?: ReadonlyArray<string>;
  readonly className?: string;
}

export function GameDetailRulesAccordion(props: GameDetailRulesAccordionProps): ReactElement {
  const { sections, viewAllHref, labels, defaultOpen = [], className } = props;
  const isEmpty = sections.length === 0;

  return (
    <section
      data-slot="game-detail-rules-accordion"
      data-empty={isEmpty}
      className={clsx('rounded-2xl border border-border bg-card p-4 shadow-sm', className)}
    >
      <header className="mb-3 flex items-baseline justify-between gap-2">
        <div>
          <h3 className="font-display text-[15px] font-extrabold text-foreground">
            {labels.title}
          </h3>
          <p className="mt-0.5 text-[12px] text-muted-foreground">{labels.subtitle}</p>
        </div>
        {!isEmpty ? (
          <Link
            href={viewAllHref}
            aria-label={labels.viewAllAriaLabel}
            data-slot="game-detail-rules-view-all"
            className="rounded-md border border-border px-3 py-1 font-display text-[11px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {labels.viewAll}
          </Link>
        ) : null}
      </header>

      {isEmpty ? (
        <p data-slot="game-detail-rules-empty" className="text-[13px] text-muted-foreground">
          {labels.empty}
        </p>
      ) : (
        <ul className="flex flex-col gap-2" role="list">
          {sections.map(section => (
            <li key={section.id}>
              <details
                data-slot="game-detail-rules-section"
                data-section-id={section.id}
                open={defaultOpen.includes(section.id)}
                className="group rounded-xl border border-border bg-background"
              >
                <summary className="flex cursor-pointer list-none items-center justify-between gap-3 rounded-xl px-3.5 py-2.5 font-display text-[13px] font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring [&::-webkit-details-marker]:hidden">
                  <span>{section.title}</span>
                  <span
                    aria-hidden="true"
                    className="text-muted-foreground transition-transform motion-reduce:transition-none group-open:rotate-180"
                  >
                    ▾
                  </span>
                </summary>
                <div className="border-t border-border px-3.5 py-2.5 text-[13px] leading-relaxed text-muted-foreground">
                  {section.summary}
                </div>
              </details>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
