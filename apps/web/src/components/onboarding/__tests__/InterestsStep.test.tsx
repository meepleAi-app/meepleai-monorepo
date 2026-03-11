import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { InterestsStep } from '../InterestsStep';

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('InterestsStep', () => {
  const user = userEvent.setup();
  const onComplete = vi.fn();
  const onSkip = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders all 9 category options', () => {
    renderWithQuery(<InterestsStep onComplete={onComplete} onSkip={onSkip} />);

    expect(screen.getByText('What Do You Enjoy?')).toBeInTheDocument();
    expect(screen.getByTestId('interest-strategy')).toBeInTheDocument();
    expect(screen.getByTestId('interest-party')).toBeInTheDocument();
    expect(screen.getByTestId('interest-cooperative')).toBeInTheDocument();
    expect(screen.getByTestId('interest-family')).toBeInTheDocument();
    expect(screen.getByTestId('interest-thematic')).toBeInTheDocument();
    expect(screen.getByTestId('interest-abstract')).toBeInTheDocument();
    expect(screen.getByTestId('interest-card')).toBeInTheDocument();
    expect(screen.getByTestId('interest-dice')).toBeInTheDocument();
    expect(screen.getByTestId('interest-miniatures')).toBeInTheDocument();
  });

  it('toggles category selection', async () => {
    renderWithQuery(<InterestsStep onComplete={onComplete} onSkip={onSkip} />);

    const strategy = screen.getByTestId('interest-strategy');

    expect(strategy).toHaveAttribute('aria-checked', 'false');

    await user.click(strategy);
    expect(strategy).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByText('1 selected')).toBeInTheDocument();

    await user.click(strategy);
    expect(strategy).toHaveAttribute('aria-checked', 'false');
  });

  it('shows selected count for multiple selections', async () => {
    renderWithQuery(<InterestsStep onComplete={onComplete} onSkip={onSkip} />);

    await user.click(screen.getByTestId('interest-strategy'));
    await user.click(screen.getByTestId('interest-party'));
    await user.click(screen.getByTestId('interest-dice'));

    expect(screen.getByText('3 selected')).toBeInTheDocument();
  });

  it('calls onSkip when skip button clicked', async () => {
    renderWithQuery(<InterestsStep onComplete={onComplete} onSkip={onSkip} />);

    await user.click(screen.getByTestId('interests-skip'));

    expect(onSkip).toHaveBeenCalled();
  });

  it('calls onSkip when submitting with no selections', async () => {
    renderWithQuery(<InterestsStep onComplete={onComplete} onSkip={onSkip} />);

    // The submit button shows "Skip" when nothing is selected
    // Use the submit button specifically (not the "Skip for now" link)
    const submitBtn = screen.getByRole('button', { name: /^skip$/i });
    await user.click(submitBtn);

    expect(onSkip).toHaveBeenCalled();
  });

  it('calls onComplete when submitting with selections', async () => {
    renderWithQuery(<InterestsStep onComplete={onComplete} onSkip={onSkip} />);

    await user.click(screen.getByTestId('interest-strategy'));
    await user.click(screen.getByRole('button', { name: /continue/i }));

    expect(onComplete).toHaveBeenCalled();
  });
});
