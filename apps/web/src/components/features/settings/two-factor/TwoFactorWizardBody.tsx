'use client';

import { useState, useEffect } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, Loader2 } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';

import { BackupCodesView } from './BackupCodesView';
import { OTPInput6Slot } from './OTPInput6Slot';

interface SetupData {
  readonly secret: string;
  readonly qrCodeUrl: string;
  readonly backupCodes: readonly string[];
}

interface Props {
  readonly setupData: SetupData;
  readonly onEnabled?: () => void;
  readonly onCancel?: () => void;
  /** When true the wizard state resets (driven by the parent open flag). */
  readonly resetKey?: boolean;
}

type Step = 'setup' | 'verify' | 'codes';

export function TwoFactorWizardBody({ setupData, onEnabled, resetKey }: Props): React.JSX.Element {
  const [step, setStep] = useState<Step>('setup');
  const [otpError, setOtpError] = useState<string | null>(null);
  const [ack, setAck] = useState(false);
  const [issuedCodes, setIssuedCodes] = useState<string[]>([]);
  const queryClient = useQueryClient();
  const { t } = useTranslation();

  // Reset internal state when parent closes / reopens (resetKey flips with the open flag)
  useEffect(() => {
    if (resetKey === false) {
      setStep('setup');
      setOtpError(null);
      setAck(false);
      setIssuedCodes([]);
    }
  }, [resetKey]);

  const enable = useMutation({
    mutationFn: (code: string) => api.auth.enable2FA(code),
    onSuccess: result => {
      if (result.success) {
        setOtpError(null);
        // Backup codes are returned in PLAINTEXT by the SETUP endpoint
        // (TotpSetupResponse.BackupCodes) and surface here via `setupData.backupCodes`.
        // The enable endpoint does NOT re-emit them — `Enable2FACommandHandler`
        // returns `Success: true` only, and the persisted backup codes are PBKDF2
        // hashed (one-way), so they cannot be reconstructed post-hash.
        // Prefer `result.backupCodes` if the BE ever populates it; otherwise fall
        // back to the trusted source already in the wizard's local state.
        const codesFromEnable = result.backupCodes ?? [];
        setIssuedCodes(
          codesFromEnable.length > 0 ? codesFromEnable : [...(setupData.backupCodes ?? [])]
        );
        setStep('codes');
        void queryClient.invalidateQueries({ queryKey: ['2fa-status'] });
      } else {
        // Surfaces the BE error text verbatim (incl. a "locked_out" message when the TOTP
        // attempt limit is hit). A live mm:ss countdown banner (plan G2) is deferred:
        // Enable2FAResult has no `retryAfterSeconds` field — only the step-up endpoint exposes
        // it. Revisit here if the enable endpoint starts returning the retry window.
        setOtpError(result.errorMessage ?? t('settings.security.wizard.invalidCode'));
      }
    },
    onError: (err: Error) =>
      setOtpError(err.message ?? t('settings.security.wizard.verificationFailed')),
  });

  function handleDone(): void {
    onEnabled?.();
  }

  return (
    <>
      {step === 'setup' && (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {t('settings.security.wizard.scanInstruction')}
          </p>
          {/*
           * BE returns an otpauth:// URI (RFC 6238) — NOT a PNG. We render the QR
           * client-side via qrcode.react (same pattern as TwoFactorSetup.tsx +
           * InviteModal). Wrap with data-testid on the div so e2e selectors stay
           * stable regardless of the underlying SVG markup the lib emits.
           */}
          <div
            data-testid="2fa-qr-code"
            className="bg-card p-4 rounded-lg mx-auto w-fit border border-border"
          >
            <QRCodeSVG
              value={setupData.qrCodeUrl}
              size={192}
              level="M"
              aria-label={t('settings.security.wizard.qrAriaLabel')}
            />
          </div>
          <div>
            <p className="text-sm font-medium mb-2">{t('settings.security.wizard.manualEntry')}</p>
            <code className="block p-2 bg-muted rounded text-sm break-all font-mono">
              {setupData.secret}
            </code>
          </div>
          <Button onClick={() => setStep('verify')} className="w-full">
            {t('settings.security.wizard.continue')}
          </Button>
        </div>
      )}

      {step === 'verify' && (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground text-center">
            {t('settings.security.wizard.enterCode')}
          </p>
          <OTPInput6Slot
            onComplete={code => {
              setOtpError(null);
              enable.mutate(code);
            }}
            error={otpError !== null}
            disabled={enable.isPending}
          />
          {otpError && (
            <div
              className="flex items-center gap-2 p-2 rounded-md bg-destructive/10 text-destructive text-sm"
              role="alert"
            >
              <AlertCircle className="h-4 w-4 shrink-0" aria-hidden />
              <span>{otpError}</span>
            </div>
          )}
          {enable.isPending && (
            <div className="flex items-center justify-center text-muted-foreground text-xs">
              <Loader2 className="h-3 w-3 animate-spin mr-1" />{' '}
              {t('settings.security.wizard.verifying')}
            </div>
          )}
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setStep('setup')} className="flex-1">
              {t('settings.security.wizard.back')}
            </Button>
          </div>
        </div>
      )}

      {step === 'codes' && (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            {t('settings.security.wizard.codesIntro')}
          </p>
          <BackupCodesView codes={issuedCodes} acked={ack} onAck={setAck} />
          <Button onClick={handleDone} className="w-full" disabled={!ack}>
            {t('settings.security.wizard.done')}
          </Button>
        </div>
      )}
    </>
  );
}
