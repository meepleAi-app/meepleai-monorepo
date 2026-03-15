import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { OnboardingWizard } from '../OnboardingWizard';

// Mock all step components to isolate wizard logic
vi.mock('../PasswordStep', () => ({
  PasswordStep: ({ onComplete }: { onComplete: () => void }) => (
    <div data-testid="password-step">
      <button onClick={onComplete}>Complete Password</button>
    </div>
  ),
}));

vi.mock('../ProfileStep', () => ({
  ProfileStep: ({ onComplete, onSkip }: { onComplete: () => void; onSkip: () => void }) => (
    <div data-testid="profile-step">
      <button onClick={onComplete}>Complete Profile</button>
      <button onClick={onSkip}>Skip Profile</button>
    </div>
  ),
}));

vi.mock('../InterestsStep', () => ({
  InterestsStep: ({ onComplete, onSkip }: { onComplete: () => void; onSkip: () => void }) => (
    <div data-testid="interests-step">
      <button onClick={onComplete}>Complete Interests</button>
      <button onClick={onSkip}>Skip Interests</button>
    </div>
  ),
}));

vi.mock('../FirstGameStep', () => ({
  FirstGameStep: ({
    onComplete,
    onSkip,
    onGameAdded,
  }: {
    onComplete: () => void;
    onSkip: () => void;
    onGameAdded: (id: string, name: string) => void;
  }) => (
    <div data-testid="first-game-step">
      <button onClick={onSkip}>Skip Game</button>
      <button
        onClick={() => {
          onGameAdded('game-1', 'Catan');
          onComplete();
        }}
      >
        Add Game
      </button>
    </div>
  ),
}));

vi.mock('../FirstAgentStep', () => ({
  FirstAgentStep: ({ onComplete, onSkip }: { onComplete: () => void; onSkip: () => void }) => (
    <div data-testid="first-agent-step">
      <button onClick={onComplete}>Create Agent</button>
      <button onClick={onSkip}>Skip Agent</button>
    </div>
  ),
}));

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: vi.fn(),
    prefetch: vi.fn(),
    back: vi.fn(),
  }),
}));

// Mock API client for onboarding completion
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      completeOnboarding: vi.fn().mockResolvedValue({ ok: true, message: 'done' }),
    },
  },
}));

describe('OnboardingWizard', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the wizard with step 1 (password)', () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    expect(screen.getByText('Welcome to MeepleAI')).toBeInTheDocument();
    expect(screen.getByTestId('password-step')).toBeInTheDocument();
    expect(screen.getByText(/Step 1 of 4/)).toBeInTheDocument();
  });

  it('shows 4 progress steps by default (no game added)', () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    expect(screen.getByTestId('progress-step-1')).toBeInTheDocument();
    expect(screen.getByTestId('progress-step-2')).toBeInTheDocument();
    expect(screen.getByTestId('progress-step-3')).toBeInTheDocument();
    expect(screen.getByTestId('progress-step-4')).toBeInTheDocument();
    expect(screen.queryByTestId('progress-step-5')).not.toBeInTheDocument();
  });

  it('does not show skip wizard link before password is completed', () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    expect(screen.queryByTestId('skip-wizard')).not.toBeInTheDocument();
  });

  it('advances to step 2 after password completion', async () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    await user.click(screen.getByText('Complete Password'));

    expect(screen.getByTestId('profile-step')).toBeInTheDocument();
    expect(screen.getByText(/Step 2 of 4/)).toBeInTheDocument();
  });

  it('shows skip wizard link after password is completed', async () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    await user.click(screen.getByText('Complete Password'));

    expect(screen.getByTestId('skip-wizard')).toBeInTheDocument();
  });

  it('skip wizard navigates to /dashboard', async () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    await user.click(screen.getByText('Complete Password'));
    await user.click(screen.getByTestId('skip-wizard'));

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/dashboard');
    });
  });

  it('navigates through all steps without game', async () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    // Step 1 -> 2
    await user.click(screen.getByText('Complete Password'));
    expect(screen.getByTestId('profile-step')).toBeInTheDocument();

    // Step 2 -> 3
    await user.click(screen.getByText('Complete Profile'));
    expect(screen.getByTestId('interests-step')).toBeInTheDocument();

    // Step 3 -> 4
    await user.click(screen.getByText('Complete Interests'));
    expect(screen.getByTestId('first-game-step')).toBeInTheDocument();
    expect(screen.getByText(/Step 4 of 4/)).toBeInTheDocument();
  });

  it('shows 5 steps when game is added', async () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    // Complete steps 1-3
    await user.click(screen.getByText('Complete Password'));
    await user.click(screen.getByText('Complete Profile'));
    await user.click(screen.getByText('Complete Interests'));

    // Add game in step 4 -> step 5 appears
    await user.click(screen.getByText('Add Game'));

    expect(screen.getByTestId('first-agent-step')).toBeInTheDocument();
    expect(screen.getByText(/Step 5 of 5/)).toBeInTheDocument();
    expect(screen.getByTestId('progress-step-5')).toBeInTheDocument();
  });

  it('back button goes to previous step', async () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    // Complete password → advance to profile (step 2)
    await user.click(screen.getByText('Complete Password'));
    expect(screen.getByTestId('profile-step')).toBeInTheDocument();

    // Advance to interests (step 3)
    await user.click(screen.getByText('Complete Profile'));
    expect(screen.getByTestId('interests-step')).toBeInTheDocument();

    // Back → returns to profile (step 2), not password (wizard prevents going back to password)
    await user.click(screen.getByTestId('wizard-back'));
    expect(screen.getByTestId('profile-step')).toBeInTheDocument();
  });

  describe('startStep prop', () => {
    it('starts at step 2 when startStep=2 is provided', () => {
      renderWithQuery(<OnboardingWizard token="" role="User" startStep={2} />);

      expect(screen.getByTestId('profile-step')).toBeInTheDocument();
      expect(screen.getByText(/Step 2 of 4/)).toBeInTheDocument();
      expect(screen.queryByTestId('password-step')).not.toBeInTheDocument();
    });

    it('shows skip wizard link immediately when startStep > 1', () => {
      renderWithQuery(<OnboardingWizard token="" role="User" startStep={2} />);

      expect(screen.getByTestId('skip-wizard')).toBeInTheDocument();
    });

    it('back button on step 2 does not go to step 1 when startStep=2', async () => {
      renderWithQuery(<OnboardingWizard token="" role="User" startStep={2} />);

      // Advance to step 3
      await user.click(screen.getByText('Complete Profile'));
      expect(screen.getByTestId('interests-step')).toBeInTheDocument();

      // Back → should stay at step 2 (not go to password)
      await user.click(screen.getByTestId('wizard-back'));
      expect(screen.getByTestId('profile-step')).toBeInTheDocument();
    });
  });

  it('does not show back button on step 1', () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    expect(screen.queryByTestId('wizard-back')).not.toBeInTheDocument();
  });

  it('finish button navigates to /dashboard', async () => {
    renderWithQuery(<OnboardingWizard token="test-token" role="Player" />);

    // Go through all 4 steps
    await user.click(screen.getByText('Complete Password'));
    await user.click(screen.getByText('Complete Profile'));
    await user.click(screen.getByText('Complete Interests'));

    // Step 4 is the last step without game
    expect(screen.getByTestId('wizard-finish')).toBeInTheDocument();

    await user.click(screen.getByTestId('wizard-finish'));

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/dashboard');
    });
  });
});
