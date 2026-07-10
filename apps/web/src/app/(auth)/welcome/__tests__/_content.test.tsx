/**
 * Welcome page (_content.tsx) tests.
 *
 * Verifies:
 * - Auto-redirect fires after REDIRECT_DELAY_MS via setTimeout
 * - Manual "Vai alla Dashboard" button triggers redirect immediately
 * - Open-redirect protection (#2168): ?redirectTo= sanitization
 */

import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// ---------------------------------------------------------------------------
// Mocks (must be registered before component import)
// ---------------------------------------------------------------------------

const pushMock = vi.fn().mockResolvedValue(undefined);

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock('@/lib/logger', () => ({
  logger: { error: vi.fn(), info: vi.fn(), warn: vi.fn() },
}));

// Import AFTER mocks
import { WelcomeContent } from '../_content';
import { logger } from '@/lib/logger';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

// #2773: ?redirectTo= now flows as the `redirectToParam` prop from the async
// page.tsx, not via a mocked useSearchParams.

beforeEach(() => {
  vi.clearAllMocks();
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

// ---------------------------------------------------------------------------
// Helpers for timer-driven redirect
// ---------------------------------------------------------------------------

/**
 * Advance fake timers far enough to trigger the auto-redirect (REDIRECT_DELAY_MS = 2000).
 * Uses advanceTimersByTime instead of runAllTimers to avoid the infinite-loop
 * from the setInterval progress bar that keeps re-registering inside setState.
 */
async function advancePastRedirect() {
  await act(async () => {
    vi.advanceTimersByTime(2100);
  });
}

// ---------------------------------------------------------------------------
// Basic redirect behaviour
// ---------------------------------------------------------------------------

describe('WelcomeContent — auto redirect', () => {
  it('redirects to /library after 2000ms when no ?redirectTo= param', async () => {
    await act(async () => {
      render(<WelcomeContent />);
    });

    await advancePastRedirect();

    expect(pushMock).toHaveBeenCalledWith('/library');
    expect(logger.warn).not.toHaveBeenCalled();
  });

  it('redirects immediately when the "Vai alla Dashboard" button is clicked', async () => {
    await act(async () => {
      render(<WelcomeContent />);
      // Flush the setMounted(true) useEffect so the component renders the button
      vi.advanceTimersByTime(0);
    });

    const btn = screen.getByTestId('welcome-go-dashboard');
    fireEvent.click(btn);

    expect(pushMock).toHaveBeenCalledWith('/library');
    expect(logger.warn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Open redirect protection (#2168)
// ---------------------------------------------------------------------------

describe('WelcomeContent — open redirect protection (#2168)', () => {
  it('falls back to /library when ?redirectTo= is external URL', async () => {
    await act(async () => {
      render(<WelcomeContent redirectToParam="https://evil.com" />);
    });

    await advancePastRedirect();

    expect(pushMock).toHaveBeenCalledWith('/library');
    expect(pushMock).not.toHaveBeenCalledWith('https://evil.com');

    // Unsafe input must trigger a warn log
    expect(logger.warn).toHaveBeenCalledWith(
      'Rejected unsafe ?redirectTo= redirect target on welcome',
      expect.objectContaining({
        metadata: expect.objectContaining({ redirectToMasked: expect.any(String) }),
      })
    );
  });

  it('preserves valid relative ?redirectTo= path', async () => {
    await act(async () => {
      render(<WelcomeContent redirectToParam="/sessions/abc-123" />);
    });

    await advancePastRedirect();

    expect(pushMock).toHaveBeenCalledWith('/sessions/abc-123');

    // Safe input must NOT trigger a warn log
    expect(logger.warn).not.toHaveBeenCalled();
  });

  it('logs unsafe ?redirectTo= attempt on initial render (before redirect)', async () => {
    await act(async () => {
      render(<WelcomeContent redirectToParam="https://evil.com" />);
      // Flush microtasks so the warn useEffect commits, but do NOT advance
      // past the 2000ms redirect timer — push must not have been called yet
    });

    // The warn useEffect fires on mount (rawRedirectTo !== redirectTo)
    expect(logger.warn).toHaveBeenCalledWith(
      'Rejected unsafe ?redirectTo= redirect target on welcome',
      expect.objectContaining({
        metadata: expect.objectContaining({ redirectToMasked: expect.any(String) }),
      })
    );

    // The redirect has NOT fired yet (timer not advanced to 2000ms)
    expect(pushMock).not.toHaveBeenCalled();
  });
});
