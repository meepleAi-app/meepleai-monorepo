'use client';

import {
  Drawer,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
  DrawerDescription,
} from '@/components/ui/drawer';

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

export function TwoFactorBottomSheet({
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
    <Drawer
      open={open}
      onOpenChange={o => {
        if (!o) onClose();
      }}
      side="mobile-bottom"
    >
      <DrawerContent>
        <DrawerHeader>
          <DrawerTitle>Set up two-factor authentication</DrawerTitle>
          <DrawerDescription>
            Complete all three steps to enable 2FA on your account.
          </DrawerDescription>
        </DrawerHeader>
        <div className="px-4 pb-6">
          <TwoFactorWizardBody setupData={setupData} onEnabled={handleEnabled} resetKey={open} />
        </div>
      </DrawerContent>
    </Drawer>
  );
}
