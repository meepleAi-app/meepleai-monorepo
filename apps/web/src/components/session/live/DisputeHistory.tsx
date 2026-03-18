/**
 * DisputeHistory
 *
 * Game Night Improvvisata — Task 17 (v1) + Task 15 (v2)
 *
 * Collapsible section listing all rule dispute verdicts for the current session.
 * When `gameId` is provided, it could fetch cross-session history (future extension);
 * for now it reads from the live-session-store.
 *
 * v2 additions: confidence badge, vote counts, outcome, override rule.
 */

'use client';

import { useState } from 'react';

import { ChevronDown, ChevronUp } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { ArbitroVerdictCard } from './ArbitroVerdictCard';

// ─── Types ────────────────────────────────────────────────────────────────────

interface DisputeHistoryProps {
  sessionId: string;
  /** Reserved for future cross-session history retrieval */
  gameId?: string;
}

// ─── v2 helpers ───────────────────────────────────────────────────────────────

const confidenceBadge = {
  High: { label: 'Alta', className: 'bg-green-100 text-green-800' },
  Medium: { label: 'Media', className: 'bg-amber-100 text-amber-800' },
  Low: { label: 'Bassa', className: 'bg-red-100 text-red-800' },
} as const;

const outcomeBadge = {
  VerdictAccepted: { label: 'Accettato', className: 'bg-green-100 text-green-800' },
  VerdictOverridden: { label: 'Respinto', className: 'bg-red-100 text-red-800' },
} as const;

// ─── Component ────────────────────────────────────────────────────────────────

export function DisputeHistory({ sessionId: _sessionId }: DisputeHistoryProps) {
  const disputes = useLiveSessionStore(s => s.disputes);
  const [isOpen, setIsOpen] = useState(false);

  if (disputes.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      {/* Toggle button */}
      <Button
        variant="ghost"
        size="sm"
        className="w-full justify-between font-nunito text-gray-600 hover:text-gray-800"
        onClick={() => setIsOpen(prev => !prev)}
        aria-expanded={isOpen}
      >
        <span className="flex items-center gap-2">
          <span>⚖️</span>
          <span>Verdetti precedenti ({disputes.length})</span>
        </span>
        {isOpen ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
      </Button>

      {/* Collapsible content */}
      {isOpen && (
        <div className="space-y-3 pt-1">
          {disputes.map(dispute => (
            <div key={dispute.id} className="space-y-1.5">
              {/* Dispute description */}
              <p className="text-xs text-gray-500 font-nunito px-1">
                <span className="font-medium text-gray-700">{dispute.raisedByPlayerName}</span>
                {' — '}
                {dispute.description}
              </p>

              {/* v2 badges row */}
              {(dispute.confidence || dispute.outcome) && (
                <div className="flex items-center gap-2 px-1 flex-wrap">
                  {/* Confidence badge */}
                  {dispute.confidence && (
                    <span
                      className={`inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-nunito font-medium ${
                        confidenceBadge[dispute.confidence]?.className ??
                        'bg-gray-100 text-gray-600'
                      }`}
                    >
                      {confidenceBadge[dispute.confidence]?.label ?? dispute.confidence}
                    </span>
                  )}

                  {/* Outcome badge */}
                  {dispute.outcome && dispute.outcome !== 'Pending' && (
                    <span
                      className={`inline-flex items-center px-1.5 py-0.5 rounded-full text-xs font-nunito font-medium ${
                        outcomeBadge[dispute.outcome as keyof typeof outcomeBadge]?.className ??
                        'bg-gray-100 text-gray-600'
                      }`}
                    >
                      {outcomeBadge[dispute.outcome as keyof typeof outcomeBadge]?.label ??
                        dispute.outcome}
                    </span>
                  )}

                  {/* Vote counts */}
                  {(dispute.votesAccepted !== undefined || dispute.votesRejected !== undefined) && (
                    <span className="text-xs font-nunito text-gray-500">
                      {dispute.votesAccepted ?? 0} ✓ / {dispute.votesRejected ?? 0} ✗
                    </span>
                  )}
                </div>
              )}

              {/* Override rule text */}
              {dispute.overrideRule && (
                <div className="bg-red-50 border-l-4 border-red-300 px-3 py-1.5 rounded-r-lg mx-1">
                  <p className="text-xs font-nunito text-red-800">
                    <span className="font-medium">Regola sovrascritta:</span> {dispute.overrideRule}
                  </p>
                </div>
              )}

              {/* Verdict card */}
              <ArbitroVerdictCard
                verdict={{
                  id: dispute.id,
                  verdict: dispute.verdict,
                  ruleReferences: dispute.ruleReferences,
                  note: null,
                }}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
