'use client';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';

import { TwoFactorWizardBody } from './TwoFactorWizardBody';

interface SetupData {
  readonly secret: string;
  readonly qrCodeUrl: string;
  readonly backupCodes: readonly string[];
}

interface Props {
  readonly open: boolean;
  readonly setupData: SetupData | null;
  readonly onClose: () => void;
  readonly onEnabled?: () => void;
}

export function TwoFactorSetupModal({
  open,
  setupData,
  onClose,
  onEnabled,
}: Props): React.JSX.Element {
  if (!setupData) return <></>;

  function handleEnabled(): void {
    onEnabled?.();
    onClose();
  }

  return (
    <Dialog
      open={open}
      onOpenChange={o => {
        if (!o) onClose();
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Set up two-factor authentication</DialogTitle>
          <DialogDescription>
            Complete all three steps to enable 2FA on your account.
          </DialogDescription>
        </DialogHeader>
        <TwoFactorWizardBody setupData={setupData} onEnabled={handleEnabled} resetKey={open} />
      </DialogContent>
    </Dialog>
  );
}
