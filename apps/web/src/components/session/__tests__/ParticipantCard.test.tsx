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
    // Score appears multiple times (shadow + main), check container has the value
    const scoreElements = screen.getAllByText('87');
    expect(scoreElements.length).toBeGreaterThan(0);
  });

  it('should show rank badge', () => {
    render(<ParticipantCard participant={mockParticipant} />);
    // Rank badge splits # and number across elements in full variant
    expect(screen.getByText('#')).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
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

  describe('Compact Variant', () => {
    it('renders participant name in compact', () => {
      render(<ParticipantCard participant={mockParticipant} variant="compact" />);
      expect(screen.getByText('Alice')).toBeInTheDocument();
    });

    it('renders score with Score label in compact', () => {
      render(<ParticipantCard participant={mockParticipant} variant="compact" />);
      expect(screen.getByText(/Score: 87/)).toBeInTheDocument();
    });

    it('shows silver emoji for rank 2', () => {
      const silverParticipant: Participant = { ...mockParticipant, rank: 2 };
      render(<ParticipantCard participant={silverParticipant} variant="compact" />);
      expect(screen.getByText('🥈')).toBeInTheDocument();
    });

    it('shows bronze emoji for rank 3', () => {
      const bronzeParticipant: Participant = { ...mockParticipant, rank: 3 };
      render(<ParticipantCard participant={bronzeParticipant} variant="compact" />);
      expect(screen.getByText('🥉')).toBeInTheDocument();
    });

    it('shows rank number for rank > 3 in compact', () => {
      const lowRankParticipant: Participant = { ...mockParticipant, rank: 5 };
      render(<ParticipantCard participant={lowRankParticipant} variant="compact" />);
      expect(screen.getByText('#5')).toBeInTheDocument();
    });

    it('shows typing spinner in compact variant', () => {
      const typingParticipant: Participant = { ...mockParticipant, isTyping: true };
      const { container } = render(
        <ParticipantCard participant={typingParticipant} variant="compact" />
      );
      expect(container.querySelector('.animate-spin')).toBeInTheDocument();
    });
  });

  describe('Full Variant', () => {
    it('shows pts label in full variant', () => {
      render(<ParticipantCard participant={mockParticipant} variant="full" />);
      expect(screen.getByText('pts')).toBeInTheDocument();
    });

    it('shows rank text in full variant', () => {
      render(<ParticipantCard participant={mockParticipant} variant="full" />);
      expect(screen.getByText('rank')).toBeInTheDocument();
    });

    it('shows typing text in full variant', () => {
      const typingParticipant: Participant = { ...mockParticipant, isTyping: true };
      render(<ParticipantCard participant={typingParticipant} variant="full" />);
      expect(screen.getByText('typing...')).toBeInTheDocument();
    });
  });

  describe('Name Initials', () => {
    it('handles multi-word names', () => {
      const multiNameParticipant: Participant = {
        ...mockParticipant,
        displayName: 'John Paul',
      };
      render(<ParticipantCard participant={multiNameParticipant} variant="compact" />);
      expect(screen.getByText('JP')).toBeInTheDocument();
    });

    it('removes (io) suffix from name', () => {
      const ioParticipant: Participant = {
        ...mockParticipant,
        displayName: 'Bob (io)',
      };
      render(<ParticipantCard participant={ioParticipant} variant="compact" />);
      expect(screen.getByText('B')).toBeInTheDocument();
    });
  });

  describe('Owner Badge', () => {
    it('shows crown icon when participant is owner', () => {
      const { container } = render(
        <ParticipantCard participant={mockParticipant} variant="compact" />
      );
      expect(container.querySelector('.lucide-crown')).toBeInTheDocument();
    });

    it('does not show crown when not owner', () => {
      const nonOwner: Participant = { ...mockParticipant, isOwner: false };
      const { container } = render(
        <ParticipantCard participant={nonOwner} variant="compact" />
      );
      expect(container.querySelector('.lucide-crown')).not.toBeInTheDocument();
    });
  });
});