/**
 * Wave A.4 (Issue #603) — Hero rendering tests.
 *
 * Verifies the game-cover hero contract from spec §3.2:
 *  - Title (h1) + entity pill + meta line construction
 *  - coverUrl present → <img> rendered; absent → emoji fallback
 *  - rating clamped to 0..5 (Stars subcomponent), display formatted to 1 decimal
 *  - ConnectionPip: count > 0 → numeric badge; count = 0 → empty pip with `+` sign
 *    and aria-label = label only (no count suffix)
 *  - meta parts conditionally appended (only non-null fields)
 *  - playersText: equal min/max → single number; different → "min–max"
 *  - compact prop variations (no DOM impact verified — sizing classes change only)
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Hero, type HeroLabels } from './hero';

const labels: HeroLabels = {
  entityLabel: 'Game',
  ratingAriaLabel: 'Rating',
  ratingOf: 'of',
  toolkitsLabel: 'Toolkits',
  agentsLabel: 'Agents',
  kbsLabel: 'KBs',
  metaPlayers: 'players',
  metaMinutes: 'min',
  metaComplexity: 'Complexity',
  metaAuthor: 'by',
};

describe('Hero (Wave A.4)', () => {
  it('renders title and entity pill', () => {
    render(<Hero title="Catan" toolkitsCount={0} agentsCount={0} kbsCount={0} labels={labels} />);
    expect(screen.getByRole('heading', { level: 1, name: 'Catan' })).toBeInTheDocument();
    expect(screen.getByText('Game')).toBeInTheDocument();
  });

  it('renders default emoji 🎲 when coverUrl is omitted', () => {
    const { container } = render(
      <Hero title="Catan" toolkitsCount={0} agentsCount={0} kbsCount={0} labels={labels} />
    );
    // The cover region renders the large emoji; the entity pill also has 🎲
    // (always present). Distinguish via the cover-emoji size class.
    const coverEmoji = container.querySelector('span.text-\\[110px\\]');
    expect(coverEmoji?.textContent).toBe('🎲');
  });

  it('renders custom emoji when provided', () => {
    const { container } = render(
      <Hero
        title="Catan"
        emoji="🏝️"
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    const coverEmoji = container.querySelector('span.text-\\[110px\\]');
    expect(coverEmoji?.textContent).toBe('🏝️');
  });

  it('renders <img> when coverUrl is provided (and no large emoji fallback)', () => {
    const { container } = render(
      <Hero
        title="Catan"
        coverUrl="https://example.com/catan.jpg"
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    const img = container.querySelector('img');
    expect(img).not.toBeNull();
    expect(img).toHaveAttribute('src', 'https://example.com/catan.jpg');
    // The large cover-emoji span (text-[110px]) must NOT be rendered. The
    // entity pill 🎲 is unrelated and always rendered, so we don't assert on it.
    expect(container.querySelector('span.text-\\[110px\\]')).toBeNull();
  });

  it('renders rating stars with aria-label (clamped to 0..5)', () => {
    render(
      <Hero
        title="Catan"
        rating={4.3}
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    // Math.round(4.3) = 4 — aria-label uses rounded value
    expect(screen.getByLabelText('Rating: 4 of 5')).toBeInTheDocument();
    // Display value formatted to 1 decimal
    expect(screen.getByText('4.3')).toBeInTheDocument();
  });

  it('clamps rating above 5 to 5', () => {
    render(
      <Hero
        title="Catan"
        rating={6.7}
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    expect(screen.getByLabelText('Rating: 5 of 5')).toBeInTheDocument();
  });

  it('clamps rating below 0 to 0', () => {
    render(
      <Hero
        title="Catan"
        rating={-2}
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    expect(screen.getByLabelText('Rating: 0 of 5')).toBeInTheDocument();
  });

  it('does not render rating when rating is null/undefined', () => {
    render(<Hero title="Catan" toolkitsCount={0} agentsCount={0} kbsCount={0} labels={labels} />);
    expect(screen.queryByLabelText(/^Rating:/)).toBeNull();
  });

  it('renders meta line with author, year, players, minutes, complexity', () => {
    render(
      <Hero
        title="Catan"
        authorName="Klaus Teuber"
        year={1995}
        minPlayers={3}
        maxPlayers={4}
        playingTimeMinutes={75}
        complexityRating={2.3}
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    const meta = screen.getByText(/Klaus Teuber/).textContent ?? '';
    expect(meta).toContain('by Klaus Teuber');
    expect(meta).toContain('1995');
    expect(meta).toContain('3–4 players');
    expect(meta).toContain('75 min');
    expect(meta).toContain('Complexity 2.3');
  });

  it('renders single player number when min equals max', () => {
    render(
      <Hero
        title="Catan"
        minPlayers={4}
        maxPlayers={4}
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    expect(screen.getByText(/4 players/)).toBeInTheDocument();
    // Should NOT render a range
    expect(screen.queryByText(/4–4/)).toBeNull();
  });

  it('omits meta line when no meta fields are provided', () => {
    const { container } = render(
      <Hero title="Catan" toolkitsCount={0} agentsCount={0} kbsCount={0} labels={labels} />
    );
    // The meta paragraph has the mono / uppercase tracking class
    const allParas = container.querySelectorAll('p');
    // Only the metaParts paragraph would carry "·" separators — none should exist
    allParas.forEach(p => {
      expect(p.textContent ?? '').not.toContain(' · ');
    });
  });

  it('omits players meta if minPlayers or maxPlayers is null', () => {
    render(
      <Hero
        title="Catan"
        minPlayers={3}
        maxPlayers={null}
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
      />
    );
    expect(screen.queryByText(/players/)).toBeNull();
  });

  it('renders ConnectionPip with count when > 0', () => {
    render(<Hero title="Catan" toolkitsCount={3} agentsCount={5} kbsCount={2} labels={labels} />);
    expect(screen.getByLabelText('Toolkits: 3')).toBeInTheDocument();
    expect(screen.getByLabelText('Agents: 5')).toBeInTheDocument();
    expect(screen.getByLabelText('KBs: 2')).toBeInTheDocument();
  });

  it('renders empty ConnectionPip with label-only aria when count = 0', () => {
    render(<Hero title="Catan" toolkitsCount={0} agentsCount={0} kbsCount={0} labels={labels} />);
    // aria-label is the bare label (no `:` suffix)
    expect(screen.getByLabelText('Toolkits')).toBeInTheDocument();
    expect(screen.getByLabelText('Agents')).toBeInTheDocument();
    expect(screen.getByLabelText('KBs')).toBeInTheDocument();
  });

  it('marks empty pips with data-empty="true"', () => {
    const { container } = render(
      <Hero title="Catan" toolkitsCount={0} agentsCount={2} kbsCount={0} labels={labels} />
    );
    const toolkit = container.querySelector('[data-slot="connection-pip"][data-entity="toolkit"]');
    const agent = container.querySelector('[data-slot="connection-pip"][data-entity="agent"]');
    expect(toolkit).toHaveAttribute('data-empty', 'true');
    expect(agent).toHaveAttribute('data-empty', 'false');
  });

  it('renders ConnectionBar group with aria-label "Connections"', () => {
    render(<Hero title="Catan" toolkitsCount={0} agentsCount={0} kbsCount={0} labels={labels} />);
    expect(screen.getByRole('group', { name: 'Connections' })).toBeInTheDocument();
  });

  it('passes through optional className', () => {
    const { container } = render(
      <Hero
        title="Catan"
        toolkitsCount={0}
        agentsCount={0}
        kbsCount={0}
        labels={labels}
        className="custom-cls"
      />
    );
    const root = container.querySelector('[data-slot="shared-game-detail-hero"]');
    expect(root?.className).toContain('custom-cls');
  });
});
