/**
 * RegisterForm Component
 *
 * Reusable registration form with:
 * - React Hook Form for form state management
 * - Zod schema validation with password confirmation
 * - Accessible form controls
 * - Loading states
 * - Custom error display
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { AccessibleFormInput } from '@/components/accessible';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

// ============================================================================
// Validation Schema
// ============================================================================

const registerSchema = z.object({
  email: z
    .string()
    .min(1, 'Email is required')
    .email('Please enter a valid email address'),
  password: z
    .string()
    .min(8, 'Password must be at least 8 characters')
    .max(100, 'Password must not exceed 100 characters')
    .regex(
      /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/,
      'Password must contain at least one uppercase letter, one lowercase letter, and one number'
    ),
  confirmPassword: z
    .string()
    .min(1, 'Please confirm your password'),
  displayName: z
    .string()
    .min(2, 'Display name must be at least 2 characters')
    .max(50, 'Display name must not exceed 50 characters')
    .optional()
    .or(z.literal('')),
  role: z
    .enum(['User', 'Editor', 'Admin']),
}).refine((data) => data.password === data.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword'],
});

export type RegisterFormData = z.infer<typeof registerSchema>;

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
  showRoleSelector = false
}: RegisterFormProps) {
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
    const { confirmPassword, ...registerData } = data;
    await onSubmit(registerData);
  };

  return (
    <form onSubmit={handleSubmit(onFormSubmit)} className="space-y-4" noValidate>
      {/* Email Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Email"
          id="register-email"
          type="email"
          placeholder="you@example.com"
          autoComplete="email"
          error={errors.email?.message}
          required
          disabled={isLoading}
          {...register('email')}
        />
      </div>

      {/* Display Name Field (Optional) */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Display Name (Optional)"
          id="register-displayName"
          type="text"
          placeholder="John Doe"
          autoComplete="name"
          error={errors.displayName?.message}
          disabled={isLoading}
          {...register('displayName')}
        />
      </div>

      {/* Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Password"
          id="register-password"
          type="password"
          placeholder="••••••••"
          autoComplete="new-password"
          error={errors.password?.message}
          required
          disabled={isLoading}
          {...register('password')}
        />
        <p className="text-xs text-slate-500 dark:text-slate-400">
          Must contain uppercase, lowercase, and number (min 8 characters)
        </p>
      </div>

      {/* Confirm Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Confirm Password"
          id="register-confirmPassword"
          type="password"
          placeholder="••••••••"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          required
          disabled={isLoading}
          {...register('confirmPassword')}
        />
      </div>

      {/* Role Selector (Conditional) */}
      {showRoleSelector && (
        <div className="space-y-2">
          <label htmlFor="register-role" className="block text-sm font-medium text-slate-900 dark:text-slate-100">
            Role
          </label>
          <Select
            value={selectedRole}
            onValueChange={(value) => setValue('role', value as 'User' | 'Editor' | 'Admin')}
            disabled={isLoading}
          >
            <SelectTrigger id="register-role">
              <SelectValue placeholder="Select a role" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="User">User</SelectItem>
              <SelectItem value="Editor">Editor</SelectItem>
              <SelectItem value="Admin">Admin</SelectItem>
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
        loadingText="Creating account..."
      >
        Create Account
      </LoadingButton>
    </form>
  );
}
