'use client';

import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

export interface PlayRecordConflictDialogLabels {
  title: string;
  description: string;
  reload: string;
  overwrite: string;
  continueEditing: string;
}

export interface PlayRecordConflictDialogProps {
  open: boolean;
  labels: PlayRecordConflictDialogLabels;
  isOverwriting: boolean;
  onReload: () => void;
  onOverwrite: () => void;
  /** Leave the dialog without choosing: the form keeps what the user typed (#4805). */
  onContinueEditing: () => void;
}

export function PlayRecordConflictDialog({
  open,
  labels,
  isOverwriting,
  onReload,
  onOverwrite,
  onContinueEditing,
}: PlayRecordConflictDialogProps): React.JSX.Element {
  return (
    // ESC and a click outside are the same way out as the explicit button — except while a read or a save
    // is in flight, when leaving would orphan it.
    <Dialog
      open={open}
      onOpenChange={next => {
        if (!next && !isOverwriting) onContinueEditing();
      }}
    >
      <DialogContent hideCloseButton>
        <DialogTitle>⚠️ {labels.title}</DialogTitle>
        <DialogDescription>{labels.description}</DialogDescription>
        <div className="flex flex-wrap justify-end gap-2 pt-2">
          <Button variant="ghost" onClick={onContinueEditing} disabled={isOverwriting}>
            {labels.continueEditing}
          </Button>
          <Button variant="outline" onClick={onReload} disabled={isOverwriting}>
            {labels.reload}
          </Button>
          <Button variant="destructive" onClick={onOverwrite} disabled={isOverwriting}>
            {labels.overwrite}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
