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
 * Post-#1856 DEC-5: the top-left badge stack contains ONLY EntityBadge.
 * StatusBadge was moved to the new CardFooter (border-top + StatusDot + label).
 * The legacy overlap-fix regression test is superseded — TagStrip now shifts to
 * a single offset because the stack has at most 1 badge.
 *
 * Original pre-#1856 invariants:
 *   - Stack wraps EntityBadge + StatusBadge → SUPERSEDED (only EntityBadge now)
 *   - TagStrip shifts to top-14 when 2 badges present → SUPERSEDED (always top-9)
 */
describe('GridCard top-left badge stack (post-#1856)', () => {
  it('renders EntityBadge alone in the stack (StatusBadge moved to footer)', () => {
    const { container } = render(
      <GridCard entity="game" title="Catan" status="owned" tags={['Strategy']} />
    );
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    // The stack must use a flex column so badges never overlap.
    expect(stack?.className).toMatch(/flex/);
    expect(stack?.className).toMatch(/flex-col/);
    // Only EntityBadge (containing the "Game" label) — no StatusBadge ("Posseduto").
    expect(stack?.children).toHaveLength(1);
    expect(stack?.textContent).toMatch(/Game/i);
    expect(stack?.textContent).not.toMatch(/Posseduto/i);
  });

  it('renders EntityBadge in the stack also when status is omitted', () => {
    const { container } = render(<GridCard entity="game" title="Catan" />);
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    expect(stack?.children).toHaveLength(1);
  });

  it('keeps TagStrip at top-9 since the stack now has at most 1 badge', () => {
    const { container } = render(
      <GridCard entity="game" title="Catan" status="owned" tags={['A', 'B']} />
    );
    const tagStrip = container.querySelector('.absolute.top-9');
    expect(tagStrip).not.toBeNull();
    expect(tagStrip?.textContent).toContain('A');
    // top-14 must NOT be present (no more dual-badge stack).
    expect(container.querySelector('.absolute.top-14')).toBeNull();
  });
});

/**
 * #1856 SP4 mockup conformance integration tests.
 *
 * Verifies GridCard orchestrates the 5 mockup parts:
 *   1. AccentBorder horizontal-top
 *   2. Cover dual-mode (emoji-band when no imageUrl)
 *   3. EntityBadge glass (top-left, alone in stack)
 *   4. MenuPlaceholder (top-right, hover-visible)
 *   5. CardFooter (border-top + StatusDot + uppercase mono badge)
 */
describe('GridCard SP4 mockup conformance (#1856)', () => {
  it('renders AccentBorder horizontal-top (not vertical-left)', () => {
    const { container } = render(<GridCard entity="game" title="Catan" />);
    // AccentBorder is the first absolute-positioned child with top-0 right-0.
    const accent = container.querySelector('.absolute.top-0.right-0');
    expect(accent).not.toBeNull();
  });

  it('renders MenuPlaceholder button with aria-label="Azioni"', () => {
    render(<GridCard entity="game" title="Catan" />);
    expect(screen.getByRole('button', { name: 'Azioni' })).toBeInTheDocument();
  });

  it('renders CardFooter with status when status prop is set', () => {
    const { container } = render(<GridCard entity="game" title="Catan" status="owned" />);
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer).not.toBeNull();
    expect(footer?.textContent?.toLowerCase()).toContain('owned');
  });

  it('renders CardFooter with badge taking precedence over status', () => {
    const { container } = render(
      <GridCard entity="kb" title="Catan KB" status="indexed" badge="processing" />
    );
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer?.textContent?.toLowerCase()).toContain('processing');
  });

  it('renders emoji-band Cover when imageUrl is absent (game entity gets entityIcon[game])', () => {
    const { container } = render(<GridCard entity="game" title="Catan" />);
    const band = container.querySelector('[data-slot="cover-emoji-band"]');
    expect(band).not.toBeNull();
  });

  it('uses coverEmoji prop in the emoji-band when provided', () => {
    const { container } = render(
      <GridCard entity="session" title="Game night #1" coverEmoji="🎯" />
    );
    // The emoji-band data-slot contains the coverEmoji span.
    const band = container.querySelector('[data-slot="cover-emoji-band"]');
    expect(band).not.toBeNull();
    expect(band?.textContent).toContain('🎯');
  });

  it('does NOT render StatusBadge inside the top-left badge-stack (moved to footer per DEC-5)', () => {
    const { container } = render(<GridCard entity="game" title="Catan" status="owned" />);
    const stack = container.querySelector('[data-slot="badge-stack"]');
    expect(stack).not.toBeNull();
    // Stack should contain ONLY the EntityBadge.
    expect(stack?.children).toHaveLength(1);
    const entityBadge = stack?.querySelector('[data-slot="meeple-card-entity-badge"]');
    expect(entityBadge).not.toBeNull();
  });

  it('does NOT render inline badge in the header (moved to footer per DEC-5)', () => {
    const { container } = render(<GridCard entity="game" title="Catan" badge="OWNED" />);
    // The legacy inline-badge slot must be gone.
    const inlineBadge = container.querySelector('[data-slot="badge"]');
    expect(inlineBadge).toBeNull();
    // The footer is now the source of truth for the badge text.
    const footer = container.querySelector('[data-slot="footer-badge"]');
    expect(footer?.textContent?.toLowerCase()).toContain('owned');
  });

  it('does NOT render MenuPlaceholder when QuickActions are active (positional collision avoidance)', () => {
    render(
      <GridCard
        entity="game"
        title="Catan"
        showQuickActions
        actions={[{ icon: '+', label: 'Add', onClick: () => {} }]}
      />
    );
    // MenuPlaceholder is suppressed because QuickActions occupies the same top-right slot.
    expect(screen.queryByRole('button', { name: 'Azioni' })).toBeNull();
  });

  it('DOES render MenuPlaceholder when showQuickActions is true but actions array is empty', () => {
    render(<GridCard entity="game" title="Catan" showQuickActions actions={[]} />);
    // Empty actions → QuickActions does not render → MenuPlaceholder remains.
    expect(screen.getByRole('button', { name: 'Azioni' })).toBeInTheDocument();
  });
});

/**
 * #1842 headingLevel prop — dynamic title heading tag.
 */
describe('GridCard headingLevel prop (#1842)', () => {
  it('renders <h3> by default (backwards compat)', () => {
    render(<GridCard entity="game" title="Catan default" />);
    const el = screen.getByText('Catan default');
    expect(el.tagName).toBe('H3');
  });

  it('renders <h2> when headingLevel={2}', () => {
    render(<GridCard entity="game" title="Catan h2" headingLevel={2} />);
    const el = screen.getByText('Catan h2');
    expect(el.tagName).toBe('H2');
  });

  it('renders <h4> when headingLevel={4}', () => {
    render(<GridCard entity="game" title="Catan h4" headingLevel={4} />);
    const el = screen.getByText('Catan h4');
    expect(el.tagName).toBe('H4');
  });

  it('preserves title className across all heading levels (visual stability)', () => {
    const { rerender } = render(<GridCard entity="game" title="X" />);
    const h3Class = screen.getByText('X').className;
    rerender(<GridCard entity="game" title="X" headingLevel={2} />);
    expect(screen.getByText('X').className).toBe(h3Class);
  });
});
