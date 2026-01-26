import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RateLimitConfigClient } from '../client';

// Mock AdminAuthGuard to bypass authentication in tests
vi.mock('@/components/admin/AdminAuthGuard', () => ({
  AdminAuthGuard: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

// Mock useAuthUser hook
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: () => ({
    user: { id: 'admin-1', email: 'admin@test.com', role: 'admin' },
    loading: false,
  }),
}));

// Mock child components
vi.mock('../_components/TierConfigSection/TierConfigSection', () => ({
  TierConfigSection: () => <div data-testid="tier-config-section">Tier Config Section</div>,
}));

vi.mock('../_components/UserOverridesSection/UserOverridesSection', () => ({
  UserOverridesSection: () => <div data-testid="user-overrides-section">User Overrides Section</div>,
}));

describe('RateLimitConfigClient', () => {
  it('renders page title and description', () => {
    render(<RateLimitConfigClient />);

    expect(screen.getByText('Rate Limit Configuration')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Configure share request limits for each user tier and manage individual user overrides',
      ),
    ).toBeInTheDocument();
  });

  it('renders TierConfigSection', () => {
    render(<RateLimitConfigClient />);

    expect(screen.getByTestId('tier-config-section')).toBeInTheDocument();
  });

  it('renders UserOverridesSection', () => {
    render(<RateLimitConfigClient />);

    expect(screen.getByTestId('user-overrides-section')).toBeInTheDocument();
  });

  it('renders both sections in correct order', () => {
    render(<RateLimitConfigClient />);

    const sections = screen.getAllByTestId(/section/);
    expect(sections).toHaveLength(2);
    expect(sections[0]).toHaveAttribute('data-testid', 'tier-config-section');
    expect(sections[1]).toHaveAttribute('data-testid', 'user-overrides-section');
  });
});
