import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToolkitDrawerContent } from '../ToolkitDrawerContent';
import type { ToolkitDetailData } from '../../types';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock useToolkitDetail hook
vi.mock('../../hooks', () => ({
  useToolkitDetail: vi.fn(),
}));

import { useToolkitDetail } from '../../hooks';

const mockData: ToolkitDetailData = {
  id: 't1',
  name: 'Catan Toolkit',
  version: 2,
  isPublished: true,
  isOwner: true,
  diceTools: [{ name: 'Dado 6', diceType: 'd6', quantity: 2 }],
  cardTools: [],
  timerTools: [],
  counterTools: [],
  history: [{ version: 1, updatedAt: '2026-01-01T00:00:00Z', note: 'Prima versione' }],
};

describe('ToolkitDrawerContent', () => {
  beforeEach(() => {
    vi.mocked(useToolkitDetail).mockReturnValue({
      data: mockData,
      loading: false,
      error: null,
      retry: vi.fn(),
    });
  });

  it('renders toolkit name', () => {
    render(<ToolkitDrawerContent entityId="t1" />);
    expect(screen.getByText('Catan Toolkit')).toBeInTheDocument();
  });

  it('renders overview, template, storico tabs', () => {
    render(<ToolkitDrawerContent entityId="t1" />);
    expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /template/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /storico/i })).toBeInTheDocument();
  });

  it('shows loading skeleton when loading', () => {
    vi.mocked(useToolkitDetail).mockReturnValue({
      data: null,
      loading: true,
      error: null,
      retry: vi.fn(),
    });
    render(<ToolkitDrawerContent entityId="t1" />);
    expect(screen.getByTestId('drawer-loading-skeleton')).toBeInTheDocument();
  });

  it('shows error state when error', () => {
    vi.mocked(useToolkitDetail).mockReturnValue({
      data: null,
      loading: false,
      error: 'Toolkit non trovato',
      retry: vi.fn(),
    });
    render(<ToolkitDrawerContent entityId="t1" />);
    expect(screen.getByTestId('drawer-error-state')).toBeInTheDocument();
  });

  it('shows Usa in sessione when published', () => {
    render(<ToolkitDrawerContent entityId="t1" />);
    expect(screen.getByText('Usa in sessione')).toBeInTheDocument();
  });

  it('hides Usa in sessione when not published', () => {
    vi.mocked(useToolkitDetail).mockReturnValue({
      data: { ...mockData, isPublished: false },
      loading: false,
      error: null,
      retry: vi.fn(),
    });
    render(<ToolkitDrawerContent entityId="t1" />);
    expect(screen.queryByText('Usa in sessione')).not.toBeInTheDocument();
  });

  it('shows storico items', async () => {
    const user = userEvent.setup();
    render(<ToolkitDrawerContent entityId="t1" />);
    await user.click(screen.getByRole('tab', { name: /storico/i }));
    expect(screen.getByText('Prima versione')).toBeInTheDocument();
  });
});
