import React from 'react';

export interface MechanicAnalysisFooterAttributionProps {
  readonly totalTokensUsed?: number;
  readonly estimatedCostUsd?: number;
}

/**
 * #526 AC-5 — ADR-051 canonical attribution footer. Shared between the admin review page and the
 * public card (#528). Replaces the retired Variant-C attribution copy.
 */
export function MechanicAnalysisFooterAttribution({
  totalTokensUsed,
  estimatedCostUsd,
}: MechanicAnalysisFooterAttributionProps): React.JSX.Element {
  return (
    <div className="rounded-lg border border-green-200 bg-green-50/50 p-4 text-center text-xs text-green-800 dark:border-green-800 dark:bg-green-950/20 dark:text-green-300 print:border-green-400">
      <strong>&copy; 2026 MeepleAI</strong> — Contenuto originale.
      <br />
      <span className="opacity-70">
        Analisi elaborata dall&apos;AI sul manuale del gioco. Ogni affermazione è riformulata in
        parole originali e cita la pagina del regolamento. Copyright &copy; degli editori per il
        testo originale del manuale.
      </span>
      {(totalTokensUsed ?? 0) > 0 && (
        <span className="ml-2 opacity-70">
          | {totalTokensUsed} tokens, ${(estimatedCostUsd ?? 0).toFixed(4)}
        </span>
      )}
    </div>
  );
}
