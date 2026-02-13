'use client';

/**
 * Step 5: Request Approval (Editor Wizard)
 * Issue #6: Editor Proposal Wizard
 *
 * Creates ShareRequest for admin approval instead of direct publish.
 */

import { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { Card } from '@/components/ui/data-display/card';
import { Textarea } from '@/components/ui/primitives/textarea';
import { Label } from '@/components/ui/primitives/label';
import { Spinner } from '@/components/loading';
import { toast } from '@/components/layout';
import { useCreateShareRequest } from '@/hooks/queries/useShareRequests';

interface RequestApprovalStepProps {
  gameId: string;
  gameName: string;
  pdfId: string;
  pdfFileName: string;
  onComplete: () => void;
  onBack: () => void;
}

export function RequestApprovalStep({
  gameId,
  gameName,
  pdfId,
  pdfFileName,
  onComplete,
  onBack,
}: RequestApprovalStepProps) {
  const [notes, setNotes] = useState('');
  const createShareRequest = useCreateShareRequest();

  const handleSubmit = async () => {
    try {
      await createShareRequest.mutateAsync({
        sourceGameId: gameId,
        notes: notes || undefined,
        attachedDocumentIds: [pdfId],
      });

      toast.success(`Proposta inviata per "${gameName}"!`);
      onComplete();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Errore: ${message}`);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Request Approval
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Submit your game proposal for admin review.
        </p>
      </div>

      {/* Summary */}
      <Card className="p-4 bg-green-50 dark:bg-green-900/20 border-green-200 dark:border-green-800">
        <h3 className="font-semibold text-green-800 dark:text-green-200 mb-3">
          ✓ Ready to Submit
        </h3>
        <div className="space-y-2 text-sm text-green-700 dark:text-green-300">
          <div className="flex justify-between">
            <span>Game:</span>
            <span className="font-medium">{gameName}</span>
          </div>
          <div className="flex justify-between">
            <span>PDF:</span>
            <span className="font-medium">{pdfFileName}</span>
          </div>
          <div className="flex justify-between">
            <span>Agent:</span>
            <span className="font-medium">Configured ✓</span>
          </div>
        </div>
      </Card>

      {/* Notes for Admin */}
      <div className="space-y-2">
        <Label htmlFor="notes">
          Notes for Admin (Optional)
        </Label>
        <Textarea
          id="notes"
          value={notes}
          onChange={e => setNotes(e.target.value)}
          placeholder="Add any context or special instructions for the reviewer..."
          rows={4}
          maxLength={1000}
        />
        <p className="text-xs text-slate-500">
          {notes.length}/1000 characters
        </p>
      </div>

      {/* Info */}
      <Card className="p-4 bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800">
        <p className="text-sm text-blue-800 dark:text-blue-200">
          💡 Your proposal will be reviewed by an admin. You'll be notified when it's approved or if changes are requested.
        </p>
      </Card>

      {/* Actions */}
      <div className="flex justify-between gap-3">
        <Button variant="outline" onClick={onBack} disabled={createShareRequest.isPending}>
          ← Back
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={createShareRequest.isPending}
          className="bg-green-600 hover:bg-green-700 min-w-32"
        >
          {createShareRequest.isPending ? (
            <>
              <Spinner size="sm" className="mr-2" />
              Submitting...
            </>
          ) : (
            'Submit for Approval ✓'
          )}
        </Button>
      </div>
    </div>
  );
}
