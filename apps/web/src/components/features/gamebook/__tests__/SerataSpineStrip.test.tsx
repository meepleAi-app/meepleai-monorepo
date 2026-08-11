import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { GamebookCampaignSpine } from '@/lib/api/gamebook-campaigns';

import { SerataSpineStrip } from '../SerataSpineStrip';

const base: GamebookCampaignSpine = {
  gameNightId: '44444444-4444-4444-a444-444444444444',
  gameNightTitle: 'Serata da Marco',
  organizerId: '55555555-5555-4555-a555-555555555555',
  gameNightStatus: 'InProgress',
  totalSessions: 3,
  completedSessions: 1,
  hasLiveSession: false,
  campaignStatus: 'Resumable',
};

describe('SerataSpineStrip', () => {
  it('renders the owning GameNight title', () => {
    render(<SerataSpineStrip spine={base} />);
    expect(screen.getByTestId('serata-spine-strip')).toBeInTheDocument();
    expect(screen.getByText('Serata da Marco')).toBeInTheDocument();
  });

  it('shows the live badge when a live session exists', () => {
    render(<SerataSpineStrip spine={{ ...base, hasLiveSession: true }} />);
    expect(screen.getByTestId('serata-live-badge')).toBeInTheDocument();
  });

  it('shows the status label (not the live badge) when not live', () => {
    render(
      <SerataSpineStrip spine={{ ...base, hasLiveSession: false, gameNightStatus: 'Published' }} />
    );
    expect(screen.queryByTestId('serata-live-badge')).not.toBeInTheDocument();
    expect(screen.getByText('programmata')).toBeInTheDocument();
  });

  it('renders "completata" and NO live badge for a Completed night (SI-3 — pure BE-state render, no FE backward flip)', () => {
    render(
      <SerataSpineStrip spine={{ ...base, gameNightStatus: 'Completed', hasLiveSession: false }} />
    );
    expect(screen.queryByTestId('serata-live-badge')).not.toBeInTheDocument();
    expect(screen.getByText('completata')).toBeInTheDocument();
  });

  it('renders the session pip with completed/total', () => {
    render(<SerataSpineStrip spine={{ ...base, totalSessions: 3, completedSessions: 2 }} />);
    expect(screen.getByText('2/3')).toBeInTheDocument();
    expect(screen.getByTestId('serata-session-pip')).toHaveAttribute(
      'aria-label',
      '2 di 3 sessioni della serata'
    );
  });

  it('clamps completed to total (defensive)', () => {
    render(<SerataSpineStrip spine={{ ...base, totalSessions: 1, completedSessions: 5 }} />);
    expect(screen.getByText('1/1')).toBeInTheDocument();
  });

  it('caps rendered pip dots at MAX_PIPS but shows the true total (defensive)', () => {
    render(<SerataSpineStrip spine={{ ...base, totalSessions: 100, completedSessions: 50 }} />);
    expect(screen.getByText('50/100')).toBeInTheDocument();
    const dots = screen.getByTestId('serata-session-pip').querySelectorAll('span[aria-hidden]');
    expect(dots.length).toBe(12);
  });
});
