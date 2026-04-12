import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToolDrawerContent } from '../ToolDrawerContent';
import type { ToolDetailData } from '../../types';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock useToolDetail hook
vi.mock('../../hooks', () => ({
  useToolDetail: vi.fn(),
}));

import { useToolDetail } from '../../hooks';

const mockData: ToolDetailData = {
  id: 'tool1',
  name: 'Dado D6',
  toolType: 'dice',
  toolkitId: 'tk1',
  toolkitName: 'Catan Toolkit',
  isOwner: true,
  hasActiveSession: false,
  config: { quantity: 2, diceType: 'd6' },
  previewDescription: 'Tira 2 dadi a 6 facce',
};

describe('ToolDrawerContent', () => {
  beforeEach(() => {
    vi.mocked(useToolDetail).mockReturnValue({
      data: mockData,
      loading: false,
      error: null,
      retry: vi.fn(),
    });
  });

  it('renders tool name', () => {
    render(<ToolDrawerContent entityId="tool1" />);
    expect(screen.getByText('Dado D6')).toBeInTheDocument();
  });

  it('renders dettaglio and preview tabs', () => {
    render(<ToolDrawerContent entityId="tool1" />);
    expect(screen.getByRole('tab', { name: /dettaglio/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /preview/i })).toBeInTheDocument();
  });

  it('shows loading skeleton when loading', () => {
    vi.mocked(useToolDetail).mockReturnValue({
      data: null,
      loading: true,
      error: null,
      retry: vi.fn(),
    });
    render(<ToolDrawerContent entityId="tool1" />);
    expect(screen.getByTestId('drawer-loading-skeleton')).toBeInTheDocument();
  });

  it('shows error state when error', () => {
    vi.mocked(useToolDetail).mockReturnValue({
      data: null,
      loading: false,
      error: 'Strumento non trovato',
      retry: vi.fn(),
    });
    render(<ToolDrawerContent entityId="tool1" />);
    expect(screen.getByTestId('drawer-error-state')).toBeInTheDocument();
  });

  it('shows Usa when has active session', () => {
    vi.mocked(useToolDetail).mockReturnValue({
      data: { ...mockData, hasActiveSession: true },
      loading: false,
      error: null,
      retry: vi.fn(),
    });
    render(<ToolDrawerContent entityId="tool1" />);
    expect(screen.getByText('Usa')).toBeInTheDocument();
  });

  it('hides Usa when no active session', () => {
    render(<ToolDrawerContent entityId="tool1" />);
    expect(screen.queryByText('Usa')).not.toBeInTheDocument();
  });

  it('shows preview description in preview tab', async () => {
    const user = userEvent.setup();
    render(<ToolDrawerContent entityId="tool1" />);
    await user.click(screen.getByRole('tab', { name: /preview/i }));
    expect(screen.getByText('Tira 2 dadi a 6 facce')).toBeInTheDocument();
  });
});
