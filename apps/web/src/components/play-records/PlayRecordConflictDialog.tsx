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
}

export interface PlayRecordConflictDialogProps {
  open: boolean;
  labels: PlayRecordConflictDialogLabels;
  isOverwriting: boolean;
  onReload: () => void;
  onOverwrite: () => void;
}

export function PlayRecordConflictDialog({
  open,
  labels,
  isOverwriting,
  onReload,
  onOverwrite,
}: PlayRecordConflictDialogProps): React.JSX.Element {
  return (
    <Dialog open={open}>
      <DialogContent hideCloseButton>
        <DialogTitle>⚠️ {labels.title}</DialogTitle>
        <DialogDescription>{labels.description}</DialogDescription>
        <div className="flex justify-end gap-2 pt-2">
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
