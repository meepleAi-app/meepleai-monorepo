/**
 * MeepleCardBrowser Tests
 *
 * Coverage:
 * - Does not render when closed (initial state)
 * - Renders overlay when opened via context
 * - Shows carousel indicator n/total
 * - Closes on ESC key
 */

import React from 'react';
import { render, screen, act, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  CardBrowserProvider,
  useCardBrowser,
  type CardRef,
} from '../meeple-card-browser/CardBrowserContext';
import { MeepleCardBrowser } from '../meeple-card-browser/MeepleCardBrowser';

// ---------- Mocks ----------

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>
      ) => (
        <div ref={ref} {...props}>
          {children}
        </div>
      )
    ),
  },
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useMotionValue: (initial: number) => ({ get: () => initial, set: () => {} }),
  useTransform: (_mv: unknown, _input: number[], output: number[]) => output[0],
}));

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

// Mock DeckStackDrawer to avoid Sheet portal issues in jsdom
vi.mock('../meeple-card-browser/DeckStackDrawer', () => ({
  DeckStackDrawer: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="mock-deck-stack">DeckStack</div> : null,
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

/** Test harness that exposes open() via a button */
function OpenButton({ cards }: { cards: CardRef[] }) {
  const { open } = useCardBrowser();
  return (
    <button onClick={() => open(cards, 0)} data-testid="open-button">
      Open
    </button>
  );
}

function renderBrowser(cards: CardRef[] = [makeCard('a'), makeCard('b'), makeCard('c')]) {
  return render(
    <CardBrowserProvider>
      <OpenButton cards={cards} />
      <MeepleCardBrowser />
    </CardBrowserProvider>
  );
}

// ---------- sessionStorage + DOM stubs ----------

const store: Record<string, string> = {};

beforeEach(() => {
  Object.keys(store).forEach(k => delete store[k]);
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(k => store[k] ?? null);
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation((k, v) => {
    store[k] = v;
  });
  // jsdom does not implement scrollTo
  Element.prototype.scrollTo = vi.fn();
});

// ---------- Tests ----------

describe('MeepleCardBrowser', () => {
  it('does not render when closed', () => {
    renderBrowser();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders overlay when opened', async () => {
    renderBrowser();

    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByLabelText('Card browser')).toBeInTheDocument();
  });

  it('shows carousel indicator n/total', async () => {
    renderBrowser();

    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    expect(screen.getByTestId('carousel-indicator')).toHaveTextContent('1/3');
  });

  it('closes on ESC key', async () => {
    renderBrowser();

    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await act(async () => {
      fireEvent.keyDown(window, { key: 'Escape' });
    });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders close and history buttons', async () => {
    renderBrowser();

    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    expect(screen.getByLabelText('Close')).toBeInTheDocument();
    expect(screen.getByLabelText('History')).toBeInTheDocument();
  });

  it('renders a card for each entry in the carousel', async () => {
    renderBrowser();

    await act(async () => {
      fireEvent.click(screen.getByTestId('open-button'));
    });

    const container = screen.getByTestId('carousel-container');
    // Each card renders in a snap-center div
    expect(container.children).toHaveLength(3);
  });
});
