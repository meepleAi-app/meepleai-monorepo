/**
 * GameDetailFaqList - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (InfoTab FAQ section).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Renders the top N FAQs (default 5) as a static list. Includes a CTA link to
 * `/games/[id]/faqs` for the full FAQ subroute (preserved from v1 per Phase 0.5 sez. 4.4).
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export interface GameDetailFaqEntry {
  readonly id: string;
  readonly question: string;
  readonly answer: string;
}

export interface GameDetailFaqListLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly viewAll: string;
  readonly viewAllAriaLabel: string;
  readonly empty: string;
  readonly questionAriaLabel: string;
}

export interface GameDetailFaqListProps {
  readonly faqs: ReadonlyArray<GameDetailFaqEntry>;
  readonly viewAllHref: string;
  readonly labels: GameDetailFaqListLabels;
  readonly className?: string;
}

export function GameDetailFaqList(props: GameDetailFaqListProps): ReactElement {
  const { faqs, viewAllHref, labels, className } = props;
  const isEmpty = faqs.length === 0;

  return (
    <section
      data-slot="game-detail-faq-list"
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
            data-slot="game-detail-faq-view-all"
            className="rounded-md border border-border px-3 py-1 font-display text-[11px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {labels.viewAll}
          </Link>
        ) : null}
      </header>

      {isEmpty ? (
        <p data-slot="game-detail-faq-empty" className="text-[13px] text-muted-foreground">
          {labels.empty}
        </p>
      ) : (
        <ul className="flex flex-col divide-y divide-border" role="list">
          {faqs.map(faq => (
            <li key={faq.id} className="py-3">
              <h4
                aria-label={labels.questionAriaLabel}
                className="font-display text-[13px] font-bold text-foreground"
              >
                {faq.question}
              </h4>
              <p className="mt-1 text-[13px] leading-relaxed text-muted-foreground">{faq.answer}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
