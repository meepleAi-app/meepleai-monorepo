/**
 * Security Settings Page (Issue #4116)
 * 2FA setup and management
 */

'use client';

import React, { useState } from 'react';

import { Shield, Download } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

export default function SecuritySettingsPage() {
  const [is2FAEnabled, setIs2FAEnabled] = useState(false);
  const [showSetupWizard, setShowSetupWizard] = useState(false);
  const [wizardStep, setWizardStep] = useState(1);
  const [verificationCode, setVerificationCode] = useState('');

  // TODO: Fetch actual 2FA status from API
  // const { data: status } = useQuery(['2fa-status'], () => fetch('/api/v1/users/me/2fa/status'));

  const recoveryCodes = [
    'ABCD-1234-EFGH',
    'IJKL-5678-MNOP',
    'QRST-9012-UVWX',
    'YZAB-3456-CDEF',
    'GHIJ-7890-KLMN',
  ];

  return (
    <div className="container max-w-2xl mx-auto py-8">
      <h1 className="text-3xl font-bold mb-8">Security Settings</h1>

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
                  is2FAEnabled
                    ? 'bg-green-100 text-green-700'
                    : 'bg-gray-100 text-gray-700'
                }`}
              >
                {is2FAEnabled ? 'Enabled' : 'Disabled'}
              </div>

              {!is2FAEnabled ? (
                <Button onClick={() => setShowSetupWizard(true)}>Enable 2FA</Button>
              ) : (
                <Button variant="destructive" onClick={() => setIs2FAEnabled(false)}>
                  Disable 2FA
                </Button>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* 2FA Setup Wizard */}
      <Dialog open={showSetupWizard} onOpenChange={setShowSetupWizard}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Setup Two-Factor Authentication</DialogTitle>
            <DialogDescription>Step {wizardStep} of 3</DialogDescription>
          </DialogHeader>

          {/* Step 1: QR Code */}
          {wizardStep === 1 && (
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)
              </p>

              {/* Placeholder QR code - replace with actual QR */}
              <div className="bg-white p-4 rounded-lg mx-auto w-fit">
                <div className="w-48 h-48 bg-gray-200 flex items-center justify-center">
                  QR Code Here
                  {/* TODO: Use qrcode.react library */}
                </div>
              </div>

              <div>
                <p className="text-sm font-medium mb-2">Or enter this code manually:</p>
                <code className="block p-2 bg-muted rounded text-sm">
                  JBSWY3DPEHPK3PXP
                </code>
              </div>

              <Button onClick={() => setWizardStep(2)} className="w-full">
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
                onChange={e => setVerificationCode(e.target.value)}
                className="text-center text-2xl tracking-widest"
              />

              <div className="flex gap-2">
                <Button variant="outline" onClick={() => setWizardStep(1)} className="flex-1">
                  Back
                </Button>
                <Button onClick={() => setWizardStep(3)} className="flex-1">
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
                  // TODO: Download codes
                  const text = recoveryCodes.join('\n');
                  const blob = new Blob([text], { type: 'text/plain' });
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = '2fa-recovery-codes.txt';
                  a.click();
                }}
              >
                <Download className="h-4 w-4" />
                Download Codes
              </Button>

              <Button
                onClick={() => {
                  setIs2FAEnabled(true);
                  setShowSetupWizard(false);
                  setWizardStep(1);
                }}
                className="w-full"
              >
                Complete Setup
              </Button>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
