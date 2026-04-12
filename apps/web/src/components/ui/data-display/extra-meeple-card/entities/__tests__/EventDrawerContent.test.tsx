import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EventDrawerContent } from '../EventDrawerContent';
import type { EventDetailData } from '../../types';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock useEventDetail hook
vi.mock('../../hooks', () => ({
  useEventDetail: vi.fn(),
}));

import { useEventDetail } from '../../hooks';

const mockData: EventDetailData = {
  id: 'e1',
  title: 'Game Night Milano',
  isOnline: false,
  isOrganizer: false,
  rsvpStatus: null,
  attendeeCount: 12,
  schedule: [
    { id: 's1', title: 'Torneo Catan', scheduledAt: '2026-05-01T18:00:00Z', gameName: 'Catan' },
  ],
};

describe('EventDrawerContent', () => {
  beforeEach(() => {
    vi.mocked(useEventDetail).mockReturnValue({
      data: mockData,
      loading: false,
      error: null,
      retry: vi.fn(),
    });
  });

  it('renders event title', () => {
    render(<EventDrawerContent entityId="e1" />);
    expect(screen.getByText('Game Night Milano')).toBeInTheDocument();
  });

  it('renders overview and programma tabs', () => {
    render(<EventDrawerContent entityId="e1" />);
    expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /programma/i })).toBeInTheDocument();
  });

  it('shows loading skeleton when loading', () => {
    vi.mocked(useEventDetail).mockReturnValue({
      data: null,
      loading: true,
      error: null,
      retry: vi.fn(),
    });
    render(<EventDrawerContent entityId="e1" />);
    expect(screen.getByTestId('drawer-loading-skeleton')).toBeInTheDocument();
  });

  it('shows error state when error', () => {
    vi.mocked(useEventDetail).mockReturnValue({
      data: null,
      loading: false,
      error: 'Evento non trovato',
      retry: vi.fn(),
    });
    render(<EventDrawerContent entityId="e1" />);
    expect(screen.getByTestId('drawer-error-state')).toBeInTheDocument();
  });

  it('shows Conferma action when rsvp not confirmed', () => {
    render(<EventDrawerContent entityId="e1" />);
    expect(screen.getByText('Conferma')).toBeInTheDocument();
  });

  it('hides Conferma when already confirmed', () => {
    vi.mocked(useEventDetail).mockReturnValue({
      data: { ...mockData, rsvpStatus: 'confirmed' },
      loading: false,
      error: null,
      retry: vi.fn(),
    });
    render(<EventDrawerContent entityId="e1" />);
    expect(screen.queryByText('Conferma')).not.toBeInTheDocument();
  });

  it('shows schedule items in programma tab', async () => {
    const user = userEvent.setup();
    render(<EventDrawerContent entityId="e1" />);
    await user.click(screen.getByRole('tab', { name: /programma/i }));
    expect(screen.getByText('Torneo Catan')).toBeInTheDocument();
  });
});
