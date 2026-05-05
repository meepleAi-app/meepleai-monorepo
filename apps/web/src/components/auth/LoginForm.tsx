/**
 * LoginForm Component
 *
 * Reusable login form (auth-flow v2):
 * - React Hook Form (Controller) for form state management
 * - Zod schema validation
 * - v2 primitives: InputField, PwdInput, Btn
 * - Accessible form controls (aria-invalid, aria-describedby)
 * - Loading states
 * - Custom error display
 * - i18n support
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

export interface LoginFormData {
  email: string;
  password: string;
}

// ============================================================================
// Component Props
// ============================================================================

export interface LoginFormProps {
  onSubmit: (data: LoginFormData) => Promise<void> | void;
  loading?: boolean;
  error?: string;
  onErrorDismiss?: () => void;
}

// ============================================================================
// Component
// ============================================================================

export function LoginForm({ onSubmit, loading = false, error, onErrorDismiss }: LoginFormProps) {
  const { t } = useTranslation();

  // Validation Schema with i18n
  const loginSchema = z.object({
    email: z.string().min(1, t('validation.emailRequired')).email(t('validation.invalidEmail')),
    password: z.string().min(8, t('validation.passwordMin')).max(100, t('validation.passwordMax')),
  });

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const isLoading = loading || isSubmitting;

  const onFormSubmit = async (data: LoginFormData) => {
    if (onErrorDismiss) {
      onErrorDismiss();
    }
    await onSubmit(data);
  };

  return (
    <form
      onSubmit={handleSubmit(onFormSubmit)}
      className="space-y-4"
      noValidate
      data-testid="login-form"
    >
      {/* Email Field */}
      <Controller
        name="email"
        control={control}
        render={({ field }) => (
          <InputField
            label={t('auth.login.emailLabel')}
            id="login-email"
            type="email"
            placeholder={t('auth.login.emailPlaceholder')}
            autoComplete="email"
            value={field.value}
            onChange={field.onChange}
            error={errors.email?.message}
            disabled={isLoading}
            required
          />
        )}
      />

      {/* Password Field */}
      <Controller
        name="password"
        control={control}
        render={({ field }) => (
          <PwdInput
            label={t('auth.login.passwordLabel')}
            id="login-password"
            placeholder={t('auth.login.passwordPlaceholder')}
            autoComplete="current-password"
            value={field.value}
            onChange={field.onChange}
            error={errors.password?.message}
            disabled={isLoading}
            required
            toggleShowLabel={t('auth.visibility.show')}
            toggleHideLabel={t('auth.visibility.hide')}
          />
        )}
      />

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
        {t('auth.login.loginButton')}
      </Btn>
    </form>
  );
}
