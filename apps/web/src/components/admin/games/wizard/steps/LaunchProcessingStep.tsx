'use client';

/**
 * Launch Processing Step
 * Step 4 of the Admin Game Wizard.
 * Shows summary and launches PDF processing with admin priority.
 */

import {
  ArrowLeftIcon,
  RocketIcon,
  LoaderCircleIcon,
  CheckCircle2Icon,
  FileTextIcon,
  GamepadIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

import { useLaunchAdminPdfProcessing } from '@/hooks/queries/useAdminGameWizard';

// ─── Types ───────────────────────────────────────────────────────────────────

interface LaunchProcessingStepProps {
  gameId: string;
  gameTitle: string;
  pdfDocumentId: string;
  onBack: () => void;
  onProcessingLaunched: (gameId: string) => void;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function LaunchProcessingStep({
  gameId,
  gameTitle,
  pdfDocumentId,
  onBack,
  onProcessingLaunched,
}: LaunchProcessingStepProps) {
  const launchProcessing = useLaunchAdminPdfProcessing();

  const handleLaunch = () => {
    launchProcessing.mutate(
      { gameId, pdfDocumentId },
      {
        onSuccess: () => {
          onProcessingLaunched(gameId);
        },
      }
    );
  };

  return (
    <div className="space-y-6">
      {/* Summary card */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="text-lg">Ready to Process</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-gradient-to-br from-amber-500 to-orange-600 flex items-center justify-center">
              <GamepadIcon className="h-5 w-5 text-white" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Game</p>
              <p className="font-medium">{gameTitle}</p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center">
              <FileTextIcon className="h-5 w-5 text-white" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">PDF Document</p>
              <p className="font-medium font-mono text-sm">{pdfDocumentId}</p>
            </div>
          </div>

          {/* Priority badge */}
          <div className="rounded-lg bg-amber-50 dark:bg-amber-950/20 border border-amber-200/60 dark:border-amber-800/40 p-3">
            <p className="text-sm text-amber-700 dark:text-amber-400 font-medium">
              Admin Priority
            </p>
            <p className="text-sm text-amber-600 dark:text-amber-400/80 mt-1">
              This PDF will be processed before all user-uploaded PDFs.
              The pipeline includes text extraction, chunking, embedding generation, and vector indexing.
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex items-center justify-between">
        <Button
          variant="ghost"
          onClick={onBack}
          disabled={launchProcessing.isPending}
        >
          <ArrowLeftIcon className="h-4 w-4 mr-2" />
          Back
        </Button>

        <Button
          onClick={handleLaunch}
          disabled={launchProcessing.isPending || launchProcessing.isSuccess}
          className="bg-gradient-to-r from-amber-500 to-orange-600 hover:from-amber-600 hover:to-orange-700 text-white"
        >
          {launchProcessing.isPending ? (
            <>
              <LoaderCircleIcon className="h-4 w-4 mr-2 animate-spin" />
              Launching...
            </>
          ) : launchProcessing.isSuccess ? (
            <>
              <CheckCircle2Icon className="h-4 w-4 mr-2" />
              Processing Launched!
            </>
          ) : (
            <>
              <RocketIcon className="h-4 w-4 mr-2" />
              Launch Processing
            </>
          )}
        </Button>
      </div>

      {/* Error state */}
      {launchProcessing.isError && (
        <div className="rounded-lg border border-red-200 dark:border-red-900/40 bg-red-50 dark:bg-red-950/20 p-3">
          <p className="text-sm text-red-700 dark:text-red-400">
            {launchProcessing.error.message}
          </p>
        </div>
      )}
    </div>
  );
}
