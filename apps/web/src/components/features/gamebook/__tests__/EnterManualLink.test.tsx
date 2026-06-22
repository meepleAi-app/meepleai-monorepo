import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';
import * as analytics from '@/lib/analytics/track-event';
import { EnterManualLink } from '../EnterManualLink';

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  useSearchParams: vi.fn(() => new URLSearchParams()),
  usePathname: vi.fn(() => '/library/g/play/c/translate'),
}));

describe('EnterManualLink', () => {
  it('renders "kebab" variant as menu item button', () => {
    vi.mocked(useRouter).mockReturnValue({ push: vi.fn() } as never);
    render(<EnterManualLink entryPoint="kebab" campaignId="c1" />);
    expect(screen.getByRole('menuitem', { name: /digita manualmente/i })).toBeInTheDocument();
  });

  it('renders "empty_state" variant as card with icon + title + subtitle', () => {
    vi.mocked(useRouter).mockReturnValue({ push: vi.fn() } as never);
    render(<EnterManualLink entryPoint="empty_state" campaignId="c1" />);
    expect(screen.getByText(/libro non a portata/i)).toBeInTheDocument();
    expect(screen.getByText(/digita il paragrafo/i)).toBeInTheDocument();
  });

  it('renders "error_cta" variant as inline link', () => {
    vi.mocked(useRouter).mockReturnValue({ push: vi.fn() } as never);
    render(<EnterManualLink entryPoint="error_cta" campaignId="c1" />);
    expect(screen.getByRole('button', { name: /digita manualmente/i })).toBeInTheDocument();
  });

  it('click navigates to ?mode=manual + fires analytics', () => {
    const push = vi.fn();
    vi.mocked(useRouter).mockReturnValue({ push } as never);
    vi.mocked(usePathname).mockReturnValue('/library/g/play/c/translate');
    const trackSpy = vi.spyOn(analytics, 'trackEvent');

    render(<EnterManualLink entryPoint="kebab" campaignId="c1" />);
    fireEvent.click(screen.getByRole('menuitem'));

    expect(push).toHaveBeenCalledWith(expect.stringContaining('mode=manual'));
    expect(trackSpy).toHaveBeenCalledWith(
      'translate.manual_entry_click',
      expect.objectContaining({
        entryPoint: 'kebab',
        campaignId: 'c1',
      })
    );
  });
});
