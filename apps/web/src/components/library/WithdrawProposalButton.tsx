'use client';

/**
 * #5079: withdraws a private game's open proposal to the catalog. With a proposal open the game cannot lose its
 * description nor be deleted (decision of 2026-11-19), so the refusal offers this button where it appears.
 */

import { useState, type JSX } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';

interface WithdrawProposalButtonProps {
  gameId: string;
  onWithdrawn: () => void;
}

export function WithdrawProposalButton({
  gameId,
  onWithdrawn,
}: WithdrawProposalButtonProps): JSX.Element {
  const { t } = useTranslation();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const withdraw = async () => {
    setPending(true);
    setError(null);
    try {
      await api.library.withdrawPrivateGameProposal(gameId);
      onWithdrawn();
    } catch (err) {
      const inReview = err instanceof ApiError && err.code === 'proposal_in_review';
      setError(t(inReview ? 'privateGames.proposalInReview' : 'privateGames.withdrawFailed'));
    } finally {
      setPending(false);
    }
  };

  return (
    <div className="space-y-1">
      <Button type="button" variant="outline" size="sm" onClick={withdraw} disabled={pending}>
        {pending ? t('privateGames.withdrawingProposal') : t('privateGames.withdrawProposal')}
      </Button>
      {error && (
        <p role="alert" className="text-sm text-destructive-text">
          {error}
        </p>
      )}
    </div>
  );
}
