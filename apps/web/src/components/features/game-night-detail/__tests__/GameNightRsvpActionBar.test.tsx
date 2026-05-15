/**
 * Tests for GameNightRsvpActionBar (Issue #951 commit 2, AC-H1 + AC-H2).
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import {
  GameNightRsvpActionBar,
  type GameNightRsvpActionBarLabels,
} from '../GameNightRsvpActionBar';

const LABELS: GameNightRsvpActionBarLabels = {
  sectionTitle: 'La tua risposta',
  accept: 'Partecipo',
  maybe: 'Forse',
  decline: 'Non partecipo',
  saving: 'Salvataggio…',
};

function renderBar(overrides: Partial<React.ComponentProps<typeof GameNightRsvpActionBar>> = {}) {
  const props: React.ComponentProps<typeof GameNightRsvpActionBar> = {
    labels: LABELS,
    currentResponse: undefined,
    pendingResponse: null,
    onSelect: vi.fn(),
    ...overrides,
  };
  const utils = render(<GameNightRsvpActionBar {...props} />);
  return { ...utils, onSelect: props.onSelect };
}

describe('GameNightRsvpActionBar', () => {
  it('renders three buttons with localized labels', () => {
    renderBar();
    expect(screen.getByRole('button', { name: /Partecipo/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Forse/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Non partecipo/ })).toBeInTheDocument();
  });

  it('emits onSelect with the chosen response', async () => {
    const user = userEvent.setup();
    const { onSelect } = renderBar();
    await user.click(screen.getByRole('button', { name: /Partecipo/ }));
    expect(onSelect).toHaveBeenCalledWith('Accepted');
  });

  it('marks the current response with aria-pressed + data-selected', () => {
    renderBar({ currentResponse: 'Maybe' });
    const maybeBtn = screen.getByTestId('rsvp-btn-maybe');
    expect(maybeBtn).toHaveAttribute('aria-pressed', 'true');
    expect(maybeBtn).toHaveAttribute('data-selected', 'true');

    const acceptBtn = screen.getByTestId('rsvp-btn-accepted');
    expect(acceptBtn).toHaveAttribute('aria-pressed', 'false');
  });

  it('shows saving label + disables all buttons while a mutation is in flight', () => {
    renderBar({ pendingResponse: 'Accepted' });
    expect(screen.getByText('Salvataggio…')).toBeInTheDocument();

    // All three buttons disabled to prevent double-submit during in-flight call (AC-H2).
    expect(screen.getByTestId('rsvp-btn-accepted')).toBeDisabled();
    expect(screen.getByTestId('rsvp-btn-maybe')).toBeDisabled();
    expect(screen.getByTestId('rsvp-btn-declined')).toBeDisabled();
  });

  it('disables all buttons when disabled prop is true (terminal state)', () => {
    renderBar({ disabled: true });
    expect(screen.getByTestId('rsvp-btn-accepted')).toBeDisabled();
    expect(screen.getByTestId('rsvp-btn-maybe')).toBeDisabled();
    expect(screen.getByTestId('rsvp-btn-declined')).toBeDisabled();
  });

  it('does NOT show saving label on non-pending buttons', () => {
    renderBar({ pendingResponse: 'Accepted' });
    // Only the in-flight button shows the saving label.
    const acceptBtn = screen.getByTestId('rsvp-btn-accepted');
    expect(acceptBtn).toHaveAttribute('data-pending', 'true');
    expect(acceptBtn.textContent).toContain('Salvataggio…');

    const maybeBtn = screen.getByTestId('rsvp-btn-maybe');
    expect(maybeBtn).toHaveAttribute('data-pending', 'false');
    expect(maybeBtn.textContent).toContain('Forse');
  });

  it('exposes the section as a labelled landmark for screen readers', () => {
    renderBar();
    const region = screen.getByRole('region', { name: 'La tua risposta' });
    expect(region).toBeInTheDocument();
  });

  it('current response with no pending uses entity-toned selected styling', () => {
    renderBar({ currentResponse: 'Accepted' });
    const acceptBtn = screen.getByTestId('rsvp-btn-accepted');
    // Entity tokens for selected state come from BUTTONS config — verify success palette applied.
    expect(acceptBtn.className).toContain('bg-success');
  });
});
