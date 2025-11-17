/**
 * RegisterForm Component
 *
 * Reusable registration form with:
 * - React 19 useActionState for form state management
 * - Client-side password confirmation validation
 * - Server Actions pattern for auth
 * - Accessible form controls
 * - Loading states
 * - Localized Italian error messages
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 */

import { useActionState, useState, useEffect } from 'react';
import { AccessibleFormInput } from '@/components/accessible';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { registerAction, type AuthActionState } from '@/actions/auth';
import { validationMessages } from '@/lib/i18n/errors';

// ============================================================================
// Component Props
// ============================================================================

export interface RegisterFormProps {
  /**
   * Callback when registration succeeds
   */
  onSuccess?: (user: NonNullable<AuthActionState['user']>) => void;
}

// ============================================================================
// Component
// ============================================================================

export function RegisterForm({
  onSuccess
}: RegisterFormProps) {
  // Use React 19 useActionState for form handling
  const [state, formAction, isPending] = useActionState<AuthActionState, FormData>(
    registerAction,
    { success: false }
  );

  // Local validation state for password confirmation
  const [passwordError, setPasswordError] = useState<string | null>(null);

  // Call onSuccess callback when registration succeeds (useEffect to avoid render-time side effects)
  useEffect(() => {
    if (state.success && state.user && onSuccess) {
      onSuccess(state.user);
    }
  }, [state.success, state.user, onSuccess]);

  // Client-side password confirmation validation
  const handlePasswordChange = (e: React.FormEvent<HTMLFormElement>) => {
    const formData = new FormData(e.currentTarget);
    const password = formData.get('password') as string;
    const confirmPassword = formData.get('confirmPassword') as string;

    if (confirmPassword && password !== confirmPassword) {
      setPasswordError(validationMessages.passwordsDoNotMatch);
    } else {
      setPasswordError(null);
    }
  };

  return (
    <form action={formAction} onChange={handlePasswordChange} className="space-y-4" noValidate>
      {/* Email Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Email"
          id="register-email"
          name="email"
          type="email"
          placeholder="tuoemail@esempio.com"
          autoComplete="email"
          required
          disabled={isPending}
        />
      </div>

      {/* Display Name Field (Optional) */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Nome visualizzato (Opzionale)"
          id="register-displayName"
          name="displayName"
          type="text"
          placeholder="Mario Rossi"
          autoComplete="name"
          disabled={isPending}
        />
      </div>

      {/* Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Password"
          id="register-password"
          name="password"
          type="password"
          placeholder="••••••••"
          autoComplete="new-password"
          required
          disabled={isPending}
        />
        <p className="text-xs text-slate-500 dark:text-slate-400">
          Deve contenere maiuscole, minuscole e numeri (minimo 8 caratteri)
        </p>
      </div>

      {/* Confirm Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Conferma Password"
          id="register-confirmPassword"
          name="confirmPassword"
          type="password"
          placeholder="••••••••"
          autoComplete="new-password"
          error={passwordError || undefined}
          required
          disabled={isPending}
        />
      </div>

      {/* Error Message */}
      {state.error && (
        <div
          className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-3"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-red-800 dark:text-red-200">{state.error.message}</p>
        </div>
      )}

      {/* Submit Button */}
      <LoadingButton
        type="submit"
        className="w-full"
        isLoading={isPending}
        loadingText="Creazione account..."
        disabled={!!passwordError}
      >
        Crea Account
      </LoadingButton>
    </form>
  );
}
