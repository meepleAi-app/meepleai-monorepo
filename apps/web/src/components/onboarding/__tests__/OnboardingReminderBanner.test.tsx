import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { OnboardingReminderBanner } from '../OnboardingReminderBanner';

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    clear: () => {
      store = {};
    },
  };
})();

Object.defineProperty(window, 'localStorage', { value: localStorageMock });

describe('OnboardingReminderBanner', () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  it('renders after mount when not dismissed', async () => {
    render(<OnboardingReminderBanner />);
    // useEffect runs after mount
    await act(() => Promise.resolve());
    expect(screen.getByTestId('onboarding-reminder-banner')).toBeInTheDocument();
    expect(screen.getByText(/complete your profile setup/i)).toBeInTheDocument();
  });

  it('does not render when previously dismissed via localStorage', async () => {
    localStorageMock.getItem.mockReturnValueOnce('true');
    render(<OnboardingReminderBanner />);
    await act(() => Promise.resolve());
    expect(screen.queryByTestId('onboarding-reminder-banner')).not.toBeInTheDocument();
  });

  it('dismisses on click and persists to localStorage', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();
    render(<OnboardingReminderBanner onDismiss={onDismiss} />);
    await act(() => Promise.resolve());

    await user.click(screen.getByTestId('dismiss-onboarding-banner'));

    expect(screen.queryByTestId('onboarding-reminder-banner')).not.toBeInTheDocument();
    expect(localStorageMock.setItem).toHaveBeenCalledWith('onboarding_banner_dismissed', 'true');
    expect(onDismiss).toHaveBeenCalled();
  });

  it('has a link to the onboarding wizard', async () => {
    render(<OnboardingReminderBanner />);
    await act(() => Promise.resolve());
    const link = screen.getByText(/finish the onboarding wizard/i);
    expect(link).toHaveAttribute('href', '/accept-invite');
  });

  it('has proper accessibility attributes', async () => {
    render(<OnboardingReminderBanner />);
    await act(() => Promise.resolve());
    const banner = screen.getByTestId('onboarding-reminder-banner');
    expect(banner).toHaveAttribute('role', 'status');
    expect(banner).toHaveAttribute('aria-label', 'Onboarding reminder');
  });
});
