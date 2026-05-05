import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GridCard } from '../GridCard';

describe('GridCard connections path', () => {
  it('S2: connections=[] renders no chip strip, no warn', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    render(<GridCard entity="game" title="X" connections={[]} />);
    expect(screen.queryByTestId('connection-chip-strip')).toBeNull();
    expect(warn).not.toHaveBeenCalled();
  });

  it('renders ConnectionChipStrip when connections has items', () => {
    render(
      <GridCard entity="game" title="X" connections={[{ entityType: 'session', count: 3 }]} />
    );
    expect(screen.getByTestId('connection-chip-strip')).toBeInTheDocument();
  });
});

/**
 * Regression guard for the badge stack layout fix (Option A).
 *
 * Before the fix, EntityBadge / StatusBadge / TagStrip each rendered with
 * hardcoded absolute offsets (top-2 / top-7 / top-8) that overlapped when
 * present together. The fix introduces a single absolute flex container
 * `[data-slot="badge-stack"]` wrapping EntityBadge + StatusBadge, and shifts
 * TagStrip down (top-9 with 1 badge, top-14 with 2 badges).
 *
 * These tests assert the structural contract — they don't pin specific
 * pixel values, but they ensure the badge-stack container wraps the badges
 * and that TagStrip's vertical class adapts to the badge count.
 */
describe('GridCard top-left badge stack (overlap fix)', () => {
  it('wraps EntityBadge + StatusBadge inside a single badge-stack container', () => {
    const { container } = render(
      <GridCard entity="game" title="Catan" status="owned" tags={['Strategy']} />
    );
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    // The stack must use a flex column so badges never overlap.
    expect(stack?.className).toMatch(/flex/);
    expect(stack?.className).toMatch(/flex-col/);
    // Both badges must be inside the stack (text rendered as children).
    expect(stack?.textContent).toMatch(/Game/i);
    expect(stack?.textContent).toMatch(/Posseduto/i);
  });

  it('renders only EntityBadge in stack when status is omitted', () => {
    const { container } = render(<GridCard entity="game" title="Catan" />);
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    expect(stack?.children).toHaveLength(1);
  });

  it('shifts TagStrip to top-14 when both EntityBadge and StatusBadge are present', () => {
    const { container } = render(
      <GridCard entity="game" title="Catan" status="owned" tags={['A', 'B']} />
    );
    // TagStrip is the absolute element with `top-14` class.
    const tagStrip = container.querySelector('.absolute.top-14');
    expect(tagStrip).not.toBeNull();
    expect(tagStrip?.textContent).toContain('A');
  });

  it('positions TagStrip at top-9 when only EntityBadge is present (no status)', () => {
    const { container } = render(<GridCard entity="game" title="Catan" tags={['A']} />);
    const tagStrip = container.querySelector('.absolute.top-9');
    expect(tagStrip).not.toBeNull();
    // top-14 must NOT be present in this case.
    expect(container.querySelector('.absolute.top-14')).toBeNull();
  });
});
