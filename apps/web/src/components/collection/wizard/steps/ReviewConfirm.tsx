/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

/**
 * Step 4: Review & Confirm
 * Issue #3477, #3650: Final review before adding game to collection
 *
 * UX Features:
 * - Clear game summary display
 * - Loading animation during submission
 * - Accessible button states
 */

import { CheckCircle2 } from 'lucide-react';

import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { useAddGameWizard } from '@/hooks/useAddGameWizard';

export function ReviewConfirm() {
  const { reviewSummary, isProcessing, goBack, submitWizard } = useAddGameWizard();

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-foreground dark:text-white mb-2">
          Review & Confirm
        </h2>
        <p className="text-muted-foreground">
          Review your game details before adding to your collection
        </p>
      </div>

      {/* Game Summary Card */}
      <Card className="p-6 bg-muted transition-all duration-300 hover:shadow-md">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
            <CheckCircle2 className="w-5 h-5 text-green-600 dark:text-green-400" />
          </div>
          <h3 className="text-lg font-semibold text-foreground dark:text-white">
            Game Information
          </h3>
        </div>

        <div className="space-y-3">
          {/* Game Name */}
          <div className="flex justify-between">
            <span className="text-muted-foreground">Name:</span>
            <div className="text-right">
              <span className="font-medium text-foreground dark:text-white">
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
              <span className="text-muted-foreground">Players:</span>
              <span className="font-medium text-foreground dark:text-white">
                {reviewSummary.players}
              </span>
            </div>
          )}

          {/* Play Time (Custom only) */}
          {reviewSummary.playTime && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Play Time:</span>
              <span className="font-medium text-foreground dark:text-white">
                {reviewSummary.playTime}
              </span>
            </div>
          )}

          {/* Complexity (Custom only) */}
          {reviewSummary.complexity && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Complexity:</span>
              <span className="font-medium text-foreground dark:text-white">
                {reviewSummary.complexity}
              </span>
            </div>
          )}

          {/* Divider */}
          <div className="border-t border-border my-4" />

          {/* PDF Status */}
          <div className="flex justify-between">
            <span className="text-muted-foreground">Private PDF:</span>
            {reviewSummary.hasPdf ? (
              <div className="text-right">
                <span className="font-medium text-foreground dark:text-white">
                  {reviewSummary.pdfName}
                </span>
                <span className="ml-2 text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded">
                  Uploaded ✓
                </span>
              </div>
            ) : (
              <span className="text-muted-foreground italic">No PDF uploaded</span>
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

        <Button
          onClick={submitWizard}
          disabled={isProcessing}
          className="min-w-40 transition-all duration-200 hover:scale-[1.02] active:scale-[0.98]"
        >
          {isProcessing ? (
            <>
              <Spinner size="sm" className="mr-2" />
              Adding to Collection...
            </>
          ) : (
            <>
              <CheckCircle2 className="w-4 h-4 mr-2" />
              Add to Collection
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
