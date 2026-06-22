'use client';

import { useState } from 'react';

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
  useGeneratePlayRecordShareToken,
  useRevokePlayRecordShareToken,
} from '@/lib/domain-hooks/usePlayRecords';

export interface SharePlayRecordDialogProps {
  recordId: string;
  currentShareToken: string | null;
  open: boolean;
  onClose: () => void;
}

export function SharePlayRecordDialog({
  recordId,
  currentShareToken,
  open,
  onClose,
}: SharePlayRecordDialogProps): React.JSX.Element {
  const { t } = useTranslation();
  const [token, setToken] = useState<string | null>(currentShareToken);
  const generate = useGeneratePlayRecordShareToken(recordId);
  const revoke = useRevokePlayRecordShareToken(recordId);

  const shareUrl = token
    ? `${typeof window !== 'undefined' ? window.location.origin : ''}/play-records/shared/${token}`
    : '';

  const handleGenerate = async () => {
    try {
      const res = await generate.mutateAsync();
      setToken(res.shareToken);
    } catch {
      toast.error(t('playRecords.share.errorGenerate'));
    }
  };

  const handleCopy = async () => {
    if (!shareUrl) return;
    await navigator.clipboard.writeText(shareUrl);
    toast.success(t('playRecords.share.copied'));
  };

  const handleRevoke = async () => {
    try {
      await revoke.mutateAsync();
      setToken(null);
    } catch {
      toast.error(t('playRecords.share.errorRevoke'));
    }
  };

  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent>
        <DialogTitle>🔗 {t('playRecords.share.dialogTitle')}</DialogTitle>
        <DialogDescription>{t('playRecords.share.dialogDescription')}</DialogDescription>
        {!token ? (
          <Button onClick={handleGenerate} disabled={generate.isPending}>
            {generate.isPending
              ? t('playRecords.share.generating')
              : t('playRecords.share.generate')}
          </Button>
        ) : (
          <div className="flex flex-col gap-3">
            <label className="text-sm font-medium">{t('playRecords.share.urlLabel')}</label>
            <input
              readOnly
              value={shareUrl}
              className="w-full rounded-md border border-border bg-muted px-3 py-1.5 text-sm"
            />
            <div className="flex justify-between gap-2">
              <Button variant="outline" onClick={handleCopy}>
                {t('playRecords.share.copy')}
              </Button>
              <Button variant="destructive" onClick={handleRevoke} disabled={revoke.isPending}>
                {revoke.isPending ? t('playRecords.share.revoking') : t('playRecords.share.revoke')}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
