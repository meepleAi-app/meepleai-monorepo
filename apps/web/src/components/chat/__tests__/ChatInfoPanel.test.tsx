/**
 * ChatInfoPanel Tests - Issue #4365
 *
 * Tests for the right-side contextual info panel:
 * 1. Renders game MeepleCard when game provided
 * 2. Renders agent MeepleCard when agent provided
 * 3. Renders citations and handles click
 * 4. Renders suggested questions and handles click
 * 5. Shows empty citations message when none
 * 6. Desktop toggle collapse/expand
 * 7. Mobile Sheet drawer toggle
 * 8. Disables questions during streaming
 *
 * Pattern: Vitest + React Testing Library
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { ChatInfoPanel, type ChatInfoPanelProps } from '../ChatInfoPanel';
import { useChatInfoStore } from '@/store/chat-info/store';

// Mock store
vi.mock('@/store/chat-info/store', () => ({
  useChatInfoStore: vi.fn(),
}));

// Mock MeepleCard
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({ title, entity, 'data-testid': testId }: any) => (
    <div data-testid={testId || 'meeple-card'} data-entity={entity}>
      {title}
    </div>
  ),
}));

// Mock CitationLink
vi.mock('@/components/ui/data-display/citation-link', () => ({
  CitationLink: ({ pageNumber, onClick }: any) => (
    <button data-testid="citation-link" onClick={onClick}>
      p.{pageNumber}
    </button>
  ),
}));

// Mock Sheet
vi.mock('@/components/ui/navigation/sheet', () => ({
  Sheet: ({ children, open }: any) => (
    <div data-testid="sheet" data-open={open}>
      {open ? children : null}
    </div>
  ),
  SheetContent: ({ children }: any) => (
    <div data-testid="sheet-content">{children}</div>
  ),
  SheetHeader: ({ children }: any) => (
    <div data-testid="sheet-header">{children}</div>
  ),
  SheetTitle: ({ children }: any) => <h2>{children}</h2>,
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGame = {
  id: 'game-1',
  title: 'Catan',
  imageUrl: '/games/catan.jpg',
  publisher: 'KOSMOS',
  playerCount: '3-4',
  playTime: '60-120 min',
};

const mockAgent = {
  id: 'agent-1',
  name: 'Rules Expert',
  type: 'rules',
  description: 'Specialista regolamenti',
  avatarUrl: '/agents/rules.png',
};

const mockCitations = [
  { documentId: 'doc-1', pageNumber: 5, snippet: 'Roll two dice and move', relevanceScore: 0.95 },
  { documentId: 'doc-1', pageNumber: 12, snippet: 'Trade resources with other players', relevanceScore: 0.88 },
  { documentId: 'doc-2', pageNumber: 3, snippet: 'Place the robber on any hex', relevanceScore: 0.82 },
];

const mockQuestions = [
  'Come si costruisce una strada?',
  'Quante risorse servono per una città?',
  'Quando posso usare le carte sviluppo?',
];

const defaultStoreState = {
  isCollapsed: false,
  isMobileOpen: false,
  toggleCollapsed: vi.fn(),
  setCollapsed: vi.fn(),
  setMobileOpen: vi.fn(),
  toggleMobileOpen: vi.fn(),
};

// ============================================================================
// Helpers
// ============================================================================

function setupStore(overrides: Partial<typeof defaultStoreState> = {}) {
  const state = { ...defaultStoreState, ...overrides };
  vi.mocked(useChatInfoStore).mockReturnValue(state);
  return state;
}

function renderPanel(props: Partial<ChatInfoPanelProps> = {}) {
  return render(
    <ChatInfoPanel
      game={mockGame}
      agent={mockAgent}
      citations={mockCitations}
      suggestedQuestions={mockQuestions}
      {...props}
    />
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('ChatInfoPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    // Default: desktop viewport
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: query === '(min-width: 1024px)',
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });

    setupStore();
  });

  // --------------------------------------------------------------------------
  // Game Section
  // --------------------------------------------------------------------------

  describe('Game Section', () => {
    it('renders game card when game is provided', () => {
      renderPanel();
      const section = screen.getByTestId('chat-info-game-section');
      expect(section).toBeInTheDocument();
      expect(within(section).getByText('Catan')).toBeInTheDocument();
    });

    it('hides game section when no game', () => {
      renderPanel({ game: null });
      expect(screen.queryByTestId('chat-info-game-section')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Agent Section
  // --------------------------------------------------------------------------

  describe('Agent Section', () => {
    it('renders agent card when agent is provided', () => {
      renderPanel();
      const section = screen.getByTestId('chat-info-agent-section');
      expect(section).toBeInTheDocument();
      expect(within(section).getByText('Rules Expert')).toBeInTheDocument();
    });

    it('hides agent section when no agent', () => {
      renderPanel({ agent: null });
      expect(screen.queryByTestId('chat-info-agent-section')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Citations Section
  // --------------------------------------------------------------------------

  describe('Citations Section', () => {
    it('renders citations list with count', () => {
      renderPanel();
      const section = screen.getByTestId('chat-info-citations-section');
      expect(section).toBeInTheDocument();
      expect(within(section).getByText('Fonti (3)')).toBeInTheDocument();
    });

    it('renders citation snippets', () => {
      renderPanel();
      expect(screen.getByText('Roll two dice and move')).toBeInTheDocument();
      expect(screen.getByText('Trade resources with other players')).toBeInTheDocument();
    });

    it('handles citation click', async () => {
      const user = userEvent.setup();
      const onCitationClick = vi.fn();
      renderPanel({ onCitationClick });

      const citationLinks = screen.getAllByTestId('citation-link');
      await user.click(citationLinks[0]);

      expect(onCitationClick).toHaveBeenCalledWith('doc-1', 5);
    });

    it('deduplicates citations by documentId+pageNumber', () => {
      const duplicateCitations = [
        ...mockCitations,
        { documentId: 'doc-1', pageNumber: 5, snippet: 'Duplicate entry', relevanceScore: 0.7 },
      ];
      renderPanel({ citations: duplicateCitations });

      const section = screen.getByTestId('chat-info-citations-section');
      // Should show 3 unique, not 4
      expect(within(section).getByText('Fonti (3)')).toBeInTheDocument();
    });

    it('shows empty state when no citations', () => {
      renderPanel({ citations: [] });
      expect(
        screen.getByText('Le citazioni appariranno qui durante la conversazione.')
      ).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Suggested Questions Section
  // --------------------------------------------------------------------------

  describe('Suggested Questions', () => {
    it('renders question buttons', () => {
      renderPanel();
      expect(screen.getByText('Come si costruisce una strada?')).toBeInTheDocument();
      expect(screen.getByText('Quante risorse servono per una città?')).toBeInTheDocument();
    });

    it('handles question click', async () => {
      const user = userEvent.setup();
      const onQuestionClick = vi.fn();
      renderPanel({ onQuestionClick });

      await user.click(screen.getByText('Come si costruisce una strada?'));
      expect(onQuestionClick).toHaveBeenCalledWith('Come si costruisce una strada?');
    });

    it('disables questions during streaming', () => {
      renderPanel({ isStreaming: true, onQuestionClick: vi.fn() });
      const btn = screen.getByText('Come si costruisce una strada?').closest('button');
      expect(btn).toBeDisabled();
    });

    it('hides section when no questions', () => {
      renderPanel({ suggestedQuestions: [] });
      expect(screen.queryByTestId('chat-info-questions-section')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Desktop: Collapse/Expand
  // --------------------------------------------------------------------------

  describe('Desktop Panel', () => {
    it('renders side panel on desktop', () => {
      renderPanel();
      expect(screen.getByTestId('chat-info-panel')).toBeInTheDocument();
    });

    it('toggles collapse on button click', async () => {
      const user = userEvent.setup();
      const store = setupStore();
      renderPanel();

      await user.click(screen.getByTestId('chat-info-toggle'));
      expect(store.toggleCollapsed).toHaveBeenCalledOnce();
    });

    it('hides content when collapsed', () => {
      setupStore({ isCollapsed: true });
      renderPanel();

      const panel = screen.getByTestId('chat-info-panel');
      expect(panel).toHaveClass('w-0');
      expect(screen.queryByTestId('chat-info-panel-content')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Mobile: Sheet Drawer
  // --------------------------------------------------------------------------

  describe('Mobile Sheet', () => {
    beforeEach(() => {
      // Override to mobile viewport
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn().mockImplementation((query: string) => ({
          matches: query !== '(min-width: 1024px)', // mobile
          media: query,
          onchange: null,
          addListener: vi.fn(),
          removeListener: vi.fn(),
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          dispatchEvent: vi.fn(),
        })),
      });
    });

    it('renders mobile toggle button', () => {
      renderPanel();
      expect(screen.getByTestId('chat-info-mobile-toggle')).toBeInTheDocument();
    });

    it('opens Sheet on mobile toggle click', async () => {
      const user = userEvent.setup();
      const store = setupStore();
      renderPanel();

      await user.click(screen.getByTestId('chat-info-mobile-toggle'));
      expect(store.setMobileOpen).toHaveBeenCalledWith(true);
    });

    it('shows content in Sheet when mobile open', () => {
      setupStore({ isMobileOpen: true });
      renderPanel();

      const sheet = screen.getByTestId('sheet');
      expect(sheet).toHaveAttribute('data-open', 'true');
    });
  });
});
