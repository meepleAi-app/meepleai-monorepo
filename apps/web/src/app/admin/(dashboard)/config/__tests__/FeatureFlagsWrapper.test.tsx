import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetConfigurations = vi.hoisted(() =>
  vi.fn().mockResolvedValue({ items: [{ id: '1', key: 'test' }], total: 1, page: 1, pageSize: 100 })
);

vi.mock('@/lib/api', () => ({
  api: {
    config: {
      getConfigurations: mockGetConfigurations,
    },
  },
}));

vi.mock('@/components/admin/FeatureFlagsTab', () => ({
  default: ({ configurations }: { configurations: unknown[] }) => (
    <div data-testid="feature-flags-tab">items: {configurations.length}</div>
  ),
}));

import { FeatureFlagsWrapper } from '../FeatureFlagsWrapper';

describe('FeatureFlagsWrapper', () => {
  it('shows loading skeleton initially', () => {
    mockGetConfigurations.mockReturnValue(new Promise(() => {}));
    render(<FeatureFlagsWrapper />);
    expect(screen.queryByTestId('feature-flags-tab')).not.toBeInTheDocument();
  });

  it('renders FeatureFlagsTab after loading', async () => {
    mockGetConfigurations.mockResolvedValue({
      items: [{ id: '1', key: 'flag-1' }],
      total: 1,
      page: 1,
      pageSize: 100,
    });
    render(<FeatureFlagsWrapper />);
    await waitFor(() => {
      expect(screen.getByTestId('feature-flags-tab')).toBeInTheDocument();
    });
    expect(screen.getByText('items: 1')).toBeInTheDocument();
  });

  it('handles fetch error gracefully', async () => {
    mockGetConfigurations.mockRejectedValue(new Error('Network error'));
    render(<FeatureFlagsWrapper />);
    await waitFor(() => {
      expect(screen.getByTestId('feature-flags-tab')).toBeInTheDocument();
    });
    expect(screen.getByText('items: 0')).toBeInTheDocument();
  });
});
