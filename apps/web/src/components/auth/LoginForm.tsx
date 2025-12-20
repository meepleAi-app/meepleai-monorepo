/**
 * LoginForm Component
 *
 * Reusable login form with:
 * - React Hook Form for form state management
 * - Zod schema validation
 * - Accessible form controls
 * - Loading states
 * - Custom error display
 * - i18n support
 */

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';

import { AccessibleFormInput } from '@/components/accessible';
import { LoadingButton } from '@/components/loading/LoadingButton';
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
    register,
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
    <form onSubmit={handleSubmit(onFormSubmit)} className="space-y-4" noValidate>
      {/* Email Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label={t('auth.login.emailLabel')}
          id="login-email"
          type="email"
          placeholder={t('auth.login.emailPlaceholder')}
          autoComplete="email"
          error={errors.email?.message}
          required
          disabled={isLoading}
          {...register('email')}
        />
      </div>

      {/* Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label={t('auth.login.passwordLabel')}
          id="login-password"
          type="password"
          placeholder={t('auth.login.passwordPlaceholder')}
          autoComplete="current-password"
          error={errors.password?.message}
          required
          disabled={isLoading}
          {...register('password')}
        />
      </div>

      {/* Error Message */}
      {error && (
        <div
          className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-3"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      {/* Submit Button */}
      <LoadingButton
        type="submit"
        className="w-full"
        isLoading={isLoading}
        loadingText={t('auth.login.loggingIn')}
        data-testid="login-submit"
      >
        {t('auth.login.loginButton')}
      </LoadingButton>
    </form>
  );
}
