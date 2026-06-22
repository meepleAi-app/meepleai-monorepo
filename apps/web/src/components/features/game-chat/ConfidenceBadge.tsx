/**
 * ConfidenceBadge — visualizza confidence score con 3 fasce colore.
 * Pure component. Tier calcolato internamente:
 *   alta:  score >= 0.80  (verde, --c-success)
 *   media: 0.50 <= s < 0.80 (arancione, --c-warning)
 *   bassa: score < 0.50   (rosso, --c-danger)
 * Spec: §3.2 §4.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type ConfidenceTier = 'alta' | 'media' | 'bassa';

export interface ConfidenceBadgeProps {
  readonly score: number;
  readonly kind?: 'inline' | 'compact';
  readonly className?: string;
}

export function getConfidenceTier(score: number): ConfidenceTier {
  if (score >= 0.8) return 'alta';
  if (score >= 0.5) return 'media';
  return 'bassa';
}

const TIER_LABEL: Record<ConfidenceTier, string> = {
  alta: '🟢 Confidence alta',
  media: '🟡 Confidence media',
  bassa: '🔴 Confidence bassa',
};

const TIER_COLORS: Record<ConfidenceTier, string> = {
  alta: 'bg-[hsl(var(--c-success)/0.15)] text-[hsl(var(--c-success))]',
  media: 'bg-[hsl(var(--c-warning)/0.18)] text-[hsl(var(--c-warning))]',
  bassa: 'bg-[hsl(var(--c-danger)/0.15)] text-[hsl(var(--c-danger))]',
};

export function ConfidenceBadge({
  score,
  kind = 'inline',
  className,
}: ConfidenceBadgeProps): ReactElement {
  const tier = getConfidenceTier(score);
  const label = TIER_LABEL[tier];
  const showScore = kind !== 'compact';

  return (
    <span
      role="status"
      data-tier={tier}
      data-slot="confidence-badge"
      className={clsx(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5',
        'font-mono text-[10px] font-bold uppercase tracking-wider',
        TIER_COLORS[tier],
        className
      )}
    >
      <span>{label}</span>
      {showScore && <span>· {score.toFixed(2)}</span>}
    </span>
  );
}
