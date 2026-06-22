'use client';

import type React from 'react';

import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import {
  usePlayRecordVersions,
  useRestorePlayRecordVersion,
} from '@/lib/domain-hooks/usePlayRecords';

export interface PlayRecordHistoryDialogProps {
  recordId: string;
  open: boolean;
  onClose: () => void;
}

export function PlayRecordHistoryDialog({
  recordId,
  open,
  onClose,
}: PlayRecordHistoryDialogProps): React.JSX.Element {
  const { t } = useTranslation();
  const { data: versions, isLoading } = usePlayRecordVersions(recordId, open);
  const restore = useRestorePlayRecordVersion(recordId);

  const handleRestore = async (versionNumber: number) => {
    try {
      await restore.mutateAsync(versionNumber);
      toast.success(t('playRecords.history.restored'));
      onClose();
    } catch {
      toast.error(t('playRecords.history.errorRestore'));
    }
  };

  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent>
        <DialogTitle>🕘 {t('playRecords.history.dialogTitle')}</DialogTitle>
        <DialogDescription>{t('playRecords.history.dialogDescription')}</DialogDescription>
        {isLoading ? null : !versions || versions.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('playRecords.history.empty')}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {versions.map(v => (
              <li
                key={v.versionNumber}
                className="flex items-center justify-between gap-3 rounded-md border border-border bg-card px-3 py-2"
              >
                <div className="min-w-0 text-sm">
                  <span className="font-bold text-foreground">
                    {t('playRecords.history.versionLabel', { n: v.versionNumber })}
                  </span>
                  <span className="ml-2 text-muted-foreground">
                    {new Date(v.createdAt).toLocaleDateString()}
                  </span>
                  {v.notes && (
                    <span className="ml-2 block truncate text-xs text-muted-foreground">
                      {v.notes}
                    </span>
                  )}
                </div>
                <Button
                  variant="outline"
                  onClick={() => handleRestore(v.versionNumber)}
                  disabled={restore.isPending}
                >
                  {restore.isPending
                    ? t('playRecords.history.restoring')
                    : t('playRecords.history.restore')}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </DialogContent>
    </Dialog>
  );
}
