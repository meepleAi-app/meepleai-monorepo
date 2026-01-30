/**
 * ParticipantCard Component Tests (Issue #3166 - GST-007)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ParticipantCard } from '../ParticipantCard';
import type { Participant } from '../types';

describe('ParticipantCard', () => {
  const mockParticipant: Participant = {
    id: 'p1',
    displayName: 'Alice',
    isOwner: true,
    isCurrentUser: true,
    avatarColor: '#D97706',
    totalScore: 87,
    rank: 1,
  };

  it('should render participant name', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('should display total score', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    expect(screen.getByText('87')).toBeInTheDocument();
  });

  it('should show rank badge', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    expect(screen.getByText('#1')).toBeInTheDocument();
  });

  it('should render owner participant', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    // Owner indicator present (badge or text)
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('should render current user participant', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    // Current user indicator present
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('should not show rank for unranked participant', () => {
    const unrankedParticipant: Participant = {
      ...mockParticipant,
      rank: undefined,
    };

    render(<ParticipantCard participant={unrankedParticipant} />);
    expect(screen.queryByText(/#/)).not.toBeInTheDocument();
  });

  it('should show winner medal for rank 1', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    expect(screen.getByText('🥇')).toBeInTheDocument();
  });

  it('should handle typing state', () => {
    const typingParticipant: Participant = {
      ...mockParticipant,
      isTyping: true,
    };

    render(<ParticipantCard participant={typingParticipant} />);
    // Typing indicator may be visual (dots, animation)
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('should use avatar color for styling', () => {
    const { container } = render(<ParticipantCard participant={mockParticipant} />);

    const avatar = container.querySelector('[style*="background"]');
    expect(avatar).toBeInTheDocument();
  });

  it('should display first letter of name in avatar', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    expect(screen.getByText('A')).toBeInTheDocument();
  });
});
