/**
 * LowConfidenceDisclaimer — banner giallo con summary "non sono certo"
 * + lista alternative (KB interne o link esterni).
 * Pure component. Renderizzato solo quando response.isLowQuality === true.
 * Spec: §3.2 §4.2
 */
import type { ReactElement } from 'react';
import clsx from 'clsx';

export interface DisclaimerAlternative {
  readonly label: string;
  readonly kind: 'kb' | 'external';
  readonly url?: string;
}

export interface LowConfidenceDisclaimerProps {
  readonly summary: string;
  readonly alternatives: readonly DisclaimerAlternative[];
  readonly className?: string;
}

export function LowConfidenceDisclaimer({
  summary,
  alternatives,
  className,
}: LowConfidenceDisclaimerProps): ReactElement {
  return (
    <div
      role="note"
      data-slot="low-confidence-disclaimer"
      className={clsx(
        'mt-3 rounded-md border-l-4 p-3 text-sm',
        'border-l-[hsl(var(--c-warning))] bg-[hsl(var(--c-warning)/0.08)]',
        className
      )}
    >
      <p>
        <strong className="font-bold text-[hsl(var(--c-warning))]">⚠️ Non sono certo.</strong>{' '}
        {summary}
      </p>
      {alternatives.length > 0 && (
        <ul className="mt-2 ml-5 list-disc">
          {alternatives.map((alt, i) => (
            <li key={i}>
              {alt.kind === 'external' && alt.url ? (
                <a
                  href={alt.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="underline hover:opacity-80"
                >
                  🔗 {alt.label}
                </a>
              ) : (
                <span>📖 {alt.label}</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
