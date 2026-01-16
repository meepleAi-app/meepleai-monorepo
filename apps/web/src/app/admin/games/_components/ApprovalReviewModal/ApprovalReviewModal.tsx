'use client';

/**
 * Approval Review Modal Component (Issue #2515)
 *
 * Admin modal for reviewing and approving/rejecting game submissions.
 *
 * Features:
 * - Preview card showing how game will appear in catalog
 * - Diff view showing changes (if editing existing game)
 * - Approval notes textarea (optional)
 * - Actions: Approve & Publish, Reject with Reason, Request Changes
 */

import { useState, useEffect } from 'react';
import { CheckCircle, XCircle, AlertCircle } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Alert, AlertDescription } from '@/components/ui/alert';

import { useApiClient } from '@/lib/api/context';
import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';
import { GamePreviewCard } from '../GameEditModal/GamePreviewCard';
import { GameDiffView } from './GameDiffView';
import { useApprovalReview } from './hooks/useApprovalReview';

interface ApprovalReviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string | null;
  onSuccess?: () => void;
}

export function ApprovalReviewModal({
  isOpen,
  onClose,
  gameId,
  onSuccess,
}: ApprovalReviewModalProps) {
  const { sharedGames } = useApiClient();
  const [game, setGame] = useState<SharedGameDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('preview');

  const {
    approvalNotes,
    setApprovalNotes,
    rejectReason,
    setRejectReason,
    showRejectDialog,
    setShowRejectDialog,
    handleApprove,
    handleReject,
    isSubmitting,
  } = useApprovalReview({
    gameId,
    onSuccess: () => {
      onClose();
      if (onSuccess) onSuccess();
    },
  });

  // Load game data
  useEffect(() => {
    if (!isOpen || !gameId) {
      setGame(null);
      return;
    }

    const loadGame = async () => {
      setLoading(true);
      try {
        const data = await sharedGames.getById(gameId);
        setGame(data);
      } catch (error) {
        console.error('Failed to load game:', error);
        onClose();
      } finally {
        setLoading(false);
      }
    };

    loadGame();
  }, [isOpen, gameId, sharedGames, onClose]);

  if (!game) {
    return null;
  }

  return (
    <>
      {/* Main Review Modal */}
      <Dialog open={isOpen && !showRejectDialog} onOpenChange={(open) => !open && onClose()}>
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Review Game Submission</DialogTitle>
            <DialogDescription>
              Review game configuration before approving for publication.
            </DialogDescription>
          </DialogHeader>

          {loading ? (
            <div className="py-12 text-center">Loading game data...</div>
          ) : (
            <>
              <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
                <TabsList className="grid w-full grid-cols-2">
                  <TabsTrigger value="preview">Preview</TabsTrigger>
                  <TabsTrigger value="diff">Changes</TabsTrigger>
                </TabsList>

                <TabsContent value="preview" className="space-y-4">
                  <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription>
                      This is how the game will appear in the public catalog after approval.
                    </AlertDescription>
                  </Alert>
                  <GamePreviewCard
                    formData={{
                      name: game.title,
                      description: game.description,
                      minPlayers: game.minPlayers,
                      maxPlayers: game.maxPlayers,
                      playingTime: game.playingTimeMinutes,
                      complexity: game.complexityRating || 3,
                      tags: [], // TODO: Map from categories
                      coverImageUrl: game.imageUrl,
                    }}
                  />
                </TabsContent>

                <TabsContent value="diff" className="space-y-4">
                  <GameDiffView gameId={gameId} currentData={game} />
                </TabsContent>
              </Tabs>

              {/* Approval Notes */}
              <div className="space-y-2">
                <Label htmlFor="approval-notes">Approval Notes (Optional)</Label>
                <Textarea
                  id="approval-notes"
                  placeholder="Add any notes or comments about this approval..."
                  value={approvalNotes}
                  onChange={(e) => setApprovalNotes(e.target.value)}
                  disabled={isSubmitting}
                  rows={3}
                />
              </div>
            </>
          )}

          <DialogFooter className="gap-2">
            <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => setShowRejectDialog(true)}
              disabled={isSubmitting || loading}
            >
              <XCircle className="h-4 w-4 mr-2" />
              Reject
            </Button>
            <Button onClick={handleApprove} disabled={isSubmitting || loading}>
              <CheckCircle className="h-4 w-4 mr-2" />
              Approve & Publish
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reject Dialog */}
      <Dialog open={showRejectDialog} onOpenChange={setShowRejectDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Game Submission</DialogTitle>
            <DialogDescription>
              Please provide a reason for rejection. The editor will receive this feedback.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="reject-reason">Rejection Reason *</Label>
            <Textarea
              id="reject-reason"
              placeholder="Explain why this game submission is being rejected... (min 10 characters)"
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              disabled={isSubmitting}
              rows={4}
              minLength={10}
            />
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setShowRejectDialog(false)}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleReject} disabled={isSubmitting}>
              Confirm Rejection
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
