'use client';

/**
 * ScoreProposalCard - Display a score proposal with host confirm/reject actions
 *
 * Game Night Improvvisata - multi-device scoring flow.
 * Shows who proposed what score for which player. Host can confirm or reject.
 */

import { CheckCircle2, XCircle } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

// ============================================================================
// Types
// ============================================================================

export interface ScoreProposalCardProps {
  proposerName: string;
  targetPlayerName: string;
  dimension: string;
  value: number;
  round: number;
  isHost: boolean;
  onConfirm?: () => void;
  onReject?: () => void;
}

// ============================================================================
// Component
// ============================================================================

export function ScoreProposalCard({
  proposerName,
  targetPlayerName,
  dimension,
  value,
  round,
  isHost,
  onConfirm,
  onReject,
}: ScoreProposalCardProps) {
  return (
    <Card
      className="border-amber-200 bg-white/70 backdrop-blur-md"
      data-testid="score-proposal-card"
    >
      <CardContent className="px-4 py-3">
        <div className="flex items-start justify-between gap-3">
          {/* Proposal info */}
          <div className="min-w-0 flex-1 space-y-1">
            <p className="text-sm font-medium font-nunito text-gray-900">
              <span className="font-semibold">{proposerName}</span>
              {' proposes '}
              <span className="font-mono font-bold text-amber-900 bg-amber-100 px-1 rounded">
                +{value}
              </span>
              {' for '}
              <span className="font-semibold">{targetPlayerName}</span>
            </p>
            <p className="text-xs text-muted-foreground font-nunito">
              Round {round} &middot; {dimension}
            </p>
          </div>

          {/* Host actions */}
          {isHost && (
            <div className="flex shrink-0 items-center gap-1.5">
              <Button
                variant="ghost"
                size="sm"
                className="h-8 w-8 p-0 text-green-600 hover:bg-green-50 hover:text-green-700"
                onClick={onConfirm}
                aria-label="Confirm score proposal"
                data-testid="confirm-score-button"
              >
                <CheckCircle2 className="h-5 w-5" />
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="h-8 w-8 p-0 text-red-600 hover:bg-red-50 hover:text-red-700"
                onClick={onReject}
                aria-label="Reject score proposal"
                data-testid="reject-score-button"
              >
                <XCircle className="h-5 w-5" />
              </Button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

export default ScoreProposalCard;
