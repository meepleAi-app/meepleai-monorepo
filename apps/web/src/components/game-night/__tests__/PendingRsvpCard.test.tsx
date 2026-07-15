import { render, screen, within, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PendingRsvpCard } from '../PendingRsvpCard';

const baseProps = {
  eventId: 'gn-sat-marco',
  title: 'Serata da Marco',
  inviterName: 'Marco',
  onConfirm: () => {},
  onDecline: () => {},
};

describe('PendingRsvpCard', () => {
  it('renders the "Da confermare" badge, title and inviter', () => {
    render(<PendingRsvpCard {...baseProps} />);
    const card = screen.getByTestId('pending-rsvp-card');
    expect(card).toHaveAttribute('data-event-id', 'gn-sat-marco');
    expect(within(card).getByText('Da confermare')).toBeInTheDocument();
    expect(within(card).getByText('Serata da Marco')).toBeInTheDocument();
    expect(within(card).getByText(/Marco ti ha invitato/i)).toBeInTheDocument();
  });

  it('Conferma/Declina buttons are >=44px and fire callbacks', () => {
    const onConfirm = vi.fn();
    const onDecline = vi.fn();
    render(<PendingRsvpCard {...baseProps} onConfirm={onConfirm} onDecline={onDecline} />);
    const confirm = screen.getByRole('button', { name: 'Conferma' });
    const decline = screen.getByRole('button', { name: 'Declina' });
    expect(confirm.className).toContain('min-h-11');
    expect(decline.className).toContain('min-h-11');
    fireEvent.click(confirm);
    fireEvent.click(decline);
    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onDecline).toHaveBeenCalledOnce();
  });

  it('disables buttons and shows offline tooltip when disabled', () => {
    render(<PendingRsvpCard {...baseProps} disabled />);
    expect(screen.getByRole('button', { name: 'Conferma' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Declina' })).toBeDisabled();
    expect(screen.getByTestId('pending-rsvp-card')).toHaveAttribute(
      'title',
      'Offline — RSVP disponibile alla riconnessione'
    );
  });
});
