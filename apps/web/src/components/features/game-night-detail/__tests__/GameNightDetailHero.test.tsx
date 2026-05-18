/**
 * Tests for GameNightDetailHero (Issue #951 commit 2b).
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { GameNightDetailHero, type GameNightDetailHeroLabels } from '../GameNightDetailHero';

const baseLabels: GameNightDetailHeroLabels = {
  statusLabel: 'Attiva',
  scheduledLine: 'Sabato 17 maggio · 21:00',
  organizedByLine: 'Organizzata da Marco R.',
};

const baseProps = {
  title: 'Sabato boardgame con i Padovani',
  status: 'Published' as const,
  organizerId: '11111111-1111-1111-1111-111111111111',
  organizerName: 'Marco R.',
};

describe('GameNightDetailHero', () => {
  it('renders title + status + scheduled line + organizer', () => {
    render(<GameNightDetailHero {...baseProps} labels={baseLabels} />);
    expect(screen.getByRole('heading', { name: baseProps.title })).toBeInTheDocument();
    expect(screen.getByText('Sabato 17 maggio · 21:00')).toBeInTheDocument();
    expect(screen.getByText('Organizzata da Marco R.')).toBeInTheDocument();
    expect(screen.getByText('Attiva')).toBeInTheDocument();
  });

  it('renders the organizer avatar with full-name aria-label', () => {
    render(<GameNightDetailHero {...baseProps} labels={baseLabels} />);
    expect(screen.getByRole('img', { name: 'Marco R.' })).toBeInTheDocument();
  });

  it('renders meta line when provided', () => {
    render(
      <GameNightDetailHero
        {...baseProps}
        labels={{ ...baseLabels, metaLine: '8 invitati · 3h stimate' }}
      />
    );
    expect(screen.getByText('8 invitati · 3h stimate')).toBeInTheDocument();
  });

  it('renders location as a button when onOpenLocation is provided', async () => {
    const user = userEvent.setup();
    const onOpenLocation = vi.fn();
    render(
      <GameNightDetailHero
        {...baseProps}
        labels={{ ...baseLabels, locationLine: 'Casa Marco · Padova' }}
        onOpenLocation={onOpenLocation}
      />
    );

    const locationBtn = screen.getByRole('button', { name: /Casa Marco · Padova/ });
    await user.click(locationBtn);
    expect(onOpenLocation).toHaveBeenCalledOnce();
  });

  it('renders location as static text when onOpenLocation is omitted', () => {
    render(
      <GameNightDetailHero
        {...baseProps}
        labels={{ ...baseLabels, locationLine: 'Casa Marco · Padova' }}
      />
    );
    expect(screen.getByText('Casa Marco · Padova')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Casa Marco · Padova/ })).not.toBeInTheDocument();
  });

  it('omits location section entirely when no locationLine label', () => {
    render(<GameNightDetailHero {...baseProps} labels={baseLabels} />);
    expect(screen.queryByText(/Casa Marco/)).not.toBeInTheDocument();
  });

  it.each([
    ['Draft', 'from-entity-event/[0.16]'],
    ['Published', 'from-entity-toolkit/[0.16]'],
    ['Completed', 'bg-muted'],
    ['Cancelled', 'from-destructive/[0.14]'],
  ] as const)('applies %s gradient via Tailwind class fragment %s', (status, expectedFragment) => {
    render(<GameNightDetailHero {...baseProps} status={status} labels={baseLabels} />);
    const hero = screen.getByTestId('game-night-detail-hero');
    expect(hero).toHaveAttribute('data-status', status);
    expect(hero.className).toContain(expectedFragment);
  });

  it('strikes through the title when status=Cancelled', () => {
    render(<GameNightDetailHero {...baseProps} status="Cancelled" labels={baseLabels} />);
    const heading = screen.getByRole('heading', { name: baseProps.title });
    expect(heading.className).toContain('line-through');
  });

  it('does not strike through title for non-Cancelled statuses', () => {
    const { rerender } = render(
      <GameNightDetailHero {...baseProps} status="Draft" labels={baseLabels} />
    );
    expect(screen.getByRole('heading', { name: baseProps.title }).className).not.toContain(
      'line-through'
    );

    rerender(<GameNightDetailHero {...baseProps} status="Completed" labels={baseLabels} />);
    expect(screen.getByRole('heading', { name: baseProps.title }).className).not.toContain(
      'line-through'
    );
  });

  it('renders the status badge with the localized status label', () => {
    render(
      <GameNightDetailHero
        {...baseProps}
        status="Cancelled"
        labels={{ ...baseLabels, statusLabel: 'Annullata' }}
      />
    );
    const hero = screen.getByTestId('game-night-detail-hero');
    expect(within(hero).getByText('Annullata')).toBeInTheDocument();
  });

  describe('mode prop (issue #1169)', () => {
    it('defaults to data-mode="authenticated" when prop is omitted', () => {
      render(<GameNightDetailHero {...baseProps} labels={baseLabels} />);
      expect(screen.getByTestId('game-night-detail-hero')).toHaveAttribute(
        'data-mode',
        'authenticated'
      );
    });

    it('sets data-mode="public" when mode="public" is passed', () => {
      render(<GameNightDetailHero {...baseProps} labels={baseLabels} mode="public" />);
      expect(screen.getByTestId('game-night-detail-hero')).toHaveAttribute('data-mode', 'public');
    });

    it('suppresses the location button affordance in mode="public"', () => {
      const onOpenLocation = vi.fn();
      render(
        <GameNightDetailHero
          {...baseProps}
          labels={{ ...baseLabels, locationLine: 'Casa Marco · Padova' }}
          onOpenLocation={onOpenLocation}
          mode="public"
        />
      );
      // Location is rendered as static text, not as an interactive button.
      expect(screen.getByText('Casa Marco · Padova')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Casa Marco · Padova/ })).not.toBeInTheDocument();
    });

    it('still renders the location button in mode="authenticated" (default)', async () => {
      const user = userEvent.setup();
      const onOpenLocation = vi.fn();
      render(
        <GameNightDetailHero
          {...baseProps}
          labels={{ ...baseLabels, locationLine: 'Casa Marco · Padova' }}
          onOpenLocation={onOpenLocation}
        />
      );
      const btn = screen.getByRole('button', { name: /Casa Marco · Padova/ });
      await user.click(btn);
      expect(onOpenLocation).toHaveBeenCalledOnce();
    });
  });
});
