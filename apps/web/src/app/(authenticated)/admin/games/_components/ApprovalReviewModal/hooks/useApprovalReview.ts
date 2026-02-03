/**
 * useApprovalReview Hook (Issue #2515)
 *
 * Manages approval workflow state and actions.
 * Handles approve/reject operations with validation.
 */

import { useState } from 'react';

import { toast } from 'sonner';

import { useApiClient } from '@/lib/api/context';
import { RejectPublicationRequestSchema } from '@/lib/api/schemas/shared-games.schemas';

interface UseApprovalReviewParams {
  gameId: string | null;
  onSuccess?: () => void;
}

export function useApprovalReview({ gameId, onSuccess }: UseApprovalReviewParams) {
  const { sharedGames } = useApiClient();

  const [approvalNotes, setApprovalNotes] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [showRejectDialog, setShowRejectDialog] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleApprove = async () => {
    if (!gameId) return;

    setIsSubmitting(true);
    try {
      await sharedGames.approvePublication(gameId);
      toast.success('Game approved and published successfully');
      if (onSuccess) onSuccess();
    } catch (error) {
      console.error('Failed to approve game:', error);
      toast.error('Failed to approve game publication');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReject = async () => {
    if (!gameId) return;

    // Validate reason
    const validation = RejectPublicationRequestSchema.safeParse({ reason: rejectReason });
    if (!validation.success) {
      toast.error('Rejection reason must be at least 10 characters');
      return;
    }

    setIsSubmitting(true);
    try {
      await sharedGames.rejectPublication(gameId, rejectReason);
      toast.success('Game submission rejected. Editor will be notified.');
      setShowRejectDialog(false);
      if (onSuccess) onSuccess();
    } catch (error) {
      console.error('Failed to reject game:', error);
      toast.error('Failed to reject game submission');
    } finally {
      setIsSubmitting(false);
    }
  };

  return {
    approvalNotes,
    setApprovalNotes,
    rejectReason,
    setRejectReason,
    showRejectDialog,
    setShowRejectDialog,
    handleApprove,
    handleReject,
    isSubmitting,
  };
}
