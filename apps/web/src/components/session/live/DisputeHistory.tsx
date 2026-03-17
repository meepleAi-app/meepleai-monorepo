/**
 * DisputeHistory
 *
 * Game Night Improvvisata — Task 17
 *
 * Collapsible section listing all rule dispute verdicts for the current session.
 * When `gameId` is provided, it could fetch cross-session history (future extension);
 * for now it reads from the live-session-store.
 */

'use client';

import { useState } from 'react';

import { ChevronDown, ChevronUp } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { ArbitroVerdictCard } from './ArbitroVerdictCard';

interface DisputeHistoryProps {
  sessionId: string;
  /** Reserved for future cross-session history retrieval */

  gameId?: string;
}

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
