'use client';

import { useState, useEffect } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, Loader2 } from 'lucide-react';
import Image from 'next/image';

import { Button } from '@/components/ui/primitives/button';
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
        setIssuedCodes(result.backupCodes ?? []);
        setStep('codes');
        void queryClient.invalidateQueries({ queryKey: ['2fa-status'] });
      } else {
        setOtpError(result.errorMessage ?? 'Invalid code. Try again.');
      }
    },
    onError: (err: Error) => setOtpError(err.message ?? 'Verification failed'),
  });

  function handleDone(): void {
    onEnabled?.();
  }

  return (
    <>
      {step === 'setup' && (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            Scan this QR code with your authenticator app (Google Authenticator, Authy, ecc.)
          </p>
          <div className="bg-card p-4 rounded-lg mx-auto w-fit border border-border">
            <Image
              data-testid="2fa-qr-code"
              src={setupData.qrCodeUrl}
              alt="2FA QR Code"
              width={192}
              height={192}
              unoptimized
            />
          </div>
          <div>
            <p className="text-sm font-medium mb-2">Or enter this code manually:</p>
            <code className="block p-2 bg-muted rounded text-sm break-all font-mono">
              {setupData.secret}
            </code>
          </div>
          <Button onClick={() => setStep('verify')} className="w-full">
            Continue
          </Button>
        </div>
      )}

      {step === 'verify' && (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground text-center">
            Enter the code from your authenticator app
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
              <Loader2 className="h-3 w-3 animate-spin mr-1" /> verifying…
            </div>
          )}
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setStep('setup')} className="flex-1">
              Back
            </Button>
          </div>
        </div>
      )}

      {step === 'codes' && (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">
            Copia o scarica i codici prima di continuare. Se perdi accesso all&apos;app di
            autenticazione, potrai usarli per rientrare nel tuo account.
          </p>
          <BackupCodesView codes={issuedCodes} acked={ack} onAck={setAck} />
          <Button onClick={handleDone} className="w-full" disabled={!ack}>
            Done
          </Button>
        </div>
      )}
    </>
  );
}
