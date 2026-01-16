/**
 * RegisterForm Component
 *
 * Reusable registration form with:
 * - React Hook Form for form state management
 * - Zod schema validation with password confirmation
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useTranslation } from '@/hooks/useTranslation';

// ============================================================================
// Types
// ============================================================================

export interface RegisterFormData {
  email: string;
  password: string;
  confirmPassword: string;
  displayName?: string;
  role: 'User' | 'Editor' | 'Admin';
}

// ============================================================================
// Component Props
// ============================================================================

export interface RegisterFormProps {
  onSubmit: (data: Omit<RegisterFormData, 'confirmPassword'>) => Promise<void> | void;
  loading?: boolean;
  error?: string;
  onErrorDismiss?: () => void;
  showRoleSelector?: boolean;
}

// ============================================================================
// Component
// ============================================================================

export function RegisterForm({
  onSubmit,
  loading = false,
  error,
  onErrorDismiss,
  showRoleSelector = false,
}: RegisterFormProps) {
  const { t } = useTranslation();

  // Validation Schema with i18n
  const registerSchema = z
    .object({
      email: z.string().min(1, t('validation.emailRequired')).email(t('validation.invalidEmail')),
      password: z
        .string()
        .min(8, t('validation.passwordMin'))
        .max(100, t('validation.passwordMax'))
        .regex(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/, t('validation.passwordComplexity')),
      confirmPassword: z.string().min(1, t('validation.passwordConfirmRequired')),
      displayName: z
        .string()
        .min(2, t('validation.minLength', { min: 2 }))
        .max(50, t('validation.maxLength', { max: 50 }))
        .optional()
        .or(z.literal('')),
      role: z.enum(['User', 'Editor', 'Admin']),
    })
    .refine(data => data.password === data.confirmPassword, {
      message: t('validation.passwordMatch'),
      path: ['confirmPassword'],
    });

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      email: '',
      password: '',
      confirmPassword: '',
      displayName: '',
      role: 'User',
    },
  });

  const isLoading = loading || isSubmitting;
  const selectedRole = watch('role');

  const onFormSubmit = async (data: RegisterFormData) => {
    if (onErrorDismiss) {
      onErrorDismiss();
    }

    // Remove confirmPassword before sending to API
    const { confirmPassword: _confirmPassword, ...registerData } = data;
    await onSubmit(registerData);
  };

  return (
    <form
      onSubmit={handleSubmit(onFormSubmit)}
      className="space-y-4"
      noValidate
      data-testid="register-form"
    >
      {/* Email Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label={t('auth.register.email')}
          id="register-email"
          type="email"
          placeholder={t('auth.register.emailPlaceholder')}
          autoComplete="email"
          error={errors.email?.message}
          required
          disabled={isLoading}
          data-testid="register-email"
          {...register('email')}
        />
      </div>

      {/* Display Name Field (Optional) */}
      <div className="space-y-2">
        <AccessibleFormInput
          label={t('auth.register.displayNameOptional')}
          id="register-displayName"
          type="text"
          placeholder={t('auth.register.displayNamePlaceholder')}
          autoComplete="name"
          error={errors.displayName?.message}
          disabled={isLoading}
          data-testid="register-display-name"
          {...register('displayName')}
        />
      </div>

      {/* Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label={t('auth.register.password')}
          id="register-password"
          type="password"
          placeholder={t('auth.register.passwordPlaceholder')}
          autoComplete="new-password"
          error={errors.password?.message}
          required
          disabled={isLoading}
          data-testid="register-password"
          {...register('password')}
        />
        <p className="text-xs text-slate-500 dark:text-slate-400">
          {t('auth.register.passwordHelp')}
        </p>
      </div>

      {/* Confirm Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label={t('auth.register.confirmPassword')}
          id="register-confirmPassword"
          type="password"
          placeholder={t('auth.register.confirmPasswordPlaceholder')}
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          required
          disabled={isLoading}
          data-testid="register-confirm-password"
          {...register('confirmPassword')}
        />
      </div>

      {/* Role Selector (Conditional) */}
      {showRoleSelector && (
        <div className="space-y-2">
          <label
            htmlFor="register-role"
            className="block text-sm font-medium text-slate-900 dark:text-slate-100"
          >
            {t('auth.register.role')}
          </label>
          <Select
            value={selectedRole}
            onValueChange={value => setValue('role', value as 'User' | 'Editor' | 'Admin')}
            disabled={isLoading}
          >
            <SelectTrigger id="register-role" data-testid="register-role-select">
              <SelectValue placeholder={t('auth.register.selectRole')} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="User">{t('roles.user')}</SelectItem>
              <SelectItem value="Editor">{t('roles.editor')}</SelectItem>
              <SelectItem value="Admin">{t('roles.admin')}</SelectItem>
            </SelectContent>
          </Select>
        </div>
      )}

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
        loadingText={t('auth.register.creatingAccount')}
        data-testid="register-submit"
      >
        {t('auth.register.createAccount')}
      </LoadingButton>
    </form>
  );
}