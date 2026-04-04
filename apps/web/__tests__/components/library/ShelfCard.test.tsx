/**
 * ShelfCard Component Tests
 *
 * Tests for the 140px vetrina card used in horizontal library shelf rows.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ShelfCard } from '@/components/library/ShelfCard';
import type { ManaPip } from '@/components/library/ShelfCard';

describe('ShelfCard', () => {
  // --------------------------------------------------------------------------
  // Basic rendering
  // --------------------------------------------------------------------------

  it('renders game title and subtitle', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('renders the card root element with data-testid', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" />);

    expect(screen.getByTestId('shelf-card')).toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Mana pips
  // --------------------------------------------------------------------------

  it('renders mana pips when provided', () => {
    const pips: ManaPip[] = [
      { type: 'agent', active: true },
      { type: 'kb', active: false },
    ];

    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" manaPips={pips} />);

    expect(screen.getByTestId('mana-pips')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-agent')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-kb')).toBeInTheDocument();
  });

  it('does not render mana pip container when no pips provided', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" />);

    expect(screen.queryByTestId('mana-pips')).not.toBeInTheDocument();
  });

  it('does not render mana pip container when pips array is empty', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" manaPips={[]} />);

    expect(screen.queryByTestId('mana-pips')).not.toBeInTheDocument();
  });

  it('applies reduced opacity class to inactive mana pips', () => {
    const pips: ManaPip[] = [{ type: 'session', active: false }];

    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" manaPips={pips} />);

    const pip = screen.getByTestId('mana-pip-session');
    expect(pip).toHaveClass('opacity-30');
  });

  it('applies full opacity class to active mana pips', () => {
    const pips: ManaPip[] = [{ type: 'chatSession', active: true }];

    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" manaPips={pips} />);

    const pip = screen.getByTestId('mana-pip-chatSession');
    expect(pip).toHaveClass('opacity-100');
  });

  // --------------------------------------------------------------------------
  // Library status: badge / button
  // --------------------------------------------------------------------------

  it('renders "in library" badge when inLibrary is true', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" inLibrary />);

    expect(screen.getByTestId('in-library-badge')).toBeInTheDocument();
    expect(screen.getByText(/in libreria/i)).toBeInTheDocument();
  });

  it('does not render add button when inLibrary is true', () => {
    const onAdd = vi.fn();
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" inLibrary onAdd={onAdd} />);

    expect(screen.queryByTestId('add-button')).not.toBeInTheDocument();
  });

  it('renders add button when inLibrary is false and onAdd is provided', () => {
    const onAdd = vi.fn();
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" inLibrary={false} onAdd={onAdd} />);

    expect(screen.getByTestId('add-button')).toBeInTheDocument();
    expect(screen.getByText(/aggiungi/i)).toBeInTheDocument();
  });

  it('calls onAdd when add button is clicked', () => {
    const onAdd = vi.fn();
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" onAdd={onAdd} />);

    fireEvent.click(screen.getByTestId('add-button'));

    expect(onAdd).toHaveBeenCalledTimes(1);
  });

  it('does not render badge or button when inLibrary is undefined and no onAdd', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" />);

    expect(screen.queryByTestId('in-library-badge')).not.toBeInTheDocument();
    expect(screen.queryByTestId('add-button')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // MtG overlay slots
  // --------------------------------------------------------------------------

  it('renders mechanic icon when provided', () => {
    render(
      <ShelfCard
        title="Catan"
        subtitle="Klaus Teuber"
        mechanicIcon={<span data-testid="test-icon">TC</span>}
      />
    );

    expect(screen.getByTestId('mechanic-icon')).toBeInTheDocument();
    expect(screen.getByTestId('test-icon')).toBeInTheDocument();
  });

  it('renders state label with text when provided', () => {
    render(
      <ShelfCard
        title="Catan"
        subtitle="Klaus Teuber"
        stateLabel={{ text: 'Nuovo', variant: 'success' }}
      />
    );

    expect(screen.getByTestId('state-label')).toBeInTheDocument();
    expect(screen.getByText('Nuovo')).toBeInTheDocument();
  });

  it('does not render MtG overlay when neither mechanicIcon nor stateLabel provided', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" />);

    expect(screen.queryByTestId('mechanic-icon')).not.toBeInTheDocument();
    expect(screen.queryByTestId('state-label')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Click handler
  // --------------------------------------------------------------------------

  it('calls onClick when card is clicked', () => {
    const onClick = vi.fn();
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" onClick={onClick} />);

    fireEvent.click(screen.getByTestId('shelf-card'));

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('stops propagation when add button is clicked (does not trigger onClick)', () => {
    const onClick = vi.fn();
    const onAdd = vi.fn();
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" onClick={onClick} onAdd={onAdd} />);

    fireEvent.click(screen.getByTestId('add-button'));

    expect(onAdd).toHaveBeenCalledTimes(1);
    expect(onClick).not.toHaveBeenCalled();
  });

  // --------------------------------------------------------------------------
  // Cover icon fallback
  // --------------------------------------------------------------------------

  it('renders cover icon when no imageUrl is provided', () => {
    render(<ShelfCard title="Catan" subtitle="Klaus Teuber" coverIcon="🎲" />);

    expect(screen.getByText('🎲')).toBeInTheDocument();
  });
});
