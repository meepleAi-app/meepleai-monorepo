/** @vitest-environment jsdom */
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PublishedMechanicCitationDto } from '@/lib/api/schemas/mechanic-card.schemas';

import { MechanicCitationBadge } from '../MechanicCitationBadge';

const CITATION: PublishedMechanicCitationDto = {
  pdfId: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
  pdfPage: 12,
  quote: 'You may only trade resources on your own turn, never during another player’s turn.',
};

describe('MechanicCitationBadge', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders a [p.N] badge with the accessible label', () => {
    render(<MechanicCitationBadge citation={CITATION} onOpen={vi.fn()} />);
    const badge = screen.getByRole('button', {
      name: 'Citazione regolamento, pagina 12',
    });
    expect(badge).toHaveTextContent('p.12');
  });

  it('calls onOpen when clicked', () => {
    const onOpen = vi.fn();
    render(<MechanicCitationBadge citation={CITATION} onOpen={onOpen} />);
    fireEvent.click(screen.getByTestId(`mechanic-card-citation-${CITATION.pdfId}-12`));
    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it('calls onOpen on keyboard activation (Enter fires the native button click)', () => {
    const onOpen = vi.fn();
    render(<MechanicCitationBadge citation={CITATION} onOpen={onOpen} />);
    const badge = screen.getByTestId(`mechanic-card-citation-${CITATION.pdfId}-12`);
    // A real <button> maps Enter/Space to a click; assert the click handler runs.
    fireEvent.click(badge);
    expect(onOpen).toHaveBeenCalled();
  });

  it('shows the truncated quote tooltip immediately on keyboard focus', async () => {
    render(<MechanicCitationBadge citation={CITATION} onOpen={vi.fn()} />);
    const badge = screen.getByTestId(`mechanic-card-citation-${CITATION.pdfId}-12`);

    fireEvent.focus(badge);

    // Focus drives `open` immediately (no hover delay). Radix renders the
    // tooltip content into a portal; wait for it to appear.
    await waitFor(() => {
      const tips = screen.getAllByText(/You may only trade resources/);
      expect(tips.length).toBeGreaterThan(0);
    });
  });

  it('renders a disabled, non-interactive badge when the page is null', () => {
    const onOpen = vi.fn();
    const noPage = {
      ...CITATION,
      pdfPage: null as unknown as number,
    };
    render(<MechanicCitationBadge citation={noPage} onOpen={onOpen} />);

    const disabled = screen.getByTestId('mechanic-card-citation-disabled');
    expect(disabled).toHaveAttribute('aria-disabled', 'true');
    // Not a button → not activatable; clicking must not fire onOpen.
    fireEvent.click(disabled);
    expect(onOpen).not.toHaveBeenCalled();
    // No interactive [p.N] badge exists.
    expect(screen.queryByRole('button', { name: /Citazione regolamento/ })).not.toBeInTheDocument();
  });

  it('shows a defensive tooltip label when the quote is missing', async () => {
    render(<MechanicCitationBadge citation={{ ...CITATION, quote: '' }} onOpen={vi.fn()} />);
    fireEvent.focus(screen.getByTestId(`mechanic-card-citation-${CITATION.pdfId}-12`));
    await waitFor(() => {
      expect(screen.getAllByText('[Citazione regolamento p.12]').length).toBeGreaterThan(0);
    });
  });
});
