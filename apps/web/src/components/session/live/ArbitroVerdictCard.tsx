/**
 * ArbitroVerdictCard
 *
 * Game Night Improvvisata — Task 17
 *
 * Displays an AI rule-dispute verdict with amber border styling,
 * rule references, and optional note.
 */

'use client';

import type { RuleDisputeResponse } from '@/lib/api/schemas/improvvisata.schemas';

interface ArbitroVerdictCardProps {
  verdict: RuleDisputeResponse;
}

export function ArbitroVerdictCard({ verdict }: ArbitroVerdictCardProps) {
  return (
    <div className="border-2 border-amber-400 bg-amber-50 rounded-xl p-4 space-y-3">
      {/* Header */}
      <div className="flex items-center gap-2">
        <span className="text-2xl" aria-hidden="true">
          ⚖️
        </span>
        <h3 className="font-quicksand font-bold text-amber-900 text-base">Verdetto</h3>
      </div>

      {/* Verdict text */}
      <p className="text-sm text-amber-900 font-nunito leading-relaxed">{verdict.verdict}</p>

      {/* Rule references */}
      {verdict.ruleReferences.length > 0 && (
        <div className="flex items-start gap-1.5">
          <span className="text-sm" aria-hidden="true">
            📖
          </span>
          <p className="text-xs text-amber-700 font-nunito">{verdict.ruleReferences.join(' | ')}</p>
        </div>
      )}

      {/* Optional note */}
      {verdict.note && (
        <p className="text-xs text-gray-500 font-nunito italic border-t border-amber-200 pt-2">
          {verdict.note}
        </p>
      )}
    </div>
  );
}
