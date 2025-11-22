/**
 * LoginForm Component
 *
 * Reusable login form with:
 * - React Hook Form for form state management
 * - Zod schema validation
 * - Accessible form controls
 * - Loading states
 * - Custom error display
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Input } from '@/components/ui/input';
import { AccessibleFormInput } from '@/components/accessible';
import { LoadingButton } from '@/components/loading/LoadingButton';

// ============================================================================
// Validation Schema
// ============================================================================

const loginSchema = z.object({
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .max(100, 'Password must not exceed 100 characters'),
});

export type LoginFormData = z.infer<typeof loginSchema>;

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

export function LoginForm({
  onSubmit,
  loading = false,
  error,
  onErrorDismiss
}: LoginFormProps) {
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
          label="Email"
          id="login-email"
          type="email"
          placeholder="you@example.com"
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
          label="Password"
          id="login-password"
          type="password"
          placeholder="••••••••"
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
        loadingText="Signing in..."
      >
        Sign In
      </LoadingButton>
    </form>
  );
}
