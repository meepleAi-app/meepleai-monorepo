/**
 * VectorCollectionCard Tests
 * Issue #4861: MeepleCard design system for /admin/knowledge-base/vectors
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  VectorCollectionCard,
  type VectorCollectionDto,
} from '../vector-collection-card';

const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return <div data-testid={props['data-testid'] as string}>MeepleCard</div>;
  },
}));

const mockCollection: VectorCollectionDto = {
  name: 'game_rules',
  vectorCount: 12500,
  dimensions: 384,
  storage: '50 MB',
  status: { health: 'healthy' },
};

const mockDegradedCollection: VectorCollectionDto = {
  name: 'faq_embeddings',
  vectorCount: 340,
  dimensions: 768,
  storage: '1 MB',
  status: { health: 'degraded', optimizerStatus: 'indexing' },
};

describe('VectorCollectionCard', () => {
  beforeEach(() => {
    mockMeepleCard.mockClear();
  });

  it('renders with entity="document" and variant="grid"', () => {
    render(<VectorCollectionCard collection={mockCollection} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        entity: 'document',
        variant: 'grid',
      })
    );
  });

  it('passes collection name as title', () => {
    render(<VectorCollectionCard collection={mockCollection} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'game_rules' })
    );
  });

  it('passes formatted subtitle with vector count and dimensions', () => {
    render(<VectorCollectionCard collection={mockCollection} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const subtitle = call?.subtitle as string;

    // toLocaleString() output is locale-dependent; check structure instead
    expect(subtitle).toContain('12');
    expect(subtitle).toContain('500');
    expect(subtitle).toContain('vettori');
    expect(subtitle).toContain('384D');
  });

  it('passes health status as badge', () => {
    render(<VectorCollectionCard collection={mockCollection} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ badge: 'Healthy' })
    );
  });

  it('passes degraded health as badge', () => {
    render(<VectorCollectionCard collection={mockDegradedCollection} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ badge: 'Degraded' })
    );
  });

  it('passes metadata with vectors, dimensions, and size', () => {
    render(<VectorCollectionCard collection={mockCollection} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata).toHaveLength(3);
    // toLocaleString() is locale-dependent; check key parts
    expect(metadata[0].label).toContain('vectors');
    expect(metadata[0].label).toMatch(/12[.,]500/);
    expect(metadata[1].label).toBe('384D');
    expect(metadata[2].label).toBe('50 MB');
  });

  it('passes storage string directly to metadata', () => {
    const customStorage = { ...mockCollection, storage: '3.2 GB' };
    render(<VectorCollectionCard collection={customStorage} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata[2].label).toBe('3.2 GB');
  });

  it('includes quickActions when callbacks provided', () => {
    const onOptimize = vi.fn();
    const onReindex = vi.fn();
    const onDelete = vi.fn();

    render(
      <VectorCollectionCard
        collection={mockCollection}
        onOptimize={onOptimize}
        onReindex={onReindex}
        onDelete={onDelete}
      />
    );

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void; destructive?: boolean }>;

    expect(actions).toHaveLength(3);
    expect(actions[0].label).toBe('Optimize');
    expect(actions[1].label).toBe('Reindex');
    expect(actions[2].label).toBe('Delete');
    expect(actions[2].destructive).toBe(true);
  });

  it('calls onOptimize with collection name', () => {
    const onOptimize = vi.fn();
    render(<VectorCollectionCard collection={mockCollection} onOptimize={onOptimize} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void }>;
    actions[0].onClick();

    expect(onOptimize).toHaveBeenCalledWith('game_rules');
  });

  it('calls onDelete with collection name', () => {
    const onDelete = vi.fn();
    render(<VectorCollectionCard collection={mockCollection} onDelete={onDelete} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void }>;
    actions[0].onClick();

    expect(onDelete).toHaveBeenCalledWith('game_rules');
  });

  it('passes undefined quickActions when no callbacks', () => {
    render(<VectorCollectionCard collection={mockCollection} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ quickActions: undefined })
    );
  });

  it('sets correct data-testid', () => {
    render(<VectorCollectionCard collection={mockCollection} />);

    expect(screen.getByTestId('vector-collection-card-game_rules')).toBeInTheDocument();
  });

  it('handles zero vectors', () => {
    const emptyCollection = { ...mockCollection, vectorCount: 0, storage: '0 B' };
    render(<VectorCollectionCard collection={emptyCollection} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata[0].label).toBe('0 vectors');
    expect(metadata[2].label).toBe('0 B');
  });
});
