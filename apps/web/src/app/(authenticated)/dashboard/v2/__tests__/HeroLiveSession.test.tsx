import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { HeroLiveSession } from '../sections/HeroLiveSession';

describe('HeroLiveSession', () => {
  const baseSession = {
    id: 'ses-1',
    gameName: 'Azul',
    locationName: 'Casa di Marco',
    playerCount: 3,
    roundCurrent: 4,
    roundTotal: 6,
    startedMinutesAgo: 38,
  };

  it('renders the session title and meta when a session is active', () => {
    render(<HeroLiveSession session={baseSession} onContinue={() => {}} />);
    expect(screen.getByText(/Serata Azul/)).toBeInTheDocument();
    expect(screen.getByText(/Casa di Marco/)).toBeInTheDocument();
    expect(screen.getByText(/In corso/i)).toBeInTheDocument();
  });

  it('calls onContinue when the primary action is clicked', async () => {
    const onContinue = vi.fn();
    const user = userEvent.setup();
    render(<HeroLiveSession session={baseSession} onContinue={onContinue} />);
    await user.click(screen.getAllByRole('button', { name: /Continua/i })[0]);
    expect(onContinue).toHaveBeenCalled();
  });

  it('renders the empty state when no active session', () => {
    render(<HeroLiveSession session={null} onContinue={() => {}} />);
    expect(screen.getByText(/Nessuna partita/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Inizia nuova partita/i })).toBeInTheDocument();
  });
});
