/**
 * OnboardingGenericWizard — unit tests.
 *
 * Asse D follow-up P3 (umbrella #1895 sub-issue #1899 follow-up).
 *
 * The test mocks the `WizardModal` primitive (already covered by its own
 * suite under `apps/web/src/components/ui/wizard-modal/__tests__/`) and the
 * two reused step components (`InterestsStep` / `FirstGameStep`). It focuses
 * on the wrapper-level contract:
 *  - 3 steps rendered with correct titles (incl. userName personalisation).
 *  - Reused step components mounted with proper props bridging.
 *  - Coming-soon placeholder mounted for step 3.
 *  - `onComplete` wiring: api.auth.completeOnboarding(false) → refreshUser →
 *    router.replace('/library') + success toast.
 *  - `onCancel` wiring: closes wizard and routes to /library.
 *  - Resilience: failed completeOnboarding shows error toast (no redirect).
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { WizardStep } from '@/components/ui/wizard-modal';

import { OnboardingGenericWizard } from '../OnboardingGenericWizard';

const replace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace }),
}));

const completeOnboarding = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      completeOnboarding: (...args: unknown[]) => completeOnboarding(...args),
    },
  },
}));

const refreshUser = vi.fn().mockResolvedValue(undefined);
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ refreshUser }),
}));

const toastSuccess = vi.fn();
const toastError = vi.fn();
vi.mock('sonner', () => ({
  toast: {
    success: (...args: unknown[]) => toastSuccess(...args),
    error: (...args: unknown[]) => toastError(...args),
  },
}));

vi.mock('@/components/onboarding/InterestsStep', () => ({
  InterestsStep: ({ onComplete, onSkip }: { onComplete: () => void; onSkip: () => void }) => (
    <div data-testid="interests-step-mock">
      <button data-testid="interests-complete" onClick={onComplete}>
        Complete interests
      </button>
      <button data-testid="interests-skip" onClick={onSkip}>
        Skip interests
      </button>
    </div>
  ),
}));

vi.mock('@/components/onboarding/FirstGameStep', () => ({
  FirstGameStep: ({
    onComplete,
    onSkip,
    onGameAdded,
  }: {
    onComplete: () => void;
    onSkip: () => void;
    onGameAdded: (id: string, name: string) => void;
  }) => (
    <div data-testid="firstgame-step-mock">
      <button data-testid="firstgame-complete" onClick={onComplete}>
        Complete firstgame
      </button>
      <button data-testid="firstgame-skip" onClick={onSkip}>
        Skip firstgame
      </button>
      <button data-testid="firstgame-add" onClick={() => onGameAdded('game-1', 'Catan')}>
        Add game
      </button>
    </div>
  ),
}));

// Render all wizard steps inline so we can assert all three independently.
vi.mock('@/components/ui/wizard-modal', () => ({
  WizardModal: ({
    steps,
    onComplete,
    onCancel,
    open,
  }: {
    steps: WizardStep[];
    onComplete: (data: unknown) => Promise<void>;
    onCancel: () => void;
    open: boolean;
  }) =>
    open ? (
      <div data-testid="wizard-modal-mock" role="dialog">
        {steps.map((s, i) => (
          <div key={i} data-testid={`step-${i}`}>
            <h2 data-testid={`step-${i}-title`}>{s.title}</h2>
            <div data-testid={`step-${i}-content`}>{s.content as ReactNode}</div>
          </div>
        ))}
        <button data-testid="wizard-complete" onClick={() => void onComplete({})}>
          Complete
        </button>
        <button data-testid="wizard-cancel" onClick={onCancel}>
          Cancel
        </button>
      </div>
    ) : null,
}));

beforeEach(() => {
  replace.mockReset();
  completeOnboarding.mockReset();
  refreshUser.mockReset().mockResolvedValue(undefined);
  toastSuccess.mockReset();
  toastError.mockReset();
});

afterEach(() => {
  vi.clearAllMocks();
});

describe('OnboardingGenericWizard (Asse D P3 #1899)', () => {
  it('renders a 3-step wizard modal', () => {
    render(<OnboardingGenericWizard userName={null} />);
    expect(screen.getByTestId('wizard-modal-mock')).toBeInTheDocument();
    expect(screen.getByTestId('step-0')).toBeInTheDocument();
    expect(screen.getByTestId('step-1')).toBeInTheDocument();
    expect(screen.getByTestId('step-2')).toBeInTheDocument();
    expect(screen.queryByTestId('step-3')).not.toBeInTheDocument();
  });

  it('step 1 title personalises with userName when provided', () => {
    render(<OnboardingGenericWizard userName="Luca" />);
    expect(screen.getByTestId('step-0-title')).toHaveTextContent(
      'Ciao Luca, scegli i tuoi interessi'
    );
  });

  it('step 1 title falls back to generic when userName is null', () => {
    render(<OnboardingGenericWizard userName={null} />);
    expect(screen.getByTestId('step-0-title')).toHaveTextContent('Scegli i tuoi interessi');
  });

  it('step 1 renders InterestsStep with onComplete + onSkip bridge', () => {
    render(<OnboardingGenericWizard userName={null} />);
    const step0 = screen.getByTestId('step-0-content');
    expect(step0).toContainElement(screen.getByTestId('interests-step-mock'));
    expect(screen.getByTestId('interests-complete')).toBeInTheDocument();
    expect(screen.getByTestId('interests-skip')).toBeInTheDocument();
  });

  it('step 2 renders FirstGameStep with onComplete + onSkip + onGameAdded bridge', () => {
    render(<OnboardingGenericWizard userName={null} />);
    const step1 = screen.getByTestId('step-1-content');
    expect(step1).toContainElement(screen.getByTestId('firstgame-step-mock'));
    expect(screen.getByTestId('firstgame-complete')).toBeInTheDocument();
    expect(screen.getByTestId('firstgame-skip')).toBeInTheDocument();
    expect(screen.getByTestId('firstgame-add')).toBeInTheDocument();
    expect(screen.getByTestId('step-1-title')).toHaveTextContent('Aggiungi il tuo primo gioco');
  });

  it('step 3 renders the coming-soon placeholder with correct title', () => {
    render(<OnboardingGenericWizard userName={null} />);
    expect(screen.getByTestId('step-2-title')).toHaveTextContent('Invita un amico');
    expect(screen.getByTestId('invite-friend-coming-soon')).toBeInTheDocument();
  });

  it('onComplete calls completeOnboarding(false), refreshes user, toasts, and routes /library', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockResolvedValue({ ok: true, message: 'done' });
    render(<OnboardingGenericWizard userName="Luca" />);

    await user.click(screen.getByTestId('wizard-complete'));

    await waitFor(() => {
      expect(completeOnboarding).toHaveBeenCalledWith(false);
    });
    await waitFor(() => expect(refreshUser).toHaveBeenCalledTimes(1));
    expect(toastSuccess).toHaveBeenCalledWith('Onboarding completato! Benvenuto.');
    expect(replace).toHaveBeenCalledWith('/library');
    expect(toastError).not.toHaveBeenCalled();
  });

  it('onComplete shows error toast and does NOT redirect when API fails', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockRejectedValue(new Error('network'));
    render(<OnboardingGenericWizard userName={null} />);

    await user.click(screen.getByTestId('wizard-complete'));

    await waitFor(() => expect(toastError).toHaveBeenCalled());
    expect(toastError).toHaveBeenCalledWith('Errore completamento onboarding. Riprova.');
    expect(replace).not.toHaveBeenCalled();
    expect(toastSuccess).not.toHaveBeenCalled();
  });

  it('onCancel closes wizard and routes to /library', async () => {
    const user = userEvent.setup();
    render(<OnboardingGenericWizard userName={null} />);

    await user.click(screen.getByTestId('wizard-cancel'));

    expect(replace).toHaveBeenCalledWith('/library');
    expect(completeOnboarding).not.toHaveBeenCalled();
  });
});
