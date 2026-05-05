/**
 * RegisterForm Component (auth-flow v2)
 *
 * - React Hook Form (Controller) for form state management
 * - Zod schema validation
 * - v2 primitives: InputField, PwdInput (with strength meter), Btn
 * - GDPR-compliant terms-and-conditions checkbox
 * - Honeypot hidden field for bot protection
 * - Accessible form controls (aria-invalid, aria-describedby)
 * - Loading states, custom error display, i18n support
 */

import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import * as z from 'zod';

import { Btn } from '@/components/ui/v2/btn';
import { InputField } from '@/components/ui/v2/input-field';
import { PwdInput } from '@/components/ui/v2/pwd-input';
import { useTranslation } from '@/hooks/useTranslation';

// ============================================================================
// Types
// ============================================================================

export interface RegisterFormData {
  email: string;
  password: string;
  termsAccepted: boolean;
  honeypot?: string;
}

/**
 * Payload delivered to `onSubmit` once the form validates.
 * `termsAcceptedAt` captures the moment the user consented to the terms
 * (GDPR audit requirement); `honeypot` is forwarded for bot detection.
 */
export interface RegisterSubmitPayload {
  email: string;
  password: string;
  termsAcceptedAt: Date;
  honeypot?: string;
}

// ============================================================================
// Component Props
// ============================================================================

export interface RegisterFormProps {
  onSubmit: (data: RegisterSubmitPayload) => Promise<void> | void;
  loading?: boolean;
  error?: string;
  onErrorDismiss?: () => void;
}

// ============================================================================
// Component
// ============================================================================

export function RegisterForm({
  onSubmit,
  loading = false,
  error,
  onErrorDismiss,
}: RegisterFormProps) {
  const { t } = useTranslation();

  const registerSchema = z.object({
    email: z.string().min(1, t('validation.emailRequired')).email(t('validation.invalidEmail')),
    password: z.string().min(8, t('validation.passwordMin')).max(100, t('validation.passwordMax')),
    termsAccepted: z
      .boolean()
      .refine(v => v === true, { message: t('auth.register.termsRequired') }),
    honeypot: z.string().optional(),
  });

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      email: '',
      password: '',
      termsAccepted: false,
      honeypot: '',
    },
  });

  const isLoading = loading || isSubmitting;

  const onFormSubmit = async (data: RegisterFormData) => {
    if (onErrorDismiss) {
      onErrorDismiss();
    }
    await onSubmit({
      email: data.email,
      password: data.password,
      termsAcceptedAt: new Date(),
      honeypot: data.honeypot,
    });
  };

  return (
    <form
      onSubmit={handleSubmit(onFormSubmit)}
      className="space-y-4"
      noValidate
      data-testid="register-form"
    >
      {/* Email Field */}
      <Controller
        name="email"
        control={control}
        render={({ field }) => (
          <InputField
            label={t('auth.register.emailLabel')}
            id="register-email"
            type="email"
            placeholder={t('auth.register.emailPlaceholder')}
            autoComplete="email"
            value={field.value}
            onChange={field.onChange}
            error={errors.email?.message}
            disabled={isLoading}
            required
          />
        )}
      />

      {/* Password Field (with strength meter) */}
      <Controller
        name="password"
        control={control}
        render={({ field }) => (
          <PwdInput
            label={t('auth.register.passwordLabel')}
            id="register-password"
            placeholder={t('auth.register.passwordPlaceholder')}
            autoComplete="new-password"
            value={field.value}
            onChange={field.onChange}
            error={errors.password?.message}
            disabled={isLoading}
            required
            showStrength
            toggleShowLabel={t('auth.visibility.show')}
            toggleHideLabel={t('auth.visibility.hide')}
            strengthPrefix={t('auth.meter.prefix')}
            strengthLabels={[
              t('auth.meter.weak'),
              t('auth.meter.weak'),
              t('auth.meter.fair'),
              t('auth.meter.good'),
              t('auth.meter.strong'),
            ]}
          />
        )}
      />

      {/* Honeypot (hidden from users + assistive tech) */}
      <Controller
        name="honeypot"
        control={control}
        render={({ field }) => (
          <input
            type="text"
            tabIndex={-1}
            autoComplete="off"
            aria-hidden="true"
            className="absolute left-[-9999px] w-px h-px"
            value={field.value ?? ''}
            onChange={field.onChange}
            onBlur={field.onBlur}
            name={field.name}
            ref={field.ref}
          />
        )}
      />

      {/* Terms & Conditions */}
      <Controller
        name="termsAccepted"
        control={control}
        render={({ field }) => (
          <label className="flex items-start gap-2 text-sm font-body text-foreground cursor-pointer">
            <input
              type="checkbox"
              checked={field.value}
              onChange={e => field.onChange(e.target.checked)}
              disabled={isLoading}
              className="mt-0.5 w-4 h-4 rounded border-border text-[hsl(var(--c-toolkit))] focus:ring-2 focus:ring-[hsl(var(--c-toolkit)/0.3)]"
              aria-invalid={!!errors.termsAccepted}
              data-testid="register-terms"
            />
            <span>
              {t('auth.register.termsPrefix')}{' '}
              <a
                href="/terms"
                target="_blank"
                rel="noopener noreferrer"
                className="underline text-[hsl(var(--c-toolkit))]"
              >
                {t('auth.register.termsLink')}
              </a>{' '}
              {t('auth.register.termsAnd')}{' '}
              <a
                href="/privacy"
                target="_blank"
                rel="noopener noreferrer"
                className="underline text-[hsl(var(--c-toolkit))]"
              >
                {t('auth.register.privacyLink')}
              </a>
              .
            </span>
          </label>
        )}
      />
      {errors.termsAccepted && (
        <p role="alert" className="text-xs text-[hsl(var(--c-danger))] font-body">
          {errors.termsAccepted.message}
        </p>
      )}

      {/* Error Message */}
      {error && (
        <div
          className="rounded-md bg-[hsl(var(--c-danger)/0.1)] border border-[hsl(var(--c-danger)/0.3)] p-3"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-[hsl(var(--c-danger))]">{error}</p>
        </div>
      )}

      {/* Submit Button */}
      <Btn type="submit" variant="primary" fullWidth loading={isLoading}>
        {t('auth.register.registerButton')}
      </Btn>
    </form>
  );
}
