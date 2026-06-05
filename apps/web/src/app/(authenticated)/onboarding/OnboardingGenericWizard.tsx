'use client';

import { useCallback, useMemo, useState } from 'react';

import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { useAuth } from '@/components/auth/AuthProvider';
import { FirstGameStep } from '@/components/onboarding/FirstGameStep';
import { InterestsStep } from '@/components/onboarding/InterestsStep';
import { WizardModal, type WizardStep } from '@/components/ui/wizard-modal';
import { api } from '@/lib/api';

import { InviteFriendComingSoonStep } from './InviteFriendComingSoonStep';

interface OnboardingGenericWizardProps {
  userName: string | null;
}

/**
 * Asse D follow-up P3 — /onboarding 3-step generic wizard.
 *
 * Umbrella #1895 sub-issue #1899 follow-up. Replaces the legacy
 * `OnboardingTourClient` 5-step page-flow with the asse-B `WizardModal`
 * primitive while reusing the step components shipped in Issue #132.
 *
 * Flow:
 *  - Step 1 (Interests): `InterestsStep` with 9 board game categories.
 *  - Step 2 (First Game): `FirstGameStep` searches the internal catalog
 *    (NOT BoardGameGeek — user-side BGG access is blocked by ToS, see ADR
 *    #1903). The reused component already targets `api.games.getAll`.
 *  - Step 3 (Invite Friend): `InviteFriendComingSoonStep` placeholder,
 *    skip-only. Real invite feature deferred to a future sub-issue.
 *
 * This is NOT the invited-user `OnboardingWizard` (token-based, 5 step) which
 * remains the entry point for `/accept-invite` — that flow is untouched.
 *
 * After completion the user is redirected to `/library`, and the backend
 * marks `user.onboardingCompleted = true` via `api.auth.completeOnboarding`.
 */
export function OnboardingGenericWizard({ userName }: OnboardingGenericWizardProps) {
  const router = useRouter();
  const { refreshUser } = useAuth();
  const [open, setOpen] = useState(true);

  // Step internal completion flags (per state-machine progression).
  // The reused step components signal completion via `onComplete`/`onSkip`,
  // and we gate the wizard `validate` callback on those flags so Next does
  // not advance until the user has interacted with each step.
  const [interestsCompleted, setInterestsCompleted] = useState(false);
  const [firstGameCompleted, setFirstGameCompleted] = useState(false);

  const handleComplete = useCallback(async () => {
    try {
      await api.auth.completeOnboarding(false);
      await refreshUser();
      toast.success('Onboarding completato! Benvenuto.');
      router.replace('/library');
    } catch {
      toast.error('Errore completamento onboarding. Riprova.');
    }
  }, [refreshUser, router]);

  const handleCancel = useCallback(() => {
    setOpen(false);
    router.replace('/library');
  }, [router]);

  const steps = useMemo<WizardStep[]>(
    () => [
      {
        title: userName ? `Ciao ${userName}, scegli i tuoi interessi` : 'Scegli i tuoi interessi',
        content: (
          <InterestsStep
            onComplete={() => setInterestsCompleted(true)}
            onSkip={() => setInterestsCompleted(true)}
          />
        ),
        validate: () => interestsCompleted,
        optional: true,
      },
      {
        title: 'Aggiungi il tuo primo gioco',
        content: (
          <FirstGameStep
            onComplete={() => setFirstGameCompleted(true)}
            onSkip={() => setFirstGameCompleted(true)}
            onGameAdded={() => setFirstGameCompleted(true)}
          />
        ),
        validate: () => firstGameCompleted,
        optional: true,
      },
      {
        title: 'Invita un amico',
        content: <InviteFriendComingSoonStep />,
        optional: true,
      },
    ],
    [userName, interestsCompleted, firstGameCompleted]
  );

  return (
    <WizardModal
      steps={steps}
      onComplete={handleComplete}
      onCancel={handleCancel}
      open={open}
      onOpenChange={setOpen}
    />
  );
}
