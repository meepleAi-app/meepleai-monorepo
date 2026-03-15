import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';

vi.mock('@/lib/api', () => ({
  api: {
    onboarding: {
      getStatus: vi.fn(),
      markWizardSeen: vi.fn(),
      dismiss: vi.fn(),
    },
  },
}));

import { useOnboardingStatus } from '../use-onboarding-status';
import { api } from '@/lib/api';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
}

describe('useOnboardingStatus', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('returns loading state initially', () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    expect(result.current.isLoading).toBe(true);
  });

  it('showWizard is true when wizardSeenAt is null', async () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: null,
      dismissedAt: null,
      steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
    });

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.showWizard).toBe(true);
    expect(result.current.showChecklist).toBe(true);
  });

  it('showWizard is false when wizardSeenAt is set', async () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: '2026-03-13T10:00:00Z',
      dismissedAt: null,
      steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
    });

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.showWizard).toBe(false);
    expect(result.current.showChecklist).toBe(true);
  });

  it('showChecklist is false when dismissedAt is set', async () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: '2026-03-13T10:00:00Z',
      dismissedAt: '2026-03-13T11:00:00Z',
      steps: { hasGames: true, hasSessions: false, hasCompletedProfile: false },
    });

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.showChecklist).toBe(false);
  });

  it('merges localStorage hasVisitedDiscover into steps', async () => {
    localStorage.setItem('hasVisitedDiscover', 'true');
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: null,
      dismissedAt: null,
      steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
    });

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // Wait for useEffect to update localStorage state
    await waitFor(() => expect(result.current.steps.hasVisitedDiscover).toBe(true));
    expect(result.current.completedCount).toBe(1);
  });

  it('calculates completedCount correctly', async () => {
    localStorage.setItem('hasVisitedDiscover', 'true');
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: null,
      dismissedAt: null,
      steps: { hasGames: true, hasSessions: false, hasCompletedProfile: true },
    });

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    await waitFor(() => expect(result.current.steps.hasVisitedDiscover).toBe(true));

    expect(result.current.completedCount).toBe(3); // games + discover + profile
    expect(result.current.totalSteps).toBe(4);
  });

  it('wizard and checklist are independent', async () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: null,
      dismissedAt: '2026-03-13T11:00:00Z',
      steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
    });

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.showWizard).toBe(true);
    expect(result.current.showChecklist).toBe(false);
  });

  it('showWizard is false during loading', () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    expect(result.current.isLoading).toBe(true);
    expect(result.current.showWizard).toBe(false);
    expect(result.current.showChecklist).toBe(false);
  });

  it('markWizardSeen closes dialog immediately via local state', async () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: null,
      dismissedAt: null,
      steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
    });
    (api.onboarding.markWizardSeen as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.showWizard).toBe(true));

    result.current.markWizardSeen();
    await waitFor(() => expect(result.current.showWizard).toBe(false));
    expect(api.onboarding.markWizardSeen).toHaveBeenCalledOnce();
  });

  it('dismiss closes checklist immediately via local state', async () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: '2026-03-13T10:00:00Z',
      dismissedAt: null,
      steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
    });
    (api.onboarding.dismiss as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.showChecklist).toBe(true));

    result.current.dismiss();
    await waitFor(() => expect(result.current.showChecklist).toBe(false));
    expect(api.onboarding.dismiss).toHaveBeenCalledOnce();
  });

  it('markWizardSeen closes dialog even if API fails', async () => {
    (api.onboarding.getStatus as ReturnType<typeof vi.fn>).mockResolvedValue({
      wizardSeenAt: null,
      dismissedAt: null,
      steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
    });
    (api.onboarding.markWizardSeen as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('Network error')
    );

    const { result } = renderHook(() => useOnboardingStatus(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.showWizard).toBe(true));

    result.current.markWizardSeen();
    // Dialog should close immediately via local state, even though API will fail
    await waitFor(() => expect(result.current.showWizard).toBe(false));
  });
});
