import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import GlobalError from '../error';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('GlobalError', () => {
  let consoleSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    consoleSpy.mockRestore();
    vi.unstubAllEnvs();
  });

  it('renders localized title and subtitle', () => {
    const err = new Error('boom') as Error & { digest?: string };
    render(<GlobalError error={err} reset={() => {}} />);

    expect(screen.getByText('500')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /pages\.errors\.serverError\.title/ })
    ).toBeInTheDocument();
    expect(screen.getByText('pages.errors.serverError.subtitle')).toBeInTheDocument();
  });

  it('retry button invokes the reset callback', async () => {
    const user = userEvent.setup();
    const reset = vi.fn();
    const err = new Error('boom') as Error & { digest?: string };
    render(<GlobalError error={err} reset={reset} />);

    await user.click(screen.getByRole('button', { name: 'pages.errors.serverError.retryCta' }));
    expect(reset).toHaveBeenCalledTimes(1);
  });

  it('logs the error to console.error on mount', () => {
    const err = new Error('boom') as Error & { digest?: string };
    render(<GlobalError error={err} reset={() => {}} />);
    expect(consoleSpy).toHaveBeenCalledWith(err);
  });

  it('shows error digest only in non-production environments', () => {
    vi.stubEnv('NODE_ENV', 'development');
    const err = Object.assign(new Error('boom'), { digest: 'abc123' }) as Error & {
      digest?: string;
    };
    const { unmount } = render(<GlobalError error={err} reset={() => {}} />);
    expect(screen.getByText(/abc123/)).toBeInTheDocument();
    unmount();

    vi.stubEnv('NODE_ENV', 'production');
    render(<GlobalError error={err} reset={() => {}} />);
    expect(screen.queryByText(/abc123/)).not.toBeInTheDocument();
  });
});
