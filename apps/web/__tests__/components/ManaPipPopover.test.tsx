/**
 * ManaPipPopover Component Tests
 *
 * Tests for the ManaPipPopover component:
 * - Renders trigger (children)
 * - Shows item list when open
 * - Each item is a link with icon + label
 * - Shows create button when onCreate is provided
 * - Calls onOpenChange(false) after item click
 * - Calls onCreate and onOpenChange(false) on create click
 * - Hides create button when onCreate is not provided
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { ManaPipPopover } from '@/components/ui/data-display/meeple-card/parts/ManaPipPopover';
import type { ManaPipItem } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

const items: ManaPipItem[] = [
  { id: '1', label: 'Catan Rulebook', href: '/games/1/kb' },
  { id: '2', label: 'Catan FAQ', href: '/games/1/faq' },
];

function renderPopover(overrides: Partial<Parameters<typeof ManaPipPopover>[0]> = {}) {
  const onOpenChange = vi.fn();
  const onCreate = vi.fn();

  const props = {
    open: true,
    onOpenChange,
    items,
    onCreate,
    createLabel: 'Add Document',
    entityType: 'kb' as const,
    children: <button>Open</button>,
    ...overrides,
  };

  render(<ManaPipPopover {...props} />);

  return { onOpenChange, onCreate };
}

describe('ManaPipPopover', () => {
  it('renders the trigger children', () => {
    renderPopover();
    expect(screen.getByRole('button', { name: 'Open' })).toBeTruthy();
  });

  it('shows item labels when open=true', () => {
    renderPopover();
    expect(screen.getByText('Catan Rulebook')).toBeTruthy();
    expect(screen.getByText('Catan FAQ')).toBeTruthy();
  });

  it('renders items as links with correct href', () => {
    renderPopover();
    const links = screen.getAllByRole('link');
    const hrefs = links.map(l => l.getAttribute('href'));
    expect(hrefs).toContain('/games/1/kb');
    expect(hrefs).toContain('/games/1/faq');
  });

  it('renders entity icon for each item', () => {
    renderPopover({ entityType: 'kb' });
    // entityIcon['kb'] = '📚'
    const icons = screen.getAllByText('📚');
    expect(icons.length).toBeGreaterThanOrEqual(items.length);
  });

  it('shows create button with custom label when onCreate provided', () => {
    renderPopover();
    expect(screen.getByRole('button', { name: /Add Document/i })).toBeTruthy();
  });

  it('does not show create button when onCreate is not provided', () => {
    renderPopover({ onCreate: undefined });
    expect(screen.queryByRole('button', { name: /Add Document/i })).toBeNull();
  });

  it('calls onOpenChange(false) when an item link is clicked', () => {
    const { onOpenChange } = renderPopover();
    const firstLink = screen.getByText('Catan Rulebook').closest('a')!;
    fireEvent.click(firstLink);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('calls onCreate and onOpenChange(false) when create button is clicked', () => {
    const { onCreate, onOpenChange } = renderPopover();
    const createBtn = screen.getByRole('button', { name: /Add Document/i });
    fireEvent.click(createBtn);
    expect(onCreate).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('renders empty state gracefully when items is empty', () => {
    renderPopover({ items: [] });
    // No links rendered
    expect(screen.queryAllByRole('link')).toHaveLength(0);
    // Create button still present
    expect(screen.getByRole('button', { name: /Add Document/i })).toBeTruthy();
  });

  it('uses default createLabel when not provided', () => {
    renderPopover({ createLabel: undefined });
    expect(screen.getByRole('button', { name: /Create/i })).toBeTruthy();
  });

  it('does not show content when open=false', () => {
    renderPopover({ open: false });
    expect(screen.queryByText('Catan Rulebook')).toBeNull();
  });
});
