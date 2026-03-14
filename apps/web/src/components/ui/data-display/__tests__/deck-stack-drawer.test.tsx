/**
 * DeckStackDrawer Tests
 *
 * Coverage:
 * - Renders history entries
 * - Shows count in header
 * - Renders clear button when history exists
 */

import React from 'react';
import { render, screen, act, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  CardBrowserProvider,
  useCardBrowser,
  type CardRef,
} from '../meeple-card-browser/CardBrowserContext';
import { DeckStackDrawer } from '../meeple-card-browser/DeckStackDrawer';

// ---------- Mocks ----------

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

// Mock matchMedia
vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ---------- Helpers ----------

function makeCard(id: string): CardRef {
  return {
    id,
    entity: 'game',
    title: `Card ${id}`,
    subtitle: `Sub ${id}`,
    color: '25 95% 45%',
  };
}

/** Helper to open browser with cards (populates history) then render drawer */
function DrawerHarness({ onClose }: { onClose: () => void }) {
  const { open, cards } = useCardBrowser();
  const currentCard = cards[0];

  return (
    <>
      <button
        onClick={() => {
          const testCards = [makeCard('a'), makeCard('b')];
          open(testCards, 0);
        }}
        data-testid="open-button"
      >
        Open
      </button>
      <button
        onClick={() => {
          const testCards = [makeCard('a'), makeCard('b')];
          open(testCards, 1);
        }}
        data-testid="open-second"
      >
        Open Second
      </button>
      <DeckStackDrawer isOpen={true} currentCardId={currentCard?.id ?? ''} onClose={onClose} />
    </>
  );
}

function renderDrawer(onClose = vi.fn()) {
  return {
    onClose,
    ...render(
      <CardBrowserProvider>
        <DrawerHarness onClose={onClose} />
      </CardBrowserProvider>
    ),
  };
}

// ---------- sessionStorage mock ----------

const store: Record<string, string> = {};

beforeEach(() => {
  Object.keys(store).forEach(k => delete store[k]);
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(k => store[k] ?? null);
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation((k, v) => {
    store[k] = v;
  });
});

// ---------- Tests ----------

describe('DeckStackDrawer', () => {
  it('renders history entries after opening cards', async () => {
    renderDrawer();

    // Open browser with first card (adds to history)
    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    // Open again with second card index (adds second card to history)
    await act(async () => {
      fireEvent.click(screen.getByTestId('open-second'));
    });

    const entries = screen.getAllByTestId('deck-stack-entry');
    expect(entries.length).toBeGreaterThanOrEqual(1);
  });

  it('shows count in header', async () => {
    renderDrawer();

    // Open to populate history
    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    const title = screen.getByTestId('deck-stack-title');
    expect(title.textContent).toMatch(/History \(\d+\)/);
  });

  it('renders clear button when history exists', async () => {
    renderDrawer();

    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    expect(screen.getByTestId('clear-history-button')).toBeInTheDocument();
    expect(screen.getByText('Clear history')).toBeInTheDocument();
  });

  it('shows empty state when no history', () => {
    render(
      <CardBrowserProvider>
        <DeckStackDrawer isOpen={true} currentCardId="" onClose={vi.fn()} />
      </CardBrowserProvider>
    );

    expect(screen.getByText('No history yet')).toBeInTheDocument();
  });
});
