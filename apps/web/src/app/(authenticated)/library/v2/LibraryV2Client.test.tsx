import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { LibraryV2Client, type LibraryV2Item } from './LibraryV2Client';

type Listener = (e: MediaQueryListEvent) => void;

function installMatchMedia(matches: boolean): void {
  const listeners = new Set<Listener>();
  const mql = {
    matches,
    media: '(min-width: 768px)',
    addEventListener: (_e: 'change', cb: Listener) => listeners.add(cb),
    removeEventListener: (_e: 'change', cb: Listener) => listeners.delete(cb),
    onchange: null,
  };
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue(mql),
  });
}

const items: LibraryV2Item[] = [
  {
    id: 'g1',
    title: 'Catan',
    publisher: 'Kosmos',
    owned: true,
    wishlist: false,
    description: 'Classico',
    sessionCount: 2,
    chatCount: 1,
  },
  {
    id: 'g2',
    title: 'Root',
    publisher: 'Leder',
    owned: false,
    wishlist: true,
    sessionCount: 0,
    chatCount: 0,
  },
];

beforeEach(() => {
  if (typeof Element.prototype.hasPointerCapture !== 'function') {
    Element.prototype.hasPointerCapture = () => false;
  }
  if (typeof Element.prototype.setPointerCapture !== 'function') {
    Element.prototype.setPointerCapture = () => {};
  }
  if (typeof Element.prototype.releasePointerCapture !== 'function') {
    Element.prototype.releasePointerCapture = () => {};
  }
  if (typeof Element.prototype.scrollIntoView !== 'function') {
    Element.prototype.scrollIntoView = () => {};
  }
  const realGCS = window.getComputedStyle.bind(window);
  vi.spyOn(window, 'getComputedStyle').mockImplementation((el: Element, pseudo?: string | null) => {
    const cs = realGCS(el, pseudo ?? undefined);
    if (!cs.transform) {
      try {
        Object.defineProperty(cs, 'transform', { configurable: true, value: 'none' });
      } catch {
        // ignore
      }
    }
    return cs;
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('LibraryV2Client (mobile)', () => {
  beforeEach(() => {
    installMatchMedia(false);
  });

  it('renders the mobile hub with all items', () => {
    render(<LibraryV2Client items={items} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Root')).toBeInTheDocument();
  });

  it('opens drawer with game detail when item clicked', async () => {
    render(<LibraryV2Client items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /catan/i }));
    expect(await screen.findByText('Classico')).toBeInTheDocument();
  });
});

describe('LibraryV2Client (desktop)', () => {
  beforeEach(() => {
    installMatchMedia(true);
  });

  it('renders desktop split-view (no drawer)', () => {
    render(<LibraryV2Client items={items} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Root')).toBeInTheDocument();
    // empty detail placeholder visible initially
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });
});
