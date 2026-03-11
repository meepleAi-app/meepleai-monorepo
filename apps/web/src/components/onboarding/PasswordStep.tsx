'use client';

/**
 * PasswordStep Component
 * Issue #132 - Password setup during onboarding
 *
 * Reuses password validation pattern from reset-password page.
 * Calls acceptInvitation API to create the user account.
 */

import { FormEvent, useEffect, useState } from 'react';

import { motion } from 'framer-motion';
import { toast } from 'sonner';

import { AccessibleButton, AccessibleFormInput } from '@/components/accessible';
import { api } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils/errorHandler';

export interface PasswordStepProps {
  token: string;
  onComplete: () => void;
}

type PasswordStrength = 'weak' | 'medium' | 'strong';

interface PasswordValidation {
  minLength: boolean;
  hasUppercase: boolean;
  hasLowercase: boolean;
  hasNumber: boolean;
  isValid: boolean;
  strength: PasswordStrength;
}

function validatePassword(password: string): PasswordValidation {
  const minLength = password.length >= 8;
  const hasUppercase = /[A-Z]/.test(password);
  const hasLowercase = /[a-z]/.test(password);
  const hasNumber = /[0-9]/.test(password);

  const met = [minLength, hasUppercase, hasLowercase, hasNumber].filter(Boolean).length;

  let strength: PasswordStrength = 'weak';
  if (met === 4) {
    strength = 'strong';
  } else if (met >= 2 && minLength) {
    strength = 'medium';
  }

  return { minLength, hasUppercase, hasLowercase, hasNumber, isValid: met === 4, strength };
}

const strengthConfig = {
  weak: { color: 'bg-red-500', text: 'Weak', textColor: 'text-red-600', width: 'w-1/3' },
  medium: {
    color: 'bg-orange-500',
    text: 'Medium',
    textColor: 'text-orange-600',
    width: 'w-2/3',
  },
  strong: {
    color: 'bg-green-500',
    text: 'Strong',
    textColor: 'text-green-600',
    width: 'w-full',
  },
};

export function PasswordStep({ token, onComplete }: PasswordStepProps) {
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [validation, setValidation] = useState<PasswordValidation>({
    minLength: false,
    hasUppercase: false,
    hasLowercase: false,
    hasNumber: false,
    isValid: false,
    strength: 'weak',
  });
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    setValidation(
      password
        ? validatePassword(password)
        : {
            minLength: false,
            hasUppercase: false,
            hasLowercase: false,
            hasNumber: false,
            isValid: false,
            strength: 'weak',
          }
    );
  }, [password]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    if (!validation.isValid) {
      setErrorMessage('Password does not meet all requirements.');
      return;
    }
    if (password !== confirmPassword) {
      setErrorMessage('Passwords do not match.');
      return;
    }

    setIsLoading(true);
    try {
      await api.invitations.acceptInvitation(token, password, confirmPassword);
      toast.success('Account created successfully!');
      onComplete();
    } catch (err) {
      setErrorMessage(getErrorMessage(err, 'Failed to create account. Please try again.'));
    } finally {
      setIsLoading(false);
    }
  };

  const config = strengthConfig[validation.strength];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-slate-900">
          Create Your Password
        </h2>
        <p className="mt-1 text-sm text-slate-600">
          Choose a strong password for your new account.
        </p>
      </div>

      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          {errorMessage}
        </div>
      )}

      <form noValidate onSubmit={handleSubmit} className="space-y-4">
        <AccessibleFormInput
          label="Password"
          type="password"
          value={password}
          onChange={e => setPassword(e.target.value)}
          required
          autoComplete="new-password"
          placeholder="Enter your password"
        />

        {/* Strength meter */}
        {password && (
          <div className="space-y-2" data-testid="password-strength">
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-600">Password strength:</span>
              <span className={`text-sm font-medium ${config.textColor}`}>{config.text}</span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-slate-200">
              <motion.div
                className={`h-full ${config.color}`}
                initial={{ width: 0 }}
                animate={{ width: config.width }}
                transition={{ duration: 0.3 }}
              />
            </div>
          </div>
        )}

        {/* Validation rules checklist */}
        {password && (
          <div className="space-y-1 text-sm" data-testid="password-rules">
            {[
              { key: 'minLength', label: 'At least 8 characters' },
              { key: 'hasUppercase', label: 'At least 1 uppercase letter' },
              { key: 'hasLowercase', label: 'At least 1 lowercase letter' },
              { key: 'hasNumber', label: 'At least 1 number' },
            ].map(({ key, label }) => (
              <div
                key={key}
                className={`flex items-center gap-2 ${
                  validation[key as keyof PasswordValidation] ? 'text-green-600' : 'text-slate-500'
                }`}
              >
                <span aria-hidden="true">
                  {validation[key as keyof PasswordValidation] ? '\u2713' : '\u25CB'}
                </span>
                <span>{label}</span>
              </div>
            ))}
          </div>
        )}

        <AccessibleFormInput
          label="Confirm Password"
          type="password"
          value={confirmPassword}
          onChange={e => setConfirmPassword(e.target.value)}
          required
          autoComplete="new-password"
          placeholder="Confirm your password"
          error={
            confirmPassword && password !== confirmPassword ? 'Passwords do not match' : undefined
          }
        />

        <AccessibleButton
          type="submit"
          variant="primary"
          className="w-full mt-4"
          isLoading={isLoading}
          loadingText="Creating account..."
          disabled={!validation.isValid || password !== confirmPassword || !confirmPassword.trim()}
        >
          Create Account
        </AccessibleButton>
      </form>
    </div>
  );
}
