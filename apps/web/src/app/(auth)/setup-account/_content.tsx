'use client';
/* eslint-disable @typescript-eslint/no-non-null-assertion -- pre-existing pattern: array/object access guarded by length/key check or by upstream validator; assertion is correct by construction. Cleanup tracked for follow-up audit. */

/**
 * Setup Account Page Content (Issue #124)
 *
 * Invitation activation flow:
 * 1. Read `token` from URL search params
 * 2. GET /api/v1/auth/validate-invitation?token=xxx
 * 3. If valid: show password form with pre-filled email/displayName
 * 4. POST /api/v1/auth/activate-account with { token, password }
 * 5. On success: redirect based on requiresOnboarding flag
 */

import { useEffect, useState, FormEvent } from 'react';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { AuthLayout } from '@/components/layouts';
import { Btn } from '@/components/ui/v2/btn';
import { InputField } from '@/components/ui/v2/input-field';
import { PwdInput } from '@/components/ui/v2/pwd-input';
import { getErrorMessage } from '@/lib/utils/errorHandler';

// ──────────────────────────────────────────────
// Types
// ──────────────────────────────────────────────

interface PasswordValidation {
  minLength: boolean;
  hasUppercase: boolean;
  hasLowercase: boolean;
  hasNumber: boolean;
  isValid: boolean;
}

interface InvitationValidation {
  isValid: boolean;
  email: string | null;
  displayName: string | null;
  errorReason: string | null;
}

// ──────────────────────────────────────────────
// Utilities
// ──────────────────────────────────────────────

const validatePassword = (password: string): PasswordValidation => {
  const minLength = password.length >= 8;
  const hasUppercase = /[A-Z]/.test(password);
  const hasLowercase = /[a-z]/.test(password);
  const hasNumber = /[0-9]/.test(password);

  return {
    minLength,
    hasUppercase,
    hasLowercase,
    hasNumber,
    isValid: minLength && hasUppercase && hasLowercase && hasNumber,
  };
};

const getApiBase = (): string => {
  if (typeof window !== 'undefined') {
    return process.env.NEXT_PUBLIC_API_BASE_URL ?? '';
  }
  return '';
};

// ──────────────────────────────────────────────
// Main Content Component
// ──────────────────────────────────────────────

export function SetupAccountContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams?.get('token') ?? null;

  // Token validation state
  const [validationResult, setValidationResult] = useState<InvitationValidation | null>(null);
  const [isValidating, setIsValidating] = useState(true);

  // Password form state
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordValidation, setPasswordValidation] = useState<PasswordValidation>({
    minLength: false,
    hasUppercase: false,
    hasLowercase: false,
    hasNumber: false,
    isValid: false,
  });

  // UI state
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [activationSuccess, setActivationSuccess] = useState(false);

  // ── Step 1: Validate token on mount ──────────
  useEffect(() => {
    if (!token) {
      setValidationResult({
        isValid: false,
        email: null,
        displayName: null,
        errorReason: 'invalid',
      });
      setIsValidating(false);
      return;
    }

    let cancelled = false;

    async function validateToken() {
      try {
        const baseUrl = getApiBase();
        const url = `${baseUrl}/api/v1/auth/validate-invitation?token=${encodeURIComponent(token!)}`;
        const response = await fetch(url, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
        });

        if (cancelled) return;

        if (!response.ok) {
          setValidationResult({
            isValid: false,
            email: null,
            displayName: null,
            errorReason: 'invalid',
          });
          setIsValidating(false);
          return;
        }

        const data = await response.json();
        setValidationResult({
          isValid: data.isValid ?? false,
          email: data.email ?? null,
          displayName: data.displayName ?? null,
          errorReason: data.errorReason ?? null,
        });
      } catch {
        if (cancelled) return;
        setValidationResult({
          isValid: false,
          email: null,
          displayName: null,
          errorReason: 'invalid',
        });
      } finally {
        if (!cancelled) setIsValidating(false);
      }
    }

    validateToken();
    return () => {
      cancelled = true;
    };
  }, [token]);

  // ── Update password validation on change ──────
  useEffect(() => {
    if (password) {
      setPasswordValidation(validatePassword(password));
    } else {
      setPasswordValidation({
        minLength: false,
        hasUppercase: false,
        hasLowercase: false,
        hasNumber: false,
        isValid: false,
      });
    }
  }, [password]);

  // ── Step 2: Handle form submission ────────────
  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    if (!passwordValidation.isValid) {
      setErrorMessage('La password non soddisfa tutti i requisiti.');
      return;
    }

    // eslint-disable-next-line security/detect-possible-timing-attacks -- client-side form match check; both values come from same user session, no attacker-observable timing
    if (password !== confirmPassword) {
      setErrorMessage('Le password non coincidono.');
      return;
    }

    if (!token) {
      setErrorMessage('Token mancante.');
      return;
    }

    setIsSubmitting(true);

    try {
      const baseUrl = getApiBase();
      const response = await fetch(`${baseUrl}/api/v1/auth/activate-account`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ token, password }),
      });

      if (!response.ok) {
        const data = await response.json().catch(() => null);
        const message = data?.error ?? data?.message ?? "Impossibile attivare l'account. Riprova.";
        setErrorMessage(getErrorMessage(message, "Impossibile attivare l'account. Riprova."));
        setIsSubmitting(false);
        return;
      }

      const data = await response.json();
      setActivationSuccess(true);

      // Redirect based on requiresOnboarding flag
      const redirectTo = data.requiresOnboarding ? '/onboarding' : '/library';
      setTimeout(() => {
        void router.push(redirectTo);
      }, 1500);
    } catch (err) {
      setErrorMessage(getErrorMessage(err, "Impossibile attivare l'account. Riprova."));
      setIsSubmitting(false);
    }
  };

  // ──────────────────────────────────────────────
  // Render States
  // ──────────────────────────────────────────────

  // Loading: validating token
  if (isValidating) {
    return (
      <AuthLayout title="Verifica in corso...">
        <div className="text-center space-y-4 py-8">
          <div className="animate-spin text-4xl mb-4">&#9203;</div>
          <p className="text-slate-400">Verifica del token di invito...</p>
        </div>
      </AuthLayout>
    );
  }

  // No token in URL
  if (!token) {
    return (
      <AuthLayout title="Token mancante" subtitle="Nessun token di invito fornito">
        <div className="text-center space-y-4 py-4">
          <div className="text-6xl mb-4">&#9888;&#65039;</div>
          <p className="text-sm text-red-400">
            Token mancante. Utilizza il link ricevuto nell&apos;email di invito.
          </p>
          <div className="pt-4">
            <Btn variant="secondary" asChild>
              <Link href="/login">Vai al Login</Link>
            </Btn>
          </div>
        </div>
      </AuthLayout>
    );
  }

  // Invalid token
  if (validationResult && !validationResult.isValid) {
    const isAlreadyUsed = validationResult.errorReason === 'already_used';

    return (
      <AuthLayout
        title={isAlreadyUsed ? 'Invito gia utilizzato' : 'Invito non valido'}
        subtitle={
          isAlreadyUsed
            ? 'Questo invito e gia stato utilizzato.'
            : "L'invito non e valido o e scaduto. Contatta l'amministratore."
        }
      >
        <div className="text-center space-y-4 py-4">
          <div className="text-6xl mb-4">&#9888;&#65039;</div>
          <div className="pt-4 space-y-2">
            {isAlreadyUsed && (
              <Btn asChild fullWidth>
                <Link href="/login">Vai al Login</Link>
              </Btn>
            )}
            <Btn variant="secondary" asChild fullWidth>
              <Link href="/">Torna alla Home</Link>
            </Btn>
          </div>
        </div>
      </AuthLayout>
    );
  }

  // Activation success
  if (activationSuccess) {
    return (
      <AuthLayout
        title="Account attivato!"
        subtitle="Il tuo account e stato configurato con successo"
      >
        <div className="text-center space-y-4 py-4">
          <div className="text-6xl mb-4">&#9989;</div>
          <p className="text-sm text-slate-500 dark:text-slate-400">Reindirizzamento in corso...</p>
          <div className="animate-spin text-2xl mx-auto w-fit">&#9203;</div>
        </div>
      </AuthLayout>
    );
  }

  // Valid token: show password form
  const confirmError =
    confirmPassword && password !== confirmPassword ? 'Le password non coincidono' : undefined;

  return (
    <AuthLayout
      title="Configura Account"
      subtitle="Imposta la tua password per attivare il tuo account"
    >
      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-lg mb-4"
        >
          {errorMessage}
        </div>
      )}

      <form noValidate onSubmit={handleSubmit} className="space-y-4">
        {/* Email (readonly) */}
        <InputField
          label="Email"
          type="email"
          value={validationResult?.email ?? ''}
          onChange={() => {
            /* readonly */
          }}
          autoComplete="email"
          disabled
        />

        {/* Display Name (readonly) */}
        {validationResult?.displayName && (
          <InputField
            label="Nome"
            type="text"
            value={validationResult.displayName}
            onChange={() => {
              /* readonly */
            }}
            disabled
          />
        )}

        {/* Password */}
        <PwdInput
          label="Password"
          value={password}
          onChange={setPassword}
          required
          autoComplete="new-password"
          placeholder="Inserisci la password"
          showStrength
          strengthPrefix="Sicurezza password:"
        />

        {/* Password Requirements */}
        {password && (
          <div className="text-sm space-y-1">
            <div
              className={`flex items-center gap-2 ${
                passwordValidation.minLength
                  ? 'text-green-400'
                  : 'text-slate-500 dark:text-slate-400'
              }`}
            >
              <span aria-hidden="true">{passwordValidation.minLength ? '\u2713' : '\u25CB'}</span>
              <span>Almeno 8 caratteri</span>
            </div>
            <div
              className={`flex items-center gap-2 ${
                passwordValidation.hasUppercase
                  ? 'text-green-400'
                  : 'text-slate-500 dark:text-slate-400'
              }`}
            >
              <span aria-hidden="true">
                {passwordValidation.hasUppercase ? '\u2713' : '\u25CB'}
              </span>
              <span>Almeno 1 lettera maiuscola</span>
            </div>
            <div
              className={`flex items-center gap-2 ${
                passwordValidation.hasLowercase
                  ? 'text-green-400'
                  : 'text-slate-500 dark:text-slate-400'
              }`}
            >
              <span aria-hidden="true">
                {passwordValidation.hasLowercase ? '\u2713' : '\u25CB'}
              </span>
              <span>Almeno 1 lettera minuscola</span>
            </div>
            <div
              className={`flex items-center gap-2 ${
                passwordValidation.hasNumber
                  ? 'text-green-400'
                  : 'text-slate-500 dark:text-slate-400'
              }`}
            >
              <span aria-hidden="true">{passwordValidation.hasNumber ? '\u2713' : '\u25CB'}</span>
              <span>Almeno 1 numero</span>
            </div>
          </div>
        )}

        {/* Confirm Password */}
        <PwdInput
          label="Conferma Password"
          value={confirmPassword}
          onChange={setConfirmPassword}
          required
          autoComplete="new-password"
          placeholder="Conferma la password"
          error={confirmError}
        />

        {/* Submit */}
        <Btn
          type="submit"
          fullWidth
          className="mt-6"
          loading={isSubmitting}
          disabled={
            !passwordValidation.isValid || password !== confirmPassword || !confirmPassword.trim()
          }
        >
          {isSubmitting ? 'Attivazione in corso...' : 'Configura Account'}
        </Btn>
      </form>

      <div className="text-center mt-4">
        <Link
          href="/login"
          className="text-sm text-primary hover:text-primary/80 transition-colors focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 rounded px-2 py-1"
        >
          Hai gia un account? Accedi
        </Link>
      </div>
    </AuthLayout>
  );
}
