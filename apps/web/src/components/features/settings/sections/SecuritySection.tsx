'use client';

import { useState } from 'react';

import { useQuery, useMutation } from '@tanstack/react-query';

import { useDrawerBreakpoint } from '@/components/ui/drawer';
import { api } from '@/lib/api';

import { ActiveSessionsCard } from '../two-factor/ActiveSessionsCard';
import { TwoFactorBottomSheet } from '../two-factor/TwoFactorBottomSheet';
import { TwoFactorDisableDialog } from '../two-factor/TwoFactorDisableDialog';
import { TwoFactorSetupModal } from '../two-factor/TwoFactorSetupModal';
import { TwoFactorStatusCard } from '../two-factor/TwoFactorStatusCard';

interface SetupData {
  secret: string;
  qrCodeUrl: string;
  backupCodes: string[];
}

export function SecuritySection(): React.JSX.Element {
  const [setupData, setSetupData] = useState<SetupData | null>(null);
  const [setupOpen, setSetupOpen] = useState(false);
  const [disableOpen, setDisableOpen] = useState(false);

  const breakpoint = useDrawerBreakpoint();

  const statusQuery = useQuery({
    queryKey: ['2fa-status'],
    queryFn: () => api.auth.getTwoFactorStatus(),
  });

  const setupMutation = useMutation({
    mutationFn: () => api.auth.setup2FA(),
    onSuccess: data => {
      setSetupData(data);
      setSetupOpen(true);
    },
  });

  const status = statusQuery.data ?? {
    isEnabled: false,
    enabledAt: null,
    unusedBackupCodesCount: 0,
  };

  return (
    <div className="space-y-6">
      <TwoFactorStatusCard
        status={status}
        onSetup={() => setupMutation.mutate()}
        onDisable={() => setDisableOpen(true)}
        isPending={setupMutation.isPending}
      />

      <ActiveSessionsCard />

      {breakpoint === 'mobile' ? (
        <TwoFactorBottomSheet
          open={setupOpen}
          setupData={setupData}
          onClose={() => setSetupOpen(false)}
          onEnabled={() => setSetupData(null)}
        />
      ) : (
        <TwoFactorSetupModal
          open={setupOpen}
          setupData={setupData}
          onClose={() => setSetupOpen(false)}
          onEnabled={() => setSetupData(null)}
        />
      )}

      <TwoFactorDisableDialog open={disableOpen} onClose={() => setDisableOpen(false)} />
    </div>
  );
}
