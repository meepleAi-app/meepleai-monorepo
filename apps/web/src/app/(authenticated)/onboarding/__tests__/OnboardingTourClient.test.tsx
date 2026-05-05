import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { OnboardingTourClient } from '../OnboardingTourClient';

const routerPush = vi.fn();
const routerReplace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
}));

const completeOnboarding = vi.fn();
vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    auth: {
      completeOnboarding: (...args: unknown[]) => completeOnboarding(...args),
    },
  }),
}));

beforeEach(() => {
  routerPush.mockReset();
  routerReplace.mockReset();
  completeOnboarding.mockReset();
  window.localStorage.clear();
});

afterEach(() => {
  window.localStorage.clear();
});

describe('OnboardingTourClient', () => {
  it('starts on Welcome step and moves to Games on start', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName="Luca" />);
    expect(screen.getByRole('heading', { level: 1, name: /benvenuto/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    expect(screen.getByRole('heading', { level: 2, name: /ludoteca/i })).toBeInTheDocument();
  });

  it('Avanti is disabled until 3 games selected', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName={null} />);
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));

    const advance = screen.getByRole('button', { name: /avanti/i });
    expect(advance).toBeDisabled();

    await user.click(screen.getByRole('button', { name: /catan/i }));
    await user.click(screen.getByRole('button', { name: /azul/i }));
    expect(advance).toBeDisabled();

    await user.click(screen.getByRole('button', { name: /wingspan/i }));
    expect(advance).toBeEnabled();
  });

  it('progresses Welcome→Games→Agents→FirstSession→Complete', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockResolvedValue({ ok: true });
    render(<OnboardingTourClient userName={null} />);

    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    await user.click(screen.getByRole('button', { name: /catan/i }));
    await user.click(screen.getByRole('button', { name: /azul/i }));
    await user.click(screen.getByRole('button', { name: /wingspan/i }));
    await user.click(screen.getByRole('button', { name: /avanti/i }));

    expect(screen.getByRole('heading', { level: 2, name: /assistenti/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /avanti/i }));

    expect(screen.getByRole('heading', { level: 2, name: /cosa vuoi fare/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^salta$/i }));

    expect(screen.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeInTheDocument();
  });

  it('skip from Welcome goes directly to Complete', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName={null} />);
    await user.click(screen.getByRole('button', { name: /salta, esploro da solo/i }));
    expect(screen.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeInTheDocument();
  });

  it('FirstSession action completes onboarding and routes to href', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockResolvedValue({ ok: true });
    render(<OnboardingTourClient userName={null} initialStep={3} />);

    await user.click(screen.getByRole('button', { name: /esplora la library/i }));

    await waitFor(() => expect(completeOnboarding).toHaveBeenCalledWith(false));
    expect(routerPush).toHaveBeenCalledWith('/library');
  });

  it('Complete "Vai alla home" completes onboarding and routes to /library', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockResolvedValue({ ok: true });
    render(<OnboardingTourClient userName={null} initialStep={4} />);

    await user.click(screen.getByRole('button', { name: /vai alla home/i }));
    await waitFor(() => expect(completeOnboarding).toHaveBeenCalledWith(false));
    expect(routerPush).toHaveBeenCalledWith('/library');
  });

  it('shows error and keeps user on Complete when completeOnboarding fails', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockRejectedValue(new Error('Rete non disponibile'));
    render(<OnboardingTourClient userName={null} initialStep={4} />);

    await user.click(screen.getByRole('button', { name: /vai alla home/i }));
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/rete non disponibile/i);
    });
    expect(routerPush).not.toHaveBeenCalled();
  });

  it('persists step to localStorage', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName={null} />);
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    await waitFor(() => expect(window.localStorage.getItem('mai-onboarding-step')).toBe('1'));
  });

  it('resumes from localStorage on mount', () => {
    window.localStorage.setItem('mai-onboarding-step', '2');
    render(<OnboardingTourClient userName={null} />);
    expect(screen.getByRole('heading', { level: 2, name: /assistenti/i })).toBeInTheDocument();
  });

  it('ignores invalid localStorage values', () => {
    window.localStorage.setItem('mai-onboarding-step', '99');
    render(<OnboardingTourClient userName={null} />);
    expect(screen.getByRole('heading', { level: 1, name: /benvenuto in/i })).toBeInTheDocument();
  });
});
