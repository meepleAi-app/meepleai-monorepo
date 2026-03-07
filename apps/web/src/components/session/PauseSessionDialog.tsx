'use client';

/**
 * PauseSessionDialog - Intermediate dialog prompting photo upload before pausing.
 * Issue #5372 - Session pause flow photo prompt.
 * Epic #5358 - Session Photo Attachments (Phase 0).
 */

import { useState, useCallback } from 'react';

import { Camera, SkipForward } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';

import { PhotoUploadModal, type SessionAttachmentDto } from './PhotoUploadModal';

export interface PauseSessionDialogProps {
  /** Whether this dialog is open. */
  open: boolean;
  /** Called to open/close the dialog. */
  onOpenChange: (open: boolean) => void;
  /** Session ID for the upload. */
  sessionId: string;
  /** Current player ID. */
  playerId: string;
  /** Called when the session should be paused (after upload or skip). */
  onPause: () => void;
  /** Called when a photo is uploaded during the pause flow. */
  onUploadComplete?: (attachment: SessionAttachmentDto) => void;
  /** Current snapshot index. */
  snapshotIndex?: number;
}

export function PauseSessionDialog({
  open,
  onOpenChange,
  sessionId,
  playerId,
  onPause,
  onUploadComplete,
  snapshotIndex,
}: PauseSessionDialogProps) {
  const [showUpload, setShowUpload] = useState(false);

  const handleSkip = useCallback(() => {
    onOpenChange(false);
    onPause();
  }, [onOpenChange, onPause]);

  const handleUploadClick = useCallback(() => {
    setShowUpload(true);
  }, []);

  const handleUploadComplete = useCallback(
    (attachment: SessionAttachmentDto) => {
      onUploadComplete?.(attachment);
      setShowUpload(false);
      onOpenChange(false);
      onPause();
    },
    [onUploadComplete, onOpenChange, onPause]
  );

  const handleUploadCancel = useCallback(() => {
    setShowUpload(false);
  }, []);

  // Reset upload state when dialog closes
  const handleOpenChange = useCallback(
    (isOpen: boolean) => {
      if (!isOpen) {
        setShowUpload(false);
      }
      onOpenChange(isOpen);
    },
    [onOpenChange]
  );

  return (
    <>
      <Dialog open={open && !showUpload} onOpenChange={handleOpenChange}>
        <DialogContent className="max-w-sm" data-testid="pause-session-dialog">
          <DialogTitle className="sr-only">Pause session</DialogTitle>
          <div className="flex flex-col items-center text-center space-y-4 py-2">
            {/* Icon */}
            <div className="flex h-14 w-14 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
              <Camera className="h-7 w-7 text-amber-600 dark:text-amber-400" aria-hidden="true" />
            </div>

            {/* Title & description */}
            <div className="space-y-1.5">
              <h2 className="text-lg font-semibold" data-testid="pause-dialog-title">
                Save board state before pausing?
              </h2>
              <p className="text-sm text-muted-foreground">
                Upload photos of the game board and player areas so everyone can restore the game
                next time.
              </p>
            </div>

            {/* Actions */}
            <div className="flex flex-col gap-2 w-full pt-2">
              <Button
                onClick={handleUploadClick}
                className="bg-amber-600 hover:bg-amber-700 text-white w-full"
                data-testid="upload-photos-button"
              >
                <Camera className="mr-1.5 h-4 w-4" aria-hidden="true" />
                Upload Photos
              </Button>
              <Button
                variant="outline"
                onClick={handleSkip}
                className="w-full"
                data-testid="skip-pause-button"
              >
                <SkipForward className="mr-1.5 h-4 w-4" aria-hidden="true" />
                Skip &amp; Pause
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* Photo upload modal (shown when user clicks "Upload Photos") */}
      <PhotoUploadModal
        sessionId={sessionId}
        playerId={playerId}
        snapshotIndex={snapshotIndex}
        open={showUpload}
        onOpenChange={open => {
          if (!open) handleUploadCancel();
        }}
        onUploadComplete={handleUploadComplete}
      />
    </>
  );
}
