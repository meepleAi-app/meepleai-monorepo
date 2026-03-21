'use client';

import { Suspense, useCallback, useEffect, useMemo, useState } from 'react';

import { Check, CheckCircle2, Loader2, Lock, X } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

type PageState = 'loading' | 'valid' | 'submitting' | 'success';

interface PasswordRequirement {
  label: string;
  test: (pw: string) => boolean;
}

const PASSWORD_REQUIREMENTS: PasswordRequirement[] = [
  { label: 'At least 8 characters', test: pw => pw.length >= 8 },
  { label: 'One uppercase letter', test: pw => /[A-Z]/.test(pw) },
  { label: 'One number', test: pw => /\d/.test(pw) },
  {
    label: 'One special character',
    test: pw => /[!@#$%^&*()_+\-=[\]{};':"\\|,.<>/?]/.test(pw),
  },
];

function getStrengthLevel(password: string): number {
  return PASSWORD_REQUIREMENTS.filter(r => r.test(password)).length;
}

const STRENGTH_COLORS = ['bg-red-500', 'bg-orange-500', 'bg-yellow-500', 'bg-green-500'];

const STRENGTH_LABELS = ['Weak', 'Fair', 'Good', 'Strong'];

export default function AcceptInvitePage() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center min-h-screen">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <AcceptInviteContent />
    </Suspense>
  );
}

function AcceptInviteContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const token = searchParams.get('token');

  const [state, setState] = useState<PageState>('loading');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [submitError, setSubmitError] = useState('');

  // Validate token on mount
  useEffect(() => {
    if (!token) {
      router.push('/invitation-expired');
      return;
    }

    let cancelled = false;

    async function validateToken() {
      try {
        const response = await fetch('/api/v1/auth/validate-invitation', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token }),
        });

        if (cancelled) return;

        if (!response.ok) {
          if (cancelled) return;
          router.push('/invitation-expired');
          return;
        }

        const data = await response.json();

        if (!data.isValid) {
          const reason =
            data.errorReason === 'already_used'
              ? 'This invitation has already been used. If you already have an account, please log in.'
              : 'This invitation is invalid or has expired. Please contact your administrator.';
          setErrorMessage(reason);
          setState('invalid');
          return;
        }

        setEmail(data.email ?? '');
        setState('valid');
      } catch {
        if (cancelled) return;
        router.push('/invitation-expired');
      }
    }

    validateToken();
    return () => {
      cancelled = true;
    };
  }, [token, router]);

  const strengthLevel = useMemo(() => getStrengthLevel(password), [password]);
  const passwordsMatch = confirmPassword.length > 0 && password === confirmPassword;
  const allRequirementsMet = strengthLevel === PASSWORD_REQUIREMENTS.length;
  const canSubmit = allRequirementsMet && passwordsMatch;

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      if (!canSubmit) return;

      setState('submitting');
      setSubmitError('');

      try {
        const response = await fetch('/api/v1/auth/accept-invitation', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token, password, confirmPassword }),
        });

        if (!response.ok) {
          const data = await response.json().catch(() => null);
          setSubmitError(data?.message ?? 'Failed to create your account. Please try again.');
          setState('valid');
          return;
        }

        setState('success');
        setTimeout(() => router.push('/onboarding'), 1500);
      } catch {
        setSubmitError('A network error occurred. Please check your connection and try again.');
        setState('valid');
      }
    },
    [canSubmit, token, password, confirmPassword, router]
  );

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      {state === 'loading' && <LoadingCard />}
      {state === 'success' && <SuccessCard />}
      {(state === 'valid' || state === 'submitting') && (
        <PasswordFormCard
          email={email}
          password={password}
          confirmPassword={confirmPassword}
          onPasswordChange={setPassword}
          onConfirmPasswordChange={setConfirmPassword}
          onSubmit={handleSubmit}
          strengthLevel={strengthLevel}
          passwordsMatch={passwordsMatch}
          canSubmit={canSubmit}
          isSubmitting={state === 'submitting'}
          submitError={submitError}
        />
      )}
    </div>
  );
}

function LoadingCard() {
  return (
    <Card className="w-full max-w-md">
      <CardContent className="flex flex-col items-center gap-4 py-12">
        <Loader2 className="h-10 w-10 animate-spin text-amber-600" aria-label="Loading" />
        <p className="text-muted-foreground text-sm">Validating your invitation...</p>
      </CardContent>
    </Card>
  );
}

function SuccessCard() {
  return (
    <Card className="w-full max-w-md">
      <CardHeader className="items-center text-center">
        <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/30">
          <CheckCircle2 className="h-6 w-6 text-green-600 dark:text-green-400" />
        </div>
        <CardTitle>Account Created!</CardTitle>
        <CardDescription>
          Your password has been set successfully. Setting up your account...
        </CardDescription>
      </CardHeader>
    </Card>
  );
}

interface PasswordFormCardProps {
  email: string;
  password: string;
  confirmPassword: string;
  onPasswordChange: (value: string) => void;
  onConfirmPasswordChange: (value: string) => void;
  onSubmit: (e: React.FormEvent) => void;
  strengthLevel: number;
  passwordsMatch: boolean;
  canSubmit: boolean;
  isSubmitting: boolean;
  submitError: string;
}

function PasswordFormCard({
  email,
  password,
  confirmPassword,
  onPasswordChange,
  onConfirmPasswordChange,
  onSubmit,
  strengthLevel,
  passwordsMatch,
  canSubmit,
  isSubmitting,
  submitError,
}: PasswordFormCardProps) {
  return (
    <Card className="w-full max-w-md">
      <CardHeader className="items-center text-center">
        <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
          <Lock className="h-6 w-6 text-amber-600 dark:text-amber-400" />
        </div>
        <CardTitle>Welcome to MeepleAI</CardTitle>
        <CardDescription>Set your password to complete your account setup</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="space-y-5">
          {/* Email (readonly) */}
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" value={email} readOnly className="bg-muted" />
          </div>

          {/* Password */}
          <div className="space-y-2">
            <Label htmlFor="password">Password</Label>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={e => onPasswordChange(e.target.value)}
              placeholder="Enter your password"
              autoComplete="new-password"
              disabled={isSubmitting}
            />

            {/* Strength bar */}
            {password.length > 0 && (
              <div className="space-y-2">
                <div className="flex gap-1">
                  {Array.from({ length: 4 }).map((_, i) => (
                    <div
                      key={i}
                      className={`h-1.5 flex-1 rounded-full transition-colors ${
                        i < strengthLevel ? STRENGTH_COLORS[strengthLevel - 1] : 'bg-muted'
                      }`}
                    />
                  ))}
                </div>
                <p className="text-muted-foreground text-xs">
                  Strength: {strengthLevel > 0 ? STRENGTH_LABELS[strengthLevel - 1] : 'Too weak'}
                </p>
              </div>
            )}

            {/* Requirements checklist */}
            {password.length > 0 && (
              <ul className="space-y-1 pt-1">
                {PASSWORD_REQUIREMENTS.map(req => {
                  const met = req.test(password);
                  return (
                    <li key={req.label} className="flex items-center gap-2 text-xs">
                      {met ? (
                        <Check className="h-3.5 w-3.5 text-green-600" />
                      ) : (
                        <X className="text-muted-foreground h-3.5 w-3.5" />
                      )}
                      <span
                        className={
                          met ? 'text-green-700 dark:text-green-400' : 'text-muted-foreground'
                        }
                      >
                        {req.label}
                      </span>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>

          {/* Confirm password */}
          <div className="space-y-2">
            <Label htmlFor="confirmPassword">Confirm Password</Label>
            <Input
              id="confirmPassword"
              type="password"
              value={confirmPassword}
              onChange={e => onConfirmPasswordChange(e.target.value)}
              placeholder="Confirm your password"
              autoComplete="new-password"
              disabled={isSubmitting}
            />
            {confirmPassword.length > 0 && !passwordsMatch && (
              <p className="text-xs text-red-600 dark:text-red-400">Passwords do not match</p>
            )}
            {passwordsMatch && (
              <p className="flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
                <Check className="h-3.5 w-3.5" />
                Passwords match
              </p>
            )}
          </div>

          {/* Submit error */}
          {submitError && (
            <div className="rounded-md bg-red-50 p-3 text-sm text-red-700 dark:bg-red-900/20 dark:text-red-400">
              {submitError}
            </div>
          )}

          {/* Submit button */}
          <Button type="submit" className="w-full" disabled={!canSubmit || isSubmitting}>
            {isSubmitting ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Creating account...
              </>
            ) : (
              'Create Account'
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
