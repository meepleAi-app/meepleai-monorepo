/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MeepleParticipantCard } from '../MeepleParticipantCard';

describe('MeepleParticipantCard', () => {
  const mockParticipant = {
    id: 'p1',
    displayName: 'Marco',
    isOwner: false,
    isCurrentUser: false,
    avatarColor: '#7c3aed',
    totalScore: 42,
    rank: 1,
  };

  it('renders with correct entity type', () => {
    render(<MeepleParticipantCard participant={mockParticipant} />);
    const card = screen.getByTestId('participant-card-p1');
    expect(card).toHaveAttribute('data-entity', 'player');
  });

  it('displays participant name as title', () => {
    render(<MeepleParticipantCard participant={mockParticipant} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });
});
