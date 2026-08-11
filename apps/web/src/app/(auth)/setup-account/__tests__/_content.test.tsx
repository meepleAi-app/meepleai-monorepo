/**
 * Setup-account page (_content.tsx) smoke tests — #2773 props migration.
 *
 * Covers the refactored contract: the invitation `token` now flows as a PROP
 * from the async page.tsx instead of via `useSearchParams`. Focused smoke tests
 * for the prop wiring (missing-token branch + validation call), not full
 * behavioural coverage of the activation flow.
 */

import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// ---------------------------------------------------------------------------
// Mocks (must be registered before component import)
// ---------------------------------------------------------------------------

const pushMock = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock }),
}));

// Passthrough AuthLayout so the smoke test does not depend on layout internals.
vi.mock('@/components/layouts', () => ({
  AuthLayout: ({ children }: { children: React.ReactNode }) => children,
}));

// Import AFTER mocks
import { SetupAccountContent } from '../_content';

const originalFetch = global.fetch;

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  global.fetch = originalFetch;
});

describe('SetupAccountContent — #2773 props contract', () => {
  it('renders the missing-token branch when the token prop is absent (no validation call)', () => {
    const fetchMock = vi.fn();
    global.fetch = fetchMock as unknown as typeof fetch;

    render(<SetupAccountContent token={null} />);

    expect(screen.getByText(/Utilizza il link ricevuto/)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('validates the invitation token supplied via the prop on mount', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ isValid: false, errorReason: 'invalid' }),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    render(<SetupAccountContent token="inv-tok" />);

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalled();
    });

    const [url, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(String(url)).toContain('/api/v1/auth/validate-invitation');
    expect(options.method).toBe('POST');
    expect(String(options.body)).toContain('inv-tok');
  });
});
