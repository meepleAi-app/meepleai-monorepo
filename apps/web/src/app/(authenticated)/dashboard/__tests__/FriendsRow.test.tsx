import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { FriendsRow } from '../sections/FriendsRow';

describe('FriendsRow', () => {
  const friends = [
    { id: 'f1', name: 'Luca Marconi', status: 'Sta giocando Azul', presence: 'online' as const },
    { id: 'f2', name: 'Sara Vitali', status: 'Online · 12 giochi', presence: 'online' as const },
    { id: 'f3', name: 'Giulia Bianchi', status: 'Inattiva da 2 ore', presence: 'idle' as const },
    { id: 'f4', name: 'Alessandro Rossi', status: 'Offline · ieri', presence: 'offline' as const },
  ];

  it('renders the section header', () => {
    render(<FriendsRow friends={friends} />);
    expect(screen.getByText(/Amici attivi/i)).toBeInTheDocument();
  });

  it('renders all 4 friends', () => {
    render(<FriendsRow friends={friends} />);
    expect(screen.getByText('Luca Marconi')).toBeInTheDocument();
    expect(screen.getByText('Sara Vitali')).toBeInTheDocument();
    expect(screen.getByText('Giulia Bianchi')).toBeInTheDocument();
    expect(screen.getByText('Alessandro Rossi')).toBeInTheDocument();
  });

  it('renders initials from the name', () => {
    render(<FriendsRow friends={[friends[0]]} />);
    expect(screen.getByText('LM')).toBeInTheDocument();
  });

  it('renders nothing when empty', () => {
    const { container } = render(<FriendsRow friends={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
