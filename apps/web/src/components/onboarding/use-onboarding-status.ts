'use client';

import { useState, useEffect } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { OnboardingStatusResponse } from '@/lib/api/clients/onboardingClient';

import { TOTAL_STEPS } from './onboarding-steps';

export interface OnboardingSteps {
  hasGames: boolean;
  hasSessions: boolean;
  hasCompletedProfile: boolean;
  hasVisitedDiscover: boolean;
}

export interface OnboardingStatus {
  isLoading: boolean;
  showWizard: boolean;
  showChecklist: boolean;
  steps: OnboardingSteps;
  completedCount: number;
  totalSteps: number;
  dismiss: () => void;
  markWizardSeen: () => void;
}

const QUERY_KEY = ['onboarding-status'] as const;

export function useOnboardingStatus(): OnboardingStatus {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEY,
    queryFn: () => api.onboarding.getStatus(),
    staleTime: 30_000,
  });

  // Local dismiss flags so the dialog closes immediately on user action,
  // regardless of whether the API mutation succeeds.
  const [localWizardDismissed, setLocalWizardDismissed] = useState(false);
  const [localChecklistDismissed, setLocalChecklistDismissed] = useState(false);

  // Read localStorage client-side only to avoid SSR hydration mismatch
  const [hasVisitedDiscover, setHasVisitedDiscover] = useState(false);
  useEffect(() => {
    setHasVisitedDiscover(localStorage.getItem('hasVisitedDiscover') === 'true');
  }, []);

  const markWizardSeenMutation = useMutation({
    mutationFn: () => api.onboarding.markWizardSeen(),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: QUERY_KEY });
      const previous = queryClient.getQueryData<OnboardingStatusResponse>(QUERY_KEY);
      queryClient.setQueryData<OnboardingStatusResponse>(QUERY_KEY, old =>
        old ? { ...old, wizardSeenAt: new Date().toISOString() } : old
      );
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(QUERY_KEY, context.previous);
      }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });

  const dismissMutation = useMutation({
    mutationFn: () => api.onboarding.dismiss(),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: QUERY_KEY });
      const previous = queryClient.getQueryData<OnboardingStatusResponse>(QUERY_KEY);
      queryClient.setQueryData<OnboardingStatusResponse>(QUERY_KEY, old =>
        old ? { ...old, dismissedAt: new Date().toISOString() } : old
      );
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(QUERY_KEY, context.previous);
      }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
  });

  const steps: OnboardingSteps = {
    hasGames: data?.steps.hasGames ?? false,
    hasSessions: data?.steps.hasSessions ?? false,
    hasCompletedProfile: data?.steps.hasCompletedProfile ?? false,
    hasVisitedDiscover,
  };

  const completedCount = Object.values(steps).filter(Boolean).length;

  return {
    isLoading,
    showWizard: !isLoading && !!data && !data.wizardSeenAt && !localWizardDismissed,
    showChecklist: !isLoading && !!data && !data.dismissedAt && !localChecklistDismissed,
    steps,
    completedCount,
    totalSteps: TOTAL_STEPS,
    dismiss: () => {
      setLocalChecklistDismissed(true);
      dismissMutation.mutate();
    },
    markWizardSeen: () => {
      setLocalWizardDismissed(true);
      markWizardSeenMutation.mutate();
    },
  };
}
