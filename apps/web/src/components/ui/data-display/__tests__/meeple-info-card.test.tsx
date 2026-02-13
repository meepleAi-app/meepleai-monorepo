/**
 * MeepleInfoCard Component Tests
 * Epic #3820 - MeepleCard System
 *
 * Tests tab rendering, Knowledge Base, Social Links, Stats,
 * readOnly mode, and empty states.
 */

import React from 'react';
import { screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { MeepleInfoCard } from '../meeple-info-card';

// Mock PdfUploadModal
vi.mock('@/components/library/PdfUploadModal', () => ({
  PdfUploadModal: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="pdf-upload-modal">PDF Modal</div> : null,
}));

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: React.PropsWithChildren<{ href: string }>) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('MeepleInfoCard', () => {
  const defaultProps = {
    gameId: 'game-123',
    gameTitle: 'Agricola',
  };

  describe('Rendering', () => {
    it('should render with default test ID', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);
      expect(screen.getByTestId('meeple-info-card')).toBeInTheDocument();
    });

    it('should render with custom test ID', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} data-testid="custom-info" />);
      expect(screen.getByTestId('custom-info')).toBeInTheDocument();
    });

    it('should render KB and Social tabs by default', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);
      expect(screen.getByTestId('meeple-info-tab-kb')).toBeInTheDocument();
      expect(screen.getByTestId('meeple-info-tab-social')).toBeInTheDocument();
    });

    it('should not render stats tab by default', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);
      expect(screen.queryByTestId('meeple-info-tab-stats')).not.toBeInTheDocument();
    });
  });

  describe('Knowledge Base Tab', () => {
    it('should show empty state for KB tab', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);
      expect(screen.getByText('Nessun documento')).toBeInTheDocument();
    });

    it('should show upload button when not readOnly', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);
      expect(screen.getByText('Carica')).toBeInTheDocument();
    });

    it('should hide upload button when readOnly', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} readOnly />);
      expect(screen.queryByText('Carica')).not.toBeInTheDocument();
    });

    it('should show readOnly empty message', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} readOnly />);
      expect(screen.getByText(/Nessun documento disponibile/)).toBeInTheDocument();
    });

    it('should show editable empty message when not readOnly', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);
      expect(screen.getByText(/Carica regolamenti/)).toBeInTheDocument();
    });

    it('should open PDF modal on upload click', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);

      // Click the "Carica PDF" button in empty state
      fireEvent.click(screen.getByText('Carica PDF'));
      expect(screen.getByTestId('pdf-upload-modal')).toBeInTheDocument();
    });
  });

  describe('Social Links Tab', () => {
    it('should switch to social tab on click', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);

      fireEvent.click(screen.getByTestId('meeple-info-tab-social'));
      // After switching, both tab label and h3 heading say "Link Utili"
      expect(screen.getAllByText('Link Utili').length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('Nessun link')).toBeInTheDocument();
    });

    it('should show empty state when no bggId', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} />);

      fireEvent.click(screen.getByTestId('meeple-info-tab-social'));
      expect(screen.getByText('Nessun link')).toBeInTheDocument();
    });

    it('should show BGG link when bggId is provided', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} bggId={31260} />);

      fireEvent.click(screen.getByTestId('meeple-info-tab-social'));
      expect(screen.getByText('BoardGameGeek')).toBeInTheDocument();
    });

    it('should link to correct BGG URL', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} bggId={31260} />);

      fireEvent.click(screen.getByTestId('meeple-info-tab-social'));
      const link = screen.getByText('BoardGameGeek').closest('a');
      expect(link).toHaveAttribute('href', 'https://boardgamegeek.com/boardgame/31260');
    });

    it('should hide add button when readOnly', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} readOnly />);

      fireEvent.click(screen.getByTestId('meeple-info-tab-social'));
      expect(screen.queryByText('Aggiungi')).not.toBeInTheDocument();
    });
  });

  describe('Stats Tab', () => {
    const statsData = {
      timesPlayed: 15,
      lastPlayed: '2026-01-15T00:00:00Z',
      winRate: '40%',
      avgDuration: '90 min',
    };

    const recentSessions = [
      { id: 'sess-1', playedAt: '2026-01-15T00:00:00Z', durationFormatted: '1h 30m', didWin: true },
      { id: 'sess-2', playedAt: '2026-01-10T00:00:00Z', durationFormatted: '1h 45m', didWin: false },
    ];

    it('should show stats tab when showStats is true with data', () => {
      render(
        <MeepleInfoCard
          {...defaultProps}
          showStats
          statsData={statsData}
        />,
      );
      expect(screen.getByTestId('meeple-info-tab-stats')).toBeInTheDocument();
    });

    it('should display stats data', () => {
      render(
        <MeepleInfoCard
          {...defaultProps}
          showStats
          statsData={statsData}
        />,
      );

      fireEvent.click(screen.getByTestId('meeple-info-tab-stats'));
      expect(screen.getByText('15')).toBeInTheDocument();
      expect(screen.getByText('40%')).toBeInTheDocument();
      expect(screen.getByText('90 min')).toBeInTheDocument();
    });

    it('should display recent sessions', () => {
      render(
        <MeepleInfoCard
          {...defaultProps}
          showStats
          statsData={statsData}
          recentSessions={recentSessions}
        />,
      );

      fireEvent.click(screen.getByTestId('meeple-info-tab-stats'));
      expect(screen.getByText('Sessioni Recenti')).toBeInTheDocument();
      expect(screen.getByText('W')).toBeInTheDocument();
      expect(screen.getByText('L')).toBeInTheDocument();
    });

    it('should show N/A for missing stats', () => {
      render(
        <MeepleInfoCard
          {...defaultProps}
          showStats
          statsData={{
            timesPlayed: 0,
            lastPlayed: null,
            winRate: null,
            avgDuration: null,
          }}
        />,
      );

      fireEvent.click(screen.getByTestId('meeple-info-tab-stats'));
      expect(screen.getByText('0')).toBeInTheDocument();
      expect(screen.getByText('Mai')).toBeInTheDocument();
      expect(screen.getAllByText('N/A')).toHaveLength(2);
    });
  });

  describe('Tab Visibility', () => {
    it('should hide KB tab when showKnowledgeBase is false', () => {
      render(
        <MeepleInfoCard
          {...defaultProps}
          showKnowledgeBase={false}
        />,
      );
      expect(screen.queryByTestId('meeple-info-tab-kb')).not.toBeInTheDocument();
    });

    it('should hide Social tab when showSocialLinks is false', () => {
      render(
        <MeepleInfoCard
          {...defaultProps}
          showSocialLinks={false}
        />,
      );
      expect(screen.queryByTestId('meeple-info-tab-social')).not.toBeInTheDocument();
    });
  });

  describe('ReadOnly Mode', () => {
    it('should not render PDF upload modal in readOnly mode', () => {
      renderWithQuery(<MeepleInfoCard {...defaultProps} readOnly />);

      // Try to find upload buttons - they should not exist
      expect(screen.queryByText('Carica')).not.toBeInTheDocument();
      expect(screen.queryByText('Carica PDF')).not.toBeInTheDocument();
    });
  });
});
