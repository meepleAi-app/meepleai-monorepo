'use client';

/**
 * Step 4: Review & Confirm
 * Issue #3477: Final review before adding game to collection
 */

import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { useAddGameWizard } from '@/hooks/useAddGameWizard';

export function ReviewConfirm() {
  const { reviewSummary, isProcessing, goBack, submitWizard } = useAddGameWizard();

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Review & Confirm
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Review your game details before adding to your collection
        </p>
      </div>

      {/* Game Summary Card */}
      <Card className="p-6 bg-slate-50 dark:bg-slate-800/50">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-white mb-4">
          Game Information
        </h3>

        <div className="space-y-3">
          {/* Game Name */}
          <div className="flex justify-between">
            <span className="text-slate-600 dark:text-slate-400">Name:</span>
            <div className="text-right">
              <span className="font-medium text-slate-900 dark:text-white">
                {reviewSummary.gameName}
              </span>
              {reviewSummary.isCustom && (
                <span className="ml-2 text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded">
                  Custom
                </span>
              )}
            </div>
          </div>

          {/* Players (Custom only) */}
          {reviewSummary.players && (
            <div className="flex justify-between">
              <span className="text-slate-600 dark:text-slate-400">Players:</span>
              <span className="font-medium text-slate-900 dark:text-white">
                {reviewSummary.players}
              </span>
            </div>
          )}

          {/* Play Time (Custom only) */}
          {reviewSummary.playTime && (
            <div className="flex justify-between">
              <span className="text-slate-600 dark:text-slate-400">Play Time:</span>
              <span className="font-medium text-slate-900 dark:text-white">
                {reviewSummary.playTime}
              </span>
            </div>
          )}

          {/* Complexity (Custom only) */}
          {reviewSummary.complexity && (
            <div className="flex justify-between">
              <span className="text-slate-600 dark:text-slate-400">Complexity:</span>
              <span className="font-medium text-slate-900 dark:text-white">
                {reviewSummary.complexity}
              </span>
            </div>
          )}

          {/* Divider */}
          <div className="border-t border-slate-200 dark:border-slate-700 my-4" />

          {/* PDF Status */}
          <div className="flex justify-between">
            <span className="text-slate-600 dark:text-slate-400">Private PDF:</span>
            {reviewSummary.hasPdf ? (
              <div className="text-right">
                <span className="font-medium text-slate-900 dark:text-white">
                  {reviewSummary.pdfName}
                </span>
                <span className="ml-2 text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded">
                  Uploaded ✓
                </span>
              </div>
            ) : (
              <span className="text-slate-500 dark:text-slate-400 italic">No PDF uploaded</span>
            )}
          </div>
        </div>
      </Card>

      {/* Info Box */}
      <div className="p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
        <p className="text-sm text-blue-900 dark:text-blue-100">
          <strong>What happens next?</strong> This game will be added to your personal collection.
          {reviewSummary.hasPdf
            ? ' Your private PDF will be available for reference anytime.'
            : ' You can always add a PDF later from your collection dashboard.'}
        </p>
      </div>

      {/* Actions */}
      <div className="flex justify-between gap-3">
        <Button variant="outline" onClick={goBack} disabled={isProcessing}>
          ← Back
        </Button>

        <Button onClick={submitWizard} disabled={isProcessing} className="min-w-40">
          {isProcessing ? (
            <>
              <Spinner size="sm" className="mr-2" />
              Adding to Collection...
            </>
          ) : (
            'Add to Collection'
          )}
        </Button>
      </div>
    </div>
  );
}
