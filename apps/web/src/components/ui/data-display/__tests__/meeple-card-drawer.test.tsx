/**
 * MeepleCard — Drawer integration tests
 * Issue #5025 — MeepleCard "i" button opens ExtraMeepleCardDrawer
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { MeepleCard } from '../meeple-card';
import { DRAWER_TEST_IDS } from '../extra-meeple-card/drawer-test-ids';

// ============================================================================
// Mocks
// ============================================================================

// Mock Next.js Image
vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

// Mock framer-motion (used by FlipCard)
vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, style, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>
      ) => (
        <div ref={ref} style={style as React.CSSProperties} {...props}>
          {children}
        </div>
      )
    ),
  },
}));

// Mock fetch (drawer fetches entity data)
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

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

// ============================================================================
// Helpers
// ============================================================================

const defaultProps = {
  entity: 'game' as const,
  title: 'Catan',
  subtitle: 'Kosmos',
};

function mockGameFetch() {
  mockFetch.mockResolvedValue({
    ok: true,
    json: () =>
      Promise.resolve({
        id: 'game-1',
        title: 'Catan',
        publisher: 'Kosmos',
        yearPublished: 1995,
        minPlayers: 3,
        maxPlayers: 4,
        playTimeMinutes: 90,
        description: 'Trade, build, settle.',
        averageRating: 7.8,
        totalPlays: 150,
        faqCount: 12,
        rulesDocumentCount: 3,
      }),
  });
}

// ============================================================================
// Tests
// ============================================================================

describe('MeepleCard — drawer integration (Issue #5025)', () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  // --------------------------------------------------------------------------
  // entityId absent → button not rendered
  // --------------------------------------------------------------------------

  describe('when entityId is absent', () => {
    it('does not render the info button when showInfoButton is true but entityId is absent', () => {
      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
          // No entityId, no infoHref
        />
      );

      expect(screen.queryByTestId('meeple-card-info-button')).not.toBeInTheDocument();
    });

    it('does not render the drawer', () => {
      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
        />
      );

      // Sheet/dialog should not be present
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // entityId present → button opens drawer
  // --------------------------------------------------------------------------

  describe('when entityId is present', () => {
    it('renders the info button as a <button> (not a link)', () => {
      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
          entityId="game-1"
        />
      );

      const btn = screen.getByTestId('meeple-card-info-button');
      expect(btn.tagName).toBe('BUTTON');
    });

    it('clicking the info button opens the drawer', async () => {
      mockGameFetch();
      const user = userEvent.setup();

      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
          entityId="game-1"
        />
      );

      const infoBtn = screen.getByTestId('meeple-card-info-button');
      await user.click(infoBtn);

      // Drawer entity label should appear (Sheet content)
      await waitFor(() => {
        expect(screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).toBeInTheDocument();
      });
    });

    it('drawer can be closed via X button', async () => {
      mockGameFetch();
      const user = userEvent.setup();

      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
          entityId="game-1"
        />
      );

      // Open drawer
      await user.click(screen.getByTestId('meeple-card-info-button'));
      await waitFor(() => expect(screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).toBeInTheDocument());

      // Close via X
      const closeBtn = screen.getByRole('button', { name: /chiudi pannello/i });
      await user.click(closeBtn);

      await waitFor(() => {
        expect(screen.queryByTestId(DRAWER_TEST_IDS.ENTITY_LABEL)).not.toBeInTheDocument();
      });
    });

    it('uses entity type mapping: game → "game" drawer type', async () => {
      mockGameFetch();
      const user = userEvent.setup();

      render(
        <MeepleCard
          {...defaultProps}
          entity="game"
          showInfoButton
          entityId="game-1"
        />
      );

      await user.click(screen.getByTestId('meeple-card-info-button'));

      await waitFor(() => {
        const label = screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL);
        expect(label).toBeInTheDocument();
        expect(label).toHaveTextContent('Dettaglio Gioco');
      });
    });

    it('uses entity type mapping: agent → "agent" drawer type', async () => {
      // Agent drawer fetches agent details + threads + docs
      mockFetch.mockImplementation((url: string) => {
        if ((url as string).includes('/api/v1/agents/')) {
          return Promise.resolve({
            ok: true,
            json: () =>
              Promise.resolve({
                id: 'agent-1',
                name: 'Catan Expert',
                type: 'qa',
                strategyName: 'hybrid-rag',
                strategyParameters: {},
                isActive: true,
                isIdle: false,
                invocationCount: 0,
                lastInvokedAt: null,
                createdAt: '2026-01-01T00:00:00Z',
              }),
          });
        }
        return Promise.resolve({ ok: true, json: () => Promise.resolve([]) });
      });

      const user = userEvent.setup();

      render(
        <MeepleCard
          entity="agent"
          title="Catan Expert"
          showInfoButton
          entityId="agent-1"
        />
      );

      await user.click(screen.getByTestId('meeple-card-info-button'));

      await waitFor(() => {
        const label = screen.getByTestId(DRAWER_TEST_IDS.ENTITY_LABEL);
        expect(label).toBeInTheDocument();
        expect(label).toHaveTextContent('Dettaglio Agente');
      });
    });

    it('fetches entity data with the provided entityId', async () => {
      mockGameFetch();
      const user = userEvent.setup();

      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
          entityId="specific-game-id"
        />
      );

      await user.click(screen.getByTestId('meeple-card-info-button'));

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          '/api/v1/library/games/specific-game-id',
          expect.objectContaining({ signal: expect.any(AbortSignal) })
        );
      });
    });
  });

  // --------------------------------------------------------------------------
  // Backward compat: infoHref without entityId → link mode
  // --------------------------------------------------------------------------

  describe('backward compatibility: infoHref without entityId', () => {
    it('renders info button as a link when only infoHref is provided', () => {
      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
          infoHref="/games/catan"
        />
      );

      const btn = screen.getByTestId('meeple-card-info-button');
      expect(btn.tagName).toBe('A');
      expect(btn).toHaveAttribute('href', '/games/catan');
    });

    it('does not render drawer when only infoHref is provided', () => {
      render(
        <MeepleCard
          {...defaultProps}
          showInfoButton
          infoHref="/games/catan"
        />
      );

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // No showInfoButton → nothing rendered regardless
  // --------------------------------------------------------------------------

  it('does not render info button when showInfoButton is false even with entityId', () => {
    render(
      <MeepleCard
        {...defaultProps}
        showInfoButton={false}
        entityId="game-1"
      />
    );

    expect(screen.queryByTestId('meeple-card-info-button')).not.toBeInTheDocument();
  });

  // --------------------------------------------------------------------------
  // Entity types without drawer mapping → button not rendered
  // --------------------------------------------------------------------------

  it('does not render info button for player entity (no drawer mapping)', () => {
    render(
      <MeepleCard
        entity="player"
        title="Marco"
        showInfoButton
        entityId="player-1"
      />
    );

    // player has no DrawerEntityType mapping → button should not appear
    expect(screen.queryByTestId('meeple-card-info-button')).not.toBeInTheDocument();
  });
});
