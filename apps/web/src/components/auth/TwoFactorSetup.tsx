/**
 * TwoFactorSetup Component (Issue #3077)
 *
 * Complete 2FA setup flow with:
 * - QR code display for authenticator app scanning
 * - Manual secret entry fallback
 * - 6-digit verification input
 * - Step-by-step guidance
 * - Accessible form controls
 * - Loading and error states
 */

import { useState, useCallback } from 'react';

import { QRCodeSVG } from 'qrcode.react';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useTranslation } from '@/hooks/useTranslation';
import type { TotpSetupResponse } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface TwoFactorSetupData {
  code: string;
}

// ============================================================================
// Component Props
// ============================================================================

export interface TwoFactorSetupProps {
  /** Setup data with QR code URL, secret, and backup codes */
  setupData: TotpSetupResponse;
  /** Called when verification code is submitted */
  onVerify: (code: string) => Promise<void> | void;
  /** Called when setup is cancelled */
  onCancel?: () => void;
  /** Loading state */
  loading?: boolean;
  /** Error message to display */
  error?: string;
  /** Clear error callback */
  onErrorDismiss?: () => void;
}

// ============================================================================
// Component
// ============================================================================

export function TwoFactorSetup({
  setupData,
  onVerify,
  onCancel,
  loading = false,
  error,
  onErrorDismiss,
}: TwoFactorSetupProps) {
  const { t } = useTranslation();
  const [verificationCode, setVerificationCode] = useState('');
  const [showManualEntry, setShowManualEntry] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [copiedSecret, setCopiedSecret] = useState(false);

  const isLoading = loading || isSubmitting;

  // Handle code input change - only digits, max 6
  const handleCodeChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value.replace(/\D/g, '').slice(0, 6);
    setVerificationCode(value);
    if (onErrorDismiss) {
      onErrorDismiss();
    }
  }, [onErrorDismiss]);

  // Handle form submission
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (verificationCode.length !== 6 || isLoading) return;

    setIsSubmitting(true);
    try {
      await onVerify(verificationCode);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Copy secret to clipboard
  const handleCopySecret = async () => {
    try {
      await navigator.clipboard.writeText(setupData.secret);
      setCopiedSecret(true);
      setTimeout(() => setCopiedSecret(false), 2000);
    } catch {
      // Fallback for browsers without clipboard API
      const textArea = document.createElement('textarea');
      textArea.value = setupData.secret;
      document.body.appendChild(textArea);
      textArea.select();
      document.execCommand('copy');
      document.body.removeChild(textArea);
      setCopiedSecret(true);
      setTimeout(() => setCopiedSecret(false), 2000);
    }
  };

  return (
    <div
      className="w-full max-w-md space-y-6"
      data-testid="two-factor-setup"
    >
      {/* Header */}
      <div className="text-center space-y-2">
        <h2 className="text-2xl font-bold text-foreground">
          {t('auth.2fa.setupTitle', 'Set Up Two-Factor Authentication')}
        </h2>
        <p className="text-sm text-muted-foreground">
          {t('auth.2fa.setupSubtitle', 'Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)')}
        </p>
      </div>

      {/* Step 1: QR Code */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-lg">
            {t('auth.2fa.step1', 'Step 1: Scan QR Code')}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* QR Code Display */}
          <div
            className="flex justify-center p-4 bg-white rounded-lg border border-border"
            data-testid="qr-code-container"
          >
            <QRCodeSVG
              value={setupData.qrCodeUrl}
              size={200}
              level="M"
              includeMargin
            />
          </div>

          {/* Manual Entry Toggle */}
          <details
            className="text-sm"
            open={showManualEntry}
            onToggle={(e) => setShowManualEntry((e.target as HTMLDetailsElement).open)}
          >
            <summary
              className="cursor-pointer text-muted-foreground hover:text-foreground"
              data-testid="manual-entry-toggle"
            >
              {t('auth.2fa.cantScan', "Can't scan? Enter manually")}
            </summary>
            <div className="mt-3 p-3 bg-muted rounded-md space-y-2">
              <Label className="text-xs text-muted-foreground">
                {t('auth.2fa.secretKey', 'Secret Key')}
              </Label>
              <div className="flex items-center gap-2">
                <code
                  className="flex-1 text-sm font-mono bg-background px-3 py-2 rounded border border-border break-all"
                  data-testid="secret-display"
                >
                  {setupData.secret}
                </code>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleCopySecret}
                  data-testid="copy-secret-button"
                >
                  {copiedSecret
                    ? t('common.copied', 'Copied!')
                    : t('common.copy', 'Copy')}
                </Button>
              </div>
            </div>
          </details>
        </CardContent>
      </Card>

      {/* Step 2: Verify */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-lg">
            {t('auth.2fa.step2', 'Step 2: Verify & Enable')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="verification-code">
                {t('auth.2fa.enterCode', 'Enter the 6-digit code from your app')}
              </Label>
              <Input
                id="verification-code"
                type="text"
                inputMode="numeric"
                autoComplete="one-time-code"
                placeholder="000000"
                value={verificationCode}
                onChange={handleCodeChange}
                maxLength={6}
                disabled={isLoading}
                className="text-center text-2xl font-mono tracking-widest"
                data-testid="verification-code-input"
              />
            </div>

            {/* Error Display */}
            {error && (
              <Alert variant="destructive" data-testid="setup-error">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            {/* Action Buttons */}
            <div className="flex gap-3">
              {onCancel && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={onCancel}
                  disabled={isLoading}
                  className="flex-1"
                  data-testid="cancel-setup-button"
                >
                  {t('common.cancel', 'Cancel')}
                </Button>
              )}
              <LoadingButton
                type="submit"
                className="flex-1"
                isLoading={isLoading}
                loadingText={t('auth.2fa.verifying', 'Verifying...')}
                disabled={verificationCode.length !== 6}
                data-testid="verify-enable-button"
              >
                {t('auth.2fa.verifyAndEnable', 'Verify & Enable')}
              </LoadingButton>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
