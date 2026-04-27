/**
 * AccordionItem (v2) — FAQ-specific accordion row with chevron + category badge.
 *
 * Wave A.1 Phase 4.5 (Issue #583). Mirrors mockup lines 390-467.
 * Distinct from legacy `data-display/accordion.tsx` — v2 introduces inline
 * category badge, animated chevron rotation 180°, max-h transition 300ms.
 */

'use client';

import { type JSX, type ReactNode, useId } from 'react';

import clsx from 'clsx';
import { ChevronDown } from 'lucide-react';

export interface AccordionItemProps {
  readonly id: string;
  readonly question: ReactNode;
  readonly answer: ReactNode;
  readonly categoryLabel: string;
  readonly categoryIcon: string;
  readonly isOpen: boolean;
  readonly onToggle: () => void;
  readonly highlighted?: boolean;
  readonly className?: string;
}

export function AccordionItem({
  id,
  question,
  answer,
  categoryLabel,
  categoryIcon,
  isOpen,
  onToggle,
  highlighted = false,
  className,
}: AccordionItemProps): JSX.Element {
  const reactId = useId();
  const buttonId = `faq-trigger-${id}-${reactId}`;
  const panelId = `faq-panel-${id}-${reactId}`;

  return (
    <div
      id={`faq-${id}`}
      data-slot="accordion-item"
      data-open={isOpen ? 'true' : undefined}
      className={clsx(
        'border-b border-border last:border-b-0 transition-colors duration-200',
        highlighted && 'bg-[hsl(var(--c-warning)/0.05)]',
        className
      )}
    >
      <button
        type="button"
        id={buttonId}
        onClick={onToggle}
        aria-expanded={isOpen}
        aria-controls={panelId}
        className={clsx(
          'flex w-full cursor-pointer items-start gap-3 border-none bg-transparent px-1 py-[18px] text-left',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2'
        )}
      >
        <span
          aria-hidden="true"
          className="mt-0.5 inline-flex flex-shrink-0 items-center gap-1 rounded-full bg-[hsl(var(--bg-muted))] px-[7px] py-0.5 font-mono text-[10px] font-bold uppercase tracking-[0.04em] text-[hsl(var(--text-muted))]"
        >
          <span>{categoryIcon}</span>
          <span>{categoryLabel}</span>
        </span>
        <span className="flex-1 font-display text-[15px] font-semibold leading-[1.45] text-foreground">
          {question}
        </span>
        <span
          aria-hidden="true"
          className={clsx(
            'inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full',
            'transition-[transform,background-color,color] duration-200 ease-out',
            isOpen
              ? 'bg-[hsl(var(--c-game))] text-white rotate-180'
              : 'bg-[hsl(var(--bg-muted))] text-[hsl(var(--text-sec))] rotate-0'
          )}
        >
          <ChevronDown size={18} strokeWidth={2.5} />
        </span>
      </button>
      <div
        id={panelId}
        role="region"
        aria-labelledby={buttonId}
        hidden={!isOpen}
        className={clsx(
          'overflow-hidden transition-[max-height] ease-[cubic-bezier(0.4,0,0.2,1)]',
          isOpen ? 'max-h-[800px] duration-300' : 'max-h-0 duration-200'
        )}
      >
        <div className="max-w-[720px] px-1 pb-[18px] text-[13.5px] leading-[1.65] text-[hsl(var(--text-sec))]">
          {answer}
        </div>
      </div>
    </div>
  );
}
