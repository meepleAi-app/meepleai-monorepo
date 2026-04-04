'use client';

/**
 * ProfileStep Component
 * Issue #132 - Profile setup during onboarding
 *
 * Allows the user to set a display name.
 * Avatar upload is optional and can be added later.
 */

import { FormEvent, useState } from 'react';

import { toast } from 'sonner';

import { AccessibleButton, AccessibleFormInput } from '@/components/accessible';
import { api } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils/errorHandler';

export interface ProfileStepProps {
  onComplete: () => void;
  onSkip: () => void;
}

export function ProfileStep({ onComplete, onSkip }: ProfileStepProps) {
  const [displayName, setDisplayName] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    if (!displayName.trim()) {
      onSkip();
      return;
    }

    setIsLoading(true);
    try {
      await api.auth.updateProfile({ displayName: displayName.trim() });
      toast.success('Profile updated!');
      onComplete();
    } catch (err) {
      setErrorMessage(getErrorMessage(err, 'Failed to update profile.'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-slate-900">Set Up Your Profile</h2>
        <p className="mt-1 text-sm text-slate-600">
          Choose a display name so other players can find you.
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
          label="Display Name"
          type="text"
          value={displayName}
          onChange={e => setDisplayName(e.target.value)}
          placeholder="e.g., BoardGameFan42"
          hint="This is how other players will see you"
          autoComplete="nickname"
        />

        <div className="flex items-center gap-3 pt-2">
          <AccessibleButton
            type="submit"
            variant="primary"
            className="flex-1"
            isLoading={isLoading}
            loadingText="Saving..."
          >
            Save Profile
          </AccessibleButton>
          <button
            type="button"
            onClick={onSkip}
            className="text-sm text-slate-500 hover:text-slate-700"
            data-testid="profile-skip"
          >
            Skip for now
          </button>
        </div>
      </form>
    </div>
  );
}
