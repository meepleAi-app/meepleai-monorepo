/**
 * TwoFactorDisable Component (Issue #3077)
 *
 * Disable 2FA flow with:
 * - Password verification
 * - TOTP code or backup code input
 * - Confirmation warning
 * - Loading and error states
 */

import { useState, useCallback } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useTranslation } from '@/hooks/useTranslation';

// ============================================================================
// Types
// ============================================================================

export interface TwoFactorDisableData {
  password: string;
  code: string;
}

// ============================================================================
// Component Props
// ============================================================================

export interface TwoFactorDisableProps {
  /** Called when disable is submitted */
  onDisable: (data: TwoFactorDisableData) => Promise<void> | void;
  /** Called when user cancels */
  onCancel?: () => void;
  /** Loading state */
  loading?: boolean;
  /** Error message to display */
  error?: string;
  /** Clear error callback */
  onErrorDismiss?: () => void;
  /** Number of remaining backup codes (for warning) */
  remainingBackupCodes?: number;
}

// ============================================================================
// Component
// ============================================================================

export function TwoFactorDisable({
  onDisable,
  onCancel,
  loading = false,
  error,
  onErrorDismiss,
  remainingBackupCodes,
}: TwoFactorDisableProps) {
  const { t } = useTranslation();
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Validation schema
  const disableSchema = z.object({
    password: z.string().min(1, t('validation.passwordRequired', 'Password is required')),
    code: z
      .string()
      .min(6, t('auth.2fa.codeRequired', 'Please enter a 6-digit code or backup code'))
      .max(12, t('auth.2fa.codeInvalid', 'Code is too long')),
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<TwoFactorDisableData>({
    resolver: zodResolver(disableSchema),
    defaultValues: {
      password: '',
      code: '',
    },
  });

  const isLoading = loading || isSubmitting;

  const onFormSubmit = useCallback(
    async (data: TwoFactorDisableData) => {
      if (onErrorDismiss) {
        onErrorDismiss();
      }
      setIsSubmitting(true);
      try {
        await onDisable(data);
      } finally {
        setIsSubmitting(false);
      }
    },
    [onDisable, onErrorDismiss]
  );

  return (
    <div
      className="w-full max-w-md space-y-6"
      data-testid="two-factor-disable"
    >
      {/* Header */}
      <div className="text-center space-y-2">
        <h2 className="text-2xl font-bold text-foreground">
          {t('auth.2fa.disableTitle', 'Disable Two-Factor Authentication')}
        </h2>
        <p className="text-sm text-muted-foreground">
          {t('auth.2fa.disableSubtitle', 'Enter your password and a code to disable 2FA')}
        </p>
      </div>

      {/* Warning */}
      <Alert variant="destructive" data-testid="disable-warning">
        <AlertDescription className="space-y-2">
          <p className="font-semibold">
            {t('auth.2fa.disableWarningTitle', 'Warning: Your account will be less secure')}
          </p>
          <p className="text-sm">
            {t(
              'auth.2fa.disableWarning',
              'Disabling two-factor authentication removes an important security layer from your account. Anyone with your password will be able to access your account.'
            )}
          </p>
        </AlertDescription>
      </Alert>

      {/* Low Backup Codes Warning */}
      {remainingBackupCodes !== undefined && remainingBackupCodes < 3 && (
        <Alert data-testid="low-backup-codes-warning">
          <AlertDescription>
            {t(
              'auth.2fa.lowBackupCodesWarning',
              `You only have ${remainingBackupCodes} backup code${remainingBackupCodes !== 1 ? 's' : ''} remaining. Consider re-enabling 2FA after disabling to generate new backup codes.`
            )}
          </AlertDescription>
        </Alert>
      )}

      {/* Form */}
      <form
        onSubmit={handleSubmit(onFormSubmit)}
        className="space-y-4"
        noValidate
        data-testid="disable-2fa-form"
      >
        {/* Password Input */}
        <div className="space-y-2">
          <Label htmlFor="disable-password">
            {t('auth.2fa.currentPassword', 'Current Password')}
          </Label>
          <Input
            {...register('password')}
            id="disable-password"
            type="password"
            autoComplete="current-password"
            placeholder={t('auth.2fa.enterPassword', 'Enter your password')}
            disabled={isLoading}
            aria-invalid={!!errors.password}
            aria-describedby={errors.password ? 'password-error' : undefined}
            data-testid="disable-password-input"
          />
          {errors.password && (
            <p
              id="password-error"
              className="text-sm text-destructive"
              role="alert"
            >
              {errors.password.message}
            </p>
          )}
        </div>

        {/* Code Input */}
        <div className="space-y-2">
          <Label htmlFor="disable-code">
            {t('auth.2fa.codeOrBackup', 'TOTP Code or Backup Code')}
          </Label>
          <Input
            {...register('code')}
            id="disable-code"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            placeholder={t('auth.2fa.enterCodeOrBackup', '000000 or XXXX-XXXX')}
            disabled={isLoading}
            aria-invalid={!!errors.code}
            aria-describedby={errors.code ? 'code-error' : undefined}
            data-testid="disable-code-input"
          />
          {errors.code && (
            <p
              id="code-error"
              className="text-sm text-destructive"
              role="alert"
            >
              {errors.code.message}
            </p>
          )}
        </div>

        {/* Error Display */}
        {error && (
          <Alert variant="destructive" data-testid="disable-error">
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
              data-testid="cancel-disable-button"
            >
              {t('common.cancel', 'Cancel')}
            </Button>
          )}
          <LoadingButton
            type="submit"
            variant="destructive"
            className="flex-1"
            isLoading={isLoading}
            loadingText={t('auth.2fa.disabling', 'Disabling...')}
            data-testid="confirm-disable-button"
          >
            {t('auth.2fa.disableButton', 'Disable 2FA')}
          </LoadingButton>
        </div>
      </form>
    </div>
  );
}
