/**
 * VectorCollectionCard Tests
 * Issue #4861: MeepleCard design system for /admin/knowledge-base/vectors
 * Issue #4877: Updated for flat props interface
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { VectorCollectionCard } from '../vector-collection-card';

const mockMeepleCard = vi.fn(() => null);
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    mockMeepleCard(props);
    return <div data-testid={props['data-testid'] as string}>MeepleCard</div>;
  },
}));

const baseProps = {
  name: 'game_rules',
  vectorCount: 12500,
  dimensions: 384,
  storage: '50 MB',
  health: 95,
};

const degradedProps = {
  name: 'faq_embeddings',
  vectorCount: 340,
  dimensions: 768,
  storage: '1 MB',
  health: 75,
};

describe('VectorCollectionCard', () => {
  beforeEach(() => {
    mockMeepleCard.mockClear();
  });

  it('renders with entity="kb" and variant="grid"', () => {
    render(<VectorCollectionCard {...baseProps} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({
        entity: 'kb',
        variant: 'grid',
      })
    );
  });

  it('passes collection name as title', () => {
    render(<VectorCollectionCard {...baseProps} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'game_rules' })
    );
  });

  it('passes formatted subtitle with vector count, dimensions, and storage', () => {
    render(<VectorCollectionCard {...baseProps} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const subtitle = call?.subtitle as string;

    expect(subtitle).toContain('vectors');
    expect(subtitle).toContain('384D');
    expect(subtitle).toContain('50 MB');
  });

  it('passes Healthy badge for health >= 90', () => {
    render(<VectorCollectionCard {...baseProps} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ badge: 'Healthy' })
    );
  });

  it('passes Degraded badge for health 70-89', () => {
    render(<VectorCollectionCard {...degradedProps} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ badge: 'Degraded' })
    );
  });

  it('passes Error badge for health < 70', () => {
    render(<VectorCollectionCard {...baseProps} health={50} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ badge: 'Error' })
    );
  });

  it('passes metadata with vectors and dimensions', () => {
    render(<VectorCollectionCard {...baseProps} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata).toHaveLength(2);
    expect(metadata[0].label).toContain('vectors');
    expect(metadata[1].label).toBe('384D');
  });

  it('includes quickActions when callbacks provided', () => {
    const onReindex = vi.fn();
    const onDelete = vi.fn();

    render(
      <VectorCollectionCard
        {...baseProps}
        onReindex={onReindex}
        onDelete={onDelete}
      />
    );

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void; destructive?: boolean }>;

    expect(actions).toHaveLength(2);
    expect(actions[0].label).toBe('Rebuild Index');
    expect(actions[1].label).toBe('Delete');
    expect(actions[1].destructive).toBe(true);
  });

  it('calls onReindex with collection name', () => {
    const onReindex = vi.fn();
    render(<VectorCollectionCard {...baseProps} onReindex={onReindex} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void }>;
    actions[0].onClick();

    expect(onReindex).toHaveBeenCalledWith('game_rules');
  });

  it('calls onDelete with collection name', () => {
    const onDelete = vi.fn();
    render(<VectorCollectionCard {...baseProps} onDelete={onDelete} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const actions = call?.quickActions as Array<{ label: string; onClick: () => void }>;
    actions[0].onClick();

    expect(onDelete).toHaveBeenCalledWith('game_rules');
  });

  it('passes undefined quickActions when no callbacks', () => {
    render(<VectorCollectionCard {...baseProps} />);

    expect(mockMeepleCard).toHaveBeenCalledWith(
      expect.objectContaining({ quickActions: undefined })
    );
  });

  it('sets correct data-testid', () => {
    render(<VectorCollectionCard {...baseProps} />);

    expect(screen.getByTestId('vector-collection-card-game_rules')).toBeInTheDocument();
  });

  it('handles zero vectors', () => {
    render(<VectorCollectionCard {...baseProps} vectorCount={0} />);

    const call = mockMeepleCard.mock.calls[0]?.[0] as Record<string, unknown> | undefined;
    const metadata = call?.metadata as Array<{ label: string }>;

    expect(metadata[0].label).toBe('0 vectors');
  });
});
