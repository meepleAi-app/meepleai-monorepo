/**
 * Security Settings Page (Issue #4116)
 * 2FA setup and management
 */

'use client';

import React, { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Shield, Download, Loader2, AlertCircle } from 'lucide-react';
import Image from 'next/image';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';

export default function SecuritySettingsPage() {
  const queryClient = useQueryClient();

  const [showSetupWizard, setShowSetupWizard] = useState(false);
  const [wizardStep, setWizardStep] = useState(1);
  const [verificationCode, setVerificationCode] = useState('');

  // Setup data stored locally during the wizard flow
  const [setupData, setSetupData] = useState<{
    secret: string;
    qrCodeUrl: string;
    backupCodes: string[];
  } | null>(null);

  // Recovery codes from enable2FA response (shown in step 3)
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);

  // Error message for user feedback
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Fetch 2FA status
  const { data: twoFactorStatus, isLoading: statusLoading } = useQuery({
    queryKey: ['2fa-status'],
    queryFn: () => api.auth.getTwoFactorStatus(),
  });
  const is2FAEnabled = twoFactorStatus?.isEnabled ?? false;

  // Setup 2FA mutation (step 1: get QR code + secret)
  const setupMutation = useMutation({
    mutationFn: () => api.auth.setup2FA(),
    onSuccess: data => {
      setSetupData(data);
      setErrorMessage(null);
      setWizardStep(1);
      setVerificationCode('');
      setShowSetupWizard(true);
    },
    onError: (error: Error) => {
      setErrorMessage(error.message || 'Failed to start 2FA setup');
    },
  });

  // Enable 2FA mutation (step 2: verify TOTP code)
  const enableMutation = useMutation({
    mutationFn: (code: string) => api.auth.enable2FA(code),
    onSuccess: result => {
      if (result.success) {
        setRecoveryCodes(result.backupCodes ?? []);
        setErrorMessage(null);
        setWizardStep(3);
        queryClient.invalidateQueries({ queryKey: ['2fa-status'] });
      } else {
        setErrorMessage(result.errorMessage || 'Verification failed. Please try again.');
      }
    },
    onError: (error: Error) => {
      setErrorMessage(error.message || 'Failed to verify code');
    },
  });

  // Disable 2FA mutation
  const disableMutation = useMutation({
    mutationFn: () => api.auth.disable2FA('', ''),
    onSuccess: result => {
      if (result.success) {
        setErrorMessage(null);
        queryClient.invalidateQueries({ queryKey: ['2fa-status'] });
      } else {
        setErrorMessage(result.errorMessage || 'Failed to disable 2FA');
      }
    },
    onError: (error: Error) => {
      setErrorMessage(error.message || 'Failed to disable 2FA');
    },
  });

  const handleEnableClick = () => {
    setErrorMessage(null);
    setupMutation.mutate();
  };

  const handleVerify = () => {
    if (verificationCode.length !== 6) {
      setErrorMessage('Please enter a 6-digit code');
      return;
    }
    setErrorMessage(null);
    enableMutation.mutate(verificationCode);
  };

  const handleCompleteSetup = () => {
    setShowSetupWizard(false);
    setWizardStep(1);
    setVerificationCode('');
    setSetupData(null);
    setRecoveryCodes([]);
    setErrorMessage(null);
  };

  // Loading state
  if (statusLoading) {
    return (
      <div className="container max-w-2xl mx-auto py-8">
        <h1 className="text-3xl font-bold mb-8">Security Settings</h1>
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      </div>
    );
  }

  return (
    <div className="container max-w-2xl mx-auto py-8">
      <h1 className="text-3xl font-bold mb-8">Security Settings</h1>

      {/* Page-level error message */}
      {errorMessage && !showSetupWizard && (
        <div className="flex items-center gap-2 p-3 mb-6 rounded-lg bg-destructive/10 text-destructive text-sm">
          <AlertCircle className="h-4 w-4 shrink-0" />
          <span>{errorMessage}</span>
        </div>
      )}

      {/* 2FA Section */}
      <div className="bg-card rounded-xl p-6 border border-border/50 mb-6">
        <div className="flex items-start gap-4">
          <Shield className="h-8 w-8 text-primary mt-1" />
          <div className="flex-1">
            <h2 className="text-xl font-bold mb-2">Two-Factor Authentication</h2>
            <p className="text-muted-foreground mb-4">
              Add an extra layer of security to your account
            </p>

            <div className="flex items-center gap-3">
              <div
                className={`px-3 py-1 rounded-full text-sm font-medium ${
                  is2FAEnabled ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-700'
                }`}
              >
                {is2FAEnabled ? 'Enabled' : 'Disabled'}
              </div>

              {!is2FAEnabled ? (
                <Button onClick={handleEnableClick} disabled={setupMutation.isPending}>
                  {setupMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Enable 2FA
                </Button>
              ) : (
                <Button
                  variant="destructive"
                  onClick={() => disableMutation.mutate()}
                  disabled={disableMutation.isPending}
                >
                  {disableMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Disable 2FA
                </Button>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* 2FA Setup Wizard */}
      <Dialog
        open={showSetupWizard}
        onOpenChange={open => {
          if (!open) handleCompleteSetup();
        }}
      >
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Setup Two-Factor Authentication</DialogTitle>
            <DialogDescription>Step {wizardStep} of 3</DialogDescription>
          </DialogHeader>

          {/* Wizard-level error message */}
          {errorMessage && (
            <div className="flex items-center gap-2 p-3 rounded-lg bg-destructive/10 text-destructive text-sm">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          {/* Step 1: QR Code */}
          {wizardStep === 1 && setupData && (
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)
              </p>

              <div className="bg-white p-4 rounded-lg mx-auto w-fit">
                <Image
                  src={setupData.qrCodeUrl}
                  alt="2FA QR Code"
                  width={192}
                  height={192}
                  unoptimized
                />
              </div>

              <div>
                <p className="text-sm font-medium mb-2">Or enter this code manually:</p>
                <code className="block p-2 bg-muted rounded text-sm break-all">
                  {setupData.secret}
                </code>
              </div>

              <Button
                onClick={() => {
                  setErrorMessage(null);
                  setWizardStep(2);
                }}
                className="w-full"
              >
                Continue
              </Button>
            </div>
          )}

          {/* Step 2: Verify Code */}
          {wizardStep === 2 && (
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Enter the 6-digit code from your authenticator app
              </p>

              <Input
                type="text"
                placeholder="000000"
                maxLength={6}
                value={verificationCode}
                onChange={e => setVerificationCode(e.target.value.replace(/[^0-9]/g, ''))}
                className="text-center text-2xl tracking-widest"
              />

              <div className="flex gap-2">
                <Button
                  variant="outline"
                  onClick={() => {
                    setErrorMessage(null);
                    setWizardStep(1);
                  }}
                  className="flex-1"
                >
                  Back
                </Button>
                <Button
                  onClick={handleVerify}
                  className="flex-1"
                  disabled={enableMutation.isPending}
                >
                  {enableMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Verify
                </Button>
              </div>
            </div>
          )}

          {/* Step 3: Recovery Codes */}
          {wizardStep === 3 && (
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Save these recovery codes in a safe place. You can use them to access your account
                if you lose your phone.
              </p>

              <div className="bg-muted p-4 rounded-lg space-y-1 font-mono text-sm">
                {recoveryCodes.map((code, i) => (
                  <div key={i}>{code}</div>
                ))}
              </div>

              <Button
                variant="outline"
                className="w-full gap-2"
                onClick={() => {
                  const text = recoveryCodes.join('\n');
                  const blob = new Blob([text], { type: 'text/plain' });
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = '2fa-recovery-codes.txt';
                  a.click();
                  URL.revokeObjectURL(url);
                }}
              >
                <Download className="h-4 w-4" />
                Download Codes
              </Button>

              <Button onClick={handleCompleteSetup} className="w-full">
                Complete Setup
              </Button>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
