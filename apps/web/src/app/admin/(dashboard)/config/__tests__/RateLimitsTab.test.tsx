import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/hooks/useAdminConfig', () => ({
  useAdminConfig: vi.fn().mockReturnValue({ data: undefined, isLoading: false }),
  parseAllConfigs: vi.fn().mockReturnValue({}),
}));

import { RateLimitsTab } from '../RateLimitsTab';

describe('RateLimitsTab', () => {
  it('renders heading', () => {
    render(<RateLimitsTab />);
    expect(screen.getByText('Rate Limits')).toBeInTheDocument();
  });

  it('renders all three rate limit categories', () => {
    render(<RateLimitsTab />);
    expect(screen.getByText('API Rate Limits')).toBeInTheDocument();
    expect(screen.getByText('Chat Rate Limits')).toBeInTheDocument();
    expect(screen.getByText('Upload Rate Limits')).toBeInTheDocument();
  });

  it('renders limit values', () => {
    render(<RateLimitsTab />);
    expect(screen.getByText('1000 req/min')).toBeInTheDocument();
    expect(screen.getByText('10 msg/hour')).toBeInTheDocument();
    expect(screen.getByText('5 uploads/day')).toBeInTheDocument();
  });

  it('renders description text', () => {
    render(<RateLimitsTab />);
    expect(
      screen.getByText(
        'Configure request throttling and usage quotas across API, chat, and upload operations.'
      )
    ).toBeInTheDocument();
  });
});
