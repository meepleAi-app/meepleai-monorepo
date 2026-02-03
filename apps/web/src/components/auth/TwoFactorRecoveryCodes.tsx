/**
 * TwoFactorRecoveryCodes Component (Issue #3077)
 *
 * Displays backup/recovery codes with:
 * - Grid layout for codes
 * - Download as text file
 * - Copy to clipboard
 * - Warning about single-use nature
 * - Acknowledgment before continuing
 */

import { useState, useCallback } from 'react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

// ============================================================================
// Component Props
// ============================================================================

export interface TwoFactorRecoveryCodesProps {
  /** Array of backup codes to display */
  backupCodes: string[];
  /** Called when user acknowledges they saved the codes */
  onContinue?: () => void;
  /** Custom title */
  title?: string;
  /** Show acknowledgment button */
  showAcknowledgment?: boolean;
  /** Custom acknowledgment button text */
  acknowledgmentText?: string;
}

// ============================================================================
// Component
// ============================================================================

export function TwoFactorRecoveryCodes({
  backupCodes,
  onContinue,
  title,
  showAcknowledgment = true,
  acknowledgmentText,
}: TwoFactorRecoveryCodesProps) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const [downloaded, setDownloaded] = useState(false);

  // Copy all codes to clipboard
  const handleCopy = useCallback(async () => {
    const codesText = backupCodes.join('\n');
    try {
      await navigator.clipboard.writeText(codesText);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback for browsers without clipboard API
      const textArea = document.createElement('textarea');
      textArea.value = codesText;
      document.body.appendChild(textArea);
      textArea.select();
      document.execCommand('copy');
      document.body.removeChild(textArea);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }, [backupCodes]);

  // Download codes as text file
  const handleDownload = useCallback(() => {
    const text = `MeepleAI Backup Codes
========================

${backupCodes.join('\n')}

========================
Keep these codes in a secure location.
Each code can only be used once.
Generated: ${new Date().toISOString()}
`;
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'meepleai-backup-codes.txt';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    setDownloaded(true);
  }, [backupCodes]);

  return (
    <div
      className="w-full max-w-md space-y-6"
      data-testid="two-factor-recovery-codes"
    >
      {/* Header */}
      <div className="text-center space-y-2">
        <h2 className="text-2xl font-bold text-foreground">
          {title || t('auth.2fa.backupCodesTitle', 'Save Your Backup Codes')}
        </h2>
      </div>

      {/* Warning */}
      <Alert variant="destructive" data-testid="backup-codes-warning">
        <AlertDescription className="space-y-2">
          <p className="font-semibold">
            {t('auth.2fa.backupCodesWarningTitle', 'Important: Save these codes now!')}
          </p>
          <p className="text-sm">
            {t(
              'auth.2fa.backupCodesWarning',
              'These codes are your only way to access your account if you lose your authenticator device. Each code can only be used once.'
            )}
          </p>
        </AlertDescription>
      </Alert>

      {/* Codes Grid */}
      <div
        className="grid grid-cols-2 gap-2"
        data-testid="backup-codes-grid"
        role="list"
        aria-label={t('auth.2fa.backupCodesList', 'Backup codes')}
      >
        {backupCodes.map((code, index) => (
          <div
            key={index}
            className="bg-muted px-3 py-2 rounded-md font-mono text-sm text-center select-all"
            role="listitem"
            data-testid={`backup-code-${index}`}
          >
            {code}
          </div>
        ))}
      </div>

      {/* Actions */}
      <div className="flex gap-2">
        <Button
          type="button"
          variant="outline"
          onClick={handleCopy}
          className="flex-1"
          data-testid="copy-codes-button"
        >
          {copied
            ? t('common.copied', 'Copied!')
            : t('auth.2fa.copyCodes', 'Copy Codes')}
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={handleDownload}
          className="flex-1"
          data-testid="download-codes-button"
        >
          {downloaded
            ? t('common.downloaded', 'Downloaded!')
            : t('auth.2fa.downloadCodes', 'Download')}
        </Button>
      </div>

      {/* Acknowledgment */}
      {showAcknowledgment && onContinue && (
        <Button
          type="button"
          onClick={onContinue}
          className="w-full"
          data-testid="acknowledge-codes-button"
        >
          {acknowledgmentText || t('auth.2fa.savedCodes', "I've Saved My Codes")}
        </Button>
      )}
    </div>
  );
}
