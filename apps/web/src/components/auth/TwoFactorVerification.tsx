/**
 * TwoFactorVerification Component (Issue #3077)
 *
 * 6-digit TOTP verification input with:
 * - Auto-focus on mount
 * - Auto-submit when 6 digits entered
 * - Remember device option
 * - Recovery code fallback
 * - Accessible form controls
 * - Loading and error states
 */

import { useEffect, useRef, useState, useCallback } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';
import { useTranslation } from '@/hooks/useTranslation';

// ============================================================================
// Types
// ============================================================================

export interface TwoFactorVerificationData {
  code: string;
  rememberDevice?: boolean;
}

// ============================================================================
// Component Props
// ============================================================================

export interface TwoFactorVerificationProps {
  /** Called when verification code is submitted */
  onVerify: (data: TwoFactorVerificationData) => Promise<void> | void;
  /** Called when user wants to use a backup code instead */
  onUseBackupCode?: () => void;
  /** Called when user wants to cancel verification */
  onCancel?: () => void;
  /** Loading state */
  loading?: boolean;
  /** Error message to display */
  error?: string;
  /** Clear error callback */
  onErrorDismiss?: () => void;
  /** Auto-submit when 6 digits entered */
  autoSubmit?: boolean;
  /** Show remember device option */
  showRememberDevice?: boolean;
  /** Custom title */
  title?: string;
  /** Custom subtitle */
  subtitle?: string;
}

// ============================================================================
// Component
// ============================================================================

export function TwoFactorVerification({
  onVerify,
  onUseBackupCode,
  onCancel,
  loading = false,
  error,
  onErrorDismiss,
  autoSubmit = true,
  showRememberDevice = true,
  title,
  subtitle,
}: TwoFactorVerificationProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // Track auto-submitted code to prevent duplicate submissions
  const autoSubmittedCodeRef = useRef<string | null>(null);

  // Validation schema
  const verificationSchema = z.object({
    code: z
      .string()
      .min(6, t('auth.twoFactor.codeRequired'))
      .max(8, t('auth.twoFactor.codeInvalid')) // Allow backup codes (8 chars)
      .regex(/^[0-9A-Za-z-]+$/, t('auth.twoFactor.codeInvalid')),
    rememberDevice: z.boolean().default(false),
  });

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<TwoFactorVerificationData>({
    resolver: zodResolver(verificationSchema),
    defaultValues: {
      code: '',
      rememberDevice: false,
    },
  });

  const codeValue = watch('code');
  const isLoading = loading || isSubmitting;

  // Form submission handler - defined before effects that use it
  const onFormSubmit = useCallback(
    async (data: TwoFactorVerificationData) => {
      if (onErrorDismiss) {
        onErrorDismiss();
      }
      setIsSubmitting(true);
      try {
        await onVerify(data);
      } finally {
        setIsSubmitting(false);
      }
    },
    [onVerify, onErrorDismiss]
  );

  // Auto-focus input on mount
  useEffect(() => {
    if (inputRef.current) {
      inputRef.current.focus();
    }
  }, []);

  // Auto-submit when 6 digits entered
  // Uses ref to prevent duplicate submissions when isLoading state changes
  useEffect(() => {
    if (
      autoSubmit &&
      codeValue.length === 6 &&
      !isLoading &&
      /^\d{6}$/.test(codeValue) &&
      autoSubmittedCodeRef.current !== codeValue
    ) {
      autoSubmittedCodeRef.current = codeValue;
      handleSubmit(onFormSubmit)();
    }
  }, [autoSubmit, codeValue, isLoading, handleSubmit, onFormSubmit]);

  // Handle input change - only allow digits and format
  const handleCodeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value.replace(/[^0-9A-Za-z-]/g, '').slice(0, 8);
    setValue('code', value, { shouldValidate: true });
  };

  // Header rendering: AuthCard / Dialog wrappers already supply h1 + subtitle.
  // The component renders an optional inline header ONLY when `title` is
  // passed explicitly (rare); default usage (no title prop) skips the block to
  // avoid a duplicate heading inside the wrapper. Audit ref: #1816 § P3 2FA i18n.
  const showInlineHeader = Boolean(title) || Boolean(subtitle);

  return (
    <div className="w-full max-w-md space-y-6" data-testid="two-factor-verification">
      {showInlineHeader && (
        <div className="text-center space-y-2">
          {title && <h2 className="text-2xl font-bold text-foreground">{title}</h2>}
          {subtitle && <p className="text-sm text-muted-foreground">{subtitle}</p>}
        </div>
      )}

      <form
        onSubmit={handleSubmit(onFormSubmit)}
        className="space-y-4"
        noValidate
        data-testid="two-factor-form"
      >
        {/* Code Input */}
        <div className="space-y-2">
          <Label htmlFor="2fa-code" className="sr-only">
            {t('auth.twoFactor.code')}
          </Label>
          <input
            {...register('code')}
            ref={e => {
              register('code').ref(e);
              inputRef.current = e;
            }}
            id="2fa-code"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            placeholder="000000"
            maxLength={8}
            onChange={handleCodeChange}
            disabled={isLoading}
            aria-invalid={!!errors.code}
            aria-describedby={errors.code ? '2fa-code-error' : undefined}
            className="w-full px-4 py-3 border border-input rounded-md text-center text-3xl font-mono tracking-widest focus:ring-2 focus:ring-ring focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed bg-background"
            data-testid="2fa-code-input"
          />
          {errors.code && (
            <p id="2fa-code-error" className="text-sm text-destructive" role="alert">
              {errors.code.message}
            </p>
          )}
        </div>

        {/* Error Display */}
        {error && (
          <div
            className="rounded-md bg-destructive/10 border border-destructive/20 p-3"
            role="alert"
            aria-live="polite"
            data-testid="2fa-error"
          >
            <p className="text-sm text-destructive">{error}</p>
          </div>
        )}

        {/* Remember Device */}
        {showRememberDevice && (
          <div className="flex items-center gap-2">
            <Checkbox
              id="remember-device"
              checked={watch('rememberDevice')}
              onCheckedChange={checked => setValue('rememberDevice', !!checked)}
              disabled={isLoading}
              data-testid="remember-device-checkbox"
            />
            <Label
              htmlFor="remember-device"
              className="text-sm text-muted-foreground cursor-pointer"
            >
              {t('auth.twoFactor.rememberDevice')}
            </Label>
          </div>
        )}

        {/* Submit Button */}
        <LoadingButton
          type="submit"
          className="w-full"
          isLoading={isLoading}
          loadingText={t('auth.twoFactor.submitting')}
          disabled={codeValue.length < 6}
          data-testid="2fa-verify-button"
        >
          {t('auth.twoFactor.submit')}
        </LoadingButton>

        {/* Backup Code Link */}
        {onUseBackupCode && (
          <div className="text-center">
            <button
              type="button"
              onClick={onUseBackupCode}
              disabled={isLoading}
              className="text-sm text-primary hover:underline disabled:opacity-50 disabled:cursor-not-allowed"
              data-testid="use-backup-code-link"
            >
              {t('auth.twoFactor.backupCode')}
            </button>
          </div>
        )}

        {/* Cancel Button */}
        {onCancel && (
          <div className="text-center">
            <button
              type="button"
              onClick={onCancel}
              disabled={isLoading}
              className="text-sm text-muted-foreground hover:text-foreground disabled:opacity-50"
              data-testid="2fa-cancel-button"
            >
              {t('common.cancel')}
            </button>
          </div>
        )}
      </form>
    </div>
  );
}
