import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KbTree } from '../KbTree';
import type { GameKbStatusItem } from '@/lib/api/schemas/admin-knowledge-base.schemas';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';
import type { SelectedDocMeta } from '../explorer-types';

// Mock the per-game docs hook so KbTree's expanded-node sub-component can render
// without TanStack Query plumbing.
const mockUseKbGameDocuments = vi.fn();
vi.mock('@/hooks/queries/useGameDocuments', () => ({
  useKbGameDocuments: (gameId: string, enabled: boolean) => mockUseKbGameDocuments(gameId, enabled),
}));

const game1: GameKbStatusItem = {
  gameId: 'g-1',
  gameName: 'Wingspan',
  kbStatus: 'complete',
  documentCount: 6,
  totalChunks: 412,
  latestIndexedAt: '2024-12-08T14:22:00Z',
  hasAutoBackup: true,
};
const game2: GameKbStatusItem = {
  gameId: 'g-2',
  gameName: 'Brass: Birmingham',
  kbStatus: 'partial',
  documentCount: 3,
  totalChunks: 187,
  latestIndexedAt: null,
  hasAutoBackup: false,
};

const doc1: GameDocument = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Wingspan-Oceania-EN.pdf',
  status: 'indexed',
  pageCount: 42,
  createdAt: '2024-12-08T14:22:00Z',
  category: 'Rulebook',
  versionLabel: null,
};
const doc2: GameDocument = {
  id: '22222222-2222-2222-2222-222222222222',
  title: 'Wingspan-FAQ.txt',
  status: 'processing',
  pageCount: 4,
  createdAt: '2024-12-08T14:22:00Z',
  category: 'Faq',
  versionLabel: null,
};
const doc3: GameDocument = {
  id: '33333333-3333-3333-3333-333333333333',
  title: 'brass-faq-thread.txt',
  status: 'failed',
  pageCount: 0,
  createdAt: '2024-12-08T14:22:00Z',
  category: 'Faq',
  versionLabel: null,
};

function setHook(gameId: string, docs: GameDocument[], isLoading = false) {
  mockUseKbGameDocuments.mockImplementation((gid: string) => {
    if (gid === gameId) return { data: docs, isLoading, error: null };
    return { data: [], isLoading: false, error: null };
  });
}

describe('KbTree', () => {
  beforeEach(() => mockUseKbGameDocuments.mockReset());

  const baseProps = {
    games: [game1, game2],
    expandedGameIds: new Set<string>(),
    selectedDocId: null,
    filter: '',
    onToggleGame: vi.fn(),
    onSelectDoc: vi.fn(),
    onFilterChange: vi.fn(),
  };

  it('renders one treeitem per game with name and totalChunks', () => {
    render(<KbTree {...baseProps} />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
    expect(screen.getByText('412')).toBeInTheDocument();
    expect(screen.getByText('187')).toBeInTheDocument();
  });

  it('collapsed game nodes do NOT call useKbGameDocuments', () => {
    render(<KbTree {...baseProps} />);
    expect(mockUseKbGameDocuments).not.toHaveBeenCalled();
  });

  it('aria-expanded reflects expansion state', () => {
    const { rerender } = render(<KbTree {...baseProps} />);
    const wingspanNode = screen.getByRole('treeitem', { name: /Wingspan/ });
    expect(wingspanNode).toHaveAttribute('aria-expanded', 'false');

    rerender(<KbTree {...baseProps} expandedGameIds={new Set(['g-1'])} />);
    expect(screen.getByRole('treeitem', { name: /Wingspan/ })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
  });

  it('clicking a game node calls onToggleGame', () => {
    const onToggleGame = vi.fn();
    render(<KbTree {...baseProps} onToggleGame={onToggleGame} />);
    fireEvent.click(screen.getByRole('treeitem', { name: /Wingspan/ }));
    expect(onToggleGame).toHaveBeenCalledWith('g-1');
  });

  it('renders doc leaves for expanded games with status class and pageCount badge', () => {
    setHook('g-1', [doc1, doc2]);
    render(<KbTree {...baseProps} expandedGameIds={new Set(['g-1'])} />);

    const leaf1 = screen.getByText('Wingspan-Oceania-EN.pdf').closest('[role="treeitem"]');
    expect(leaf1).toBeTruthy();
    expect(leaf1).toHaveAttribute('data-status', 'indexed');
    expect(leaf1).toHaveTextContent('42p');

    const leaf2 = screen.getByText('Wingspan-FAQ.txt').closest('[role="treeitem"]');
    expect(leaf2).toHaveAttribute('data-status', 'processing');
  });

  it('marks the selected doc with aria-selected', () => {
    setHook('g-1', [doc1, doc2]);
    render(<KbTree {...baseProps} expandedGameIds={new Set(['g-1'])} selectedDocId={doc1.id} />);
    const leaf = screen.getByText('Wingspan-Oceania-EN.pdf').closest('[role="treeitem"]');
    expect(leaf).toHaveAttribute('aria-selected', 'true');
  });

  it('clicking a doc leaf calls onSelectDoc with { id, title, gameId }', () => {
    setHook('g-1', [doc1]);
    const onSelectDoc = vi.fn();
    render(<KbTree {...baseProps} expandedGameIds={new Set(['g-1'])} onSelectDoc={onSelectDoc} />);
    fireEvent.click(screen.getByText('Wingspan-Oceania-EN.pdf'));
    const expected: SelectedDocMeta = { id: doc1.id, title: doc1.title, gameId: game1.gameId };
    expect(onSelectDoc).toHaveBeenCalledWith(expected);
  });

  it('filter narrows games by name (case-insensitive)', () => {
    render(<KbTree {...baseProps} filter="brass" />);
    expect(screen.queryByText('Wingspan')).not.toBeInTheDocument();
    expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
  });

  it('filter hides non-matching games even when they are expanded', () => {
    setHook('g-2', [doc3]);
    render(<KbTree {...baseProps} filter="wingspan" expandedGameIds={new Set(['g-2'])} />);
    // Brass game name does NOT include "wingspan" → hidden even though expanded.
    expect(screen.queryByText('Brass: Birmingham')).not.toBeInTheDocument();
    expect(screen.queryByText('brass-faq-thread.txt')).not.toBeInTheDocument();
  });

  it('filter narrows doc leaves by title within an expanded matching game', () => {
    setHook('g-2', [doc3]);
    render(<KbTree {...baseProps} filter="brass" expandedGameIds={new Set(['g-2'])} />);
    // Game name includes "brass" → visible; doc title includes "brass" → visible.
    expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
    expect(screen.getByText('brass-faq-thread.txt')).toBeInTheDocument();
  });

  it('search input emits onFilterChange', () => {
    const onFilterChange = vi.fn();
    render(<KbTree {...baseProps} onFilterChange={onFilterChange} />);
    fireEvent.change(screen.getByPlaceholderText(/filtra tree/i), { target: { value: 'win' } });
    expect(onFilterChange).toHaveBeenCalledWith('win');
  });

  it('shows loading placeholder while docs for an expanded game are loading', () => {
    setHook('g-1', [], true);
    render(<KbTree {...baseProps} expandedGameIds={new Set(['g-1'])} />);
    expect(screen.getByTestId('kb-tree-docs-loading-g-1')).toBeInTheDocument();
  });
});
