import { render, screen } from '@testing-library/react';

import { GameHeader } from '../GameHeader';

describe('GameHeader', () => {
  describe('rating display', () => {
    it('renders RatingStars when rating is provided', () => {
      render(<GameHeader title="Catan" rating={7.8} />);
      // RatingStars renders 5 stars with the numeric value next to them
      expect(screen.getByText('7.8')).toBeInTheDocument();
    });

    it('hides rating row when rating is null', () => {
      render(<GameHeader title="Catan" rating={null} />);
      // No rating number should be displayed
      expect(screen.queryByText(/\d+\.\d+/)).not.toBeInTheDocument();
    });

    it('hides rating row when rating is undefined', () => {
      render(<GameHeader title="Catan" />);
      expect(screen.queryByText(/\d+\.\d+/)).not.toBeInTheDocument();
    });

    it('renders a full 5-star display via RatingStars (not single inline star)', () => {
      // This test drives the TDD red→green cycle for issue #290:
      // Before: inline Badge with 1 <Star /> icon
      // After:  <RatingStars /> component with 5 star icons (10-point BGG → 5-star scale)
      const { container } = render(<GameHeader title="Catan" rating={7.8} />);
      const svgs = container.querySelectorAll('svg');
      expect(svgs.length).toBeGreaterThanOrEqual(5);
    });
  });

  describe('metadata badges', () => {
    it('renders player range badge when both players provided', () => {
      render(<GameHeader title="Catan" minPlayers={3} maxPlayers={6} />);
      expect(screen.getByText(/3–6 giocatori/)).toBeInTheDocument();
    });

    it('renders single-player label when min equals max', () => {
      render(<GameHeader title="Solo" minPlayers={1} maxPlayers={1} />);
      expect(screen.getByText(/1 giocatori/)).toBeInTheDocument();
    });

    it('renders duration badge when playing time provided', () => {
      render(<GameHeader title="Catan" playingTimeMinutes={60} />);
      expect(screen.getByText(/60 min/)).toBeInTheDocument();
    });

    it('omits player badge when players missing', () => {
      render(<GameHeader title="Catan" minPlayers={null} maxPlayers={null} />);
      expect(screen.queryByText(/giocatori/)).not.toBeInTheDocument();
    });
  });

  describe('header content', () => {
    it('renders game title', () => {
      render(<GameHeader title="Brass: Birmingham" />);
      expect(screen.getByRole('heading', { name: 'Brass: Birmingham' })).toBeInTheDocument();
    });

    it('renders publisher with year when both provided', () => {
      render(<GameHeader title="Catan" publisher="Mayfair" year={1995} />);
      expect(screen.getByText(/Mayfair.*1995/)).toBeInTheDocument();
    });

    it('renders only publisher when year missing', () => {
      render(<GameHeader title="Catan" publisher="Mayfair" />);
      expect(screen.getByText('Mayfair')).toBeInTheDocument();
    });
  });
});
