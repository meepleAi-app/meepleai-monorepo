import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeepleCardGame, type MeepleCardGameLabels } from './meeple-card-game';

// Stub next/link (GridCard's root Link) to a plain <a> in jsdom.
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    prefetch: _prefetch,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
    prefetch?: boolean;
  } & React.AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const labels: MeepleCardGameLabels = {
  ratingAriaLabel: 'Voto',
  toolkitLabel: 'tk',
  agentLabel: 'ag',
  newWeekAriaLabel: count => `${count} nuovi questa settimana`,
};

const baseProps = {
  id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
  title: 'Catan',
  year: 1995,
  rating: 4,
  toolkitsCount: 3,
  agentsCount: 2,
  kbsCount: 1,
  newThisWeekCount: 0,
  labels,
};

describe('MeepleCardGame (adapter over MeepleCard, #2858)', () => {
  it('renders an anchor linking to /shared-games/{id}', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByRole('link')).toHaveAttribute('href', `/shared-games/${baseProps.id}`);
  });

  it('carries data-testid=shared-games-card on the card root', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByTestId('shared-games-card')).toBeInTheDocument();
  });

  it('emits no interactive element nested inside the card anchor (WCAG 4.1.2)', () => {
    render(<MeepleCardGame {...baseProps} />);
    const link = screen.getByRole('link');
    // Count chips render as static <span role="img"> and no MenuPlaceholder button is
    // rendered inside an anchor-rooted card, so the <a> has zero nested buttons/anchors.
    expect(link.querySelectorAll('button, a')).toHaveLength(0);
  });

  it('renders the title as a heading', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByRole('heading', { name: 'Catan' })).toBeInTheDocument();
  });

  it('renders the year as the subtitle when provided', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByText('1995')).toBeInTheDocument();
  });

  it('omits the year when null', () => {
    render(<MeepleCardGame {...baseProps} year={null} />);
    expect(screen.queryByText('1995')).not.toBeInTheDocument();
  });

  it('renders the canonical rating readout (value.toFixed(1)) from rating + ratingMax=5', () => {
    render(<MeepleCardGame {...baseProps} rating={4} />);
    expect(screen.getByText('4.0')).toBeInTheDocument();
  });

  it('renders the connection strip when any entity count > 0', () => {
    const { container } = render(<MeepleCardGame {...baseProps} />);
    expect(container.querySelector('[data-testid="connection-chip-strip"]')).not.toBeNull();
  });

  it('omits the connection strip when all entity counts are 0', () => {
    const { container } = render(
      <MeepleCardGame {...baseProps} toolkitsCount={0} agentsCount={0} kbsCount={0} />
    );
    expect(container.querySelector('[data-testid="connection-chip-strip"]')).toBeNull();
  });

  it('renders the new-this-week badge (+N) when count >= 2', () => {
    render(<MeepleCardGame {...baseProps} newThisWeekCount={3} />);
    expect(screen.getByText('+3')).toBeInTheDocument();
  });

  it('does not render the new-this-week badge when count < 2', () => {
    render(<MeepleCardGame {...baseProps} newThisWeekCount={1} />);
    expect(screen.queryByText('+1')).not.toBeInTheDocument();
  });

  it('renders the 🎲 cover fallback when coverUrl is missing', () => {
    // Real canonical DOM: GridCard renders 🎲 twice (Cover emoji-band via
    // coverEmoji="🎲" AND EntityBadge's default entityIcon.game glyph), so a
    // single getByText('🎲') is ambiguous. Scope to the cover-emoji-band slot
    // to verify the adapter's coverEmoji wiring specifically.
    const { container } = render(<MeepleCardGame {...baseProps} coverUrl={null} />);
    const coverBand = container.querySelector('[data-slot="cover-emoji-band"]');
    expect(coverBand).not.toBeNull();
    expect(coverBand).toHaveTextContent('🎲');
  });

  it('renders an <img> cover when coverUrl is provided', () => {
    const { container } = render(
      <MeepleCardGame {...baseProps} coverUrl="https://cdn.example/c.jpg" />
    );
    const img = container.querySelector('img');
    expect(img).not.toBeNull();
    expect(img).toHaveAttribute('src', 'https://cdn.example/c.jpg');
  });

  describe('Wikidata attribution footer (rendered by MeepleCard for entity=game)', () => {
    it('renders <footer> with license text when coverLicense is provided', () => {
      const { container } = render(
        <MeepleCardGame
          {...baseProps}
          coverLicense="CC BY-SA 4.0"
          coverAttribution="Doe, John"
          coverSourceUrl="https://commons.wikimedia.org/wiki/File:Catan.jpg"
        />
      );
      const footer = container.querySelector('footer');
      expect(footer).not.toBeNull();
      expect(footer).toHaveTextContent('CC BY-SA 4.0');
    });

    it('renders a source link when coverSourceUrl is provided', () => {
      const { container } = render(
        <MeepleCardGame
          {...baseProps}
          coverLicense="CC BY-SA 4.0"
          coverSourceUrl="https://commons.wikimedia.org/wiki/File:Catan.jpg"
        />
      );
      const link = container.querySelector('footer a');
      expect(link).not.toBeNull();
      expect(link).toHaveAttribute('href', 'https://commons.wikimedia.org/wiki/File:Catan.jpg');
      expect(link).toHaveAttribute('rel', 'nofollow noopener noreferrer');
      expect(link).toHaveAttribute('target', '_blank');
    });

    it('renders no <footer> when coverLicense is omitted', () => {
      const { container } = render(<MeepleCardGame {...baseProps} />);
      expect(container.querySelector('footer')).toBeNull();
    });
  });
});
