'use client';

import {
  Drawer,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
  DrawerDescription,
} from '@/components/ui/drawer';
import { useTranslation } from '@/hooks/useTranslation';

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
  const { t } = useTranslation();
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
          <DrawerTitle>{t('settings.security.setup.title')}</DrawerTitle>
          <DrawerDescription>{t('settings.security.setup.description')}</DrawerDescription>
        </DrawerHeader>
        <div className="px-4 pb-6">
          <TwoFactorWizardBody setupData={setupData} onEnabled={handleEnabled} resetKey={open} />
        </div>
      </DrawerContent>
    </Drawer>
  );
}
