import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { FAQ_CATEGORIES, type CategoryId } from '@/lib/faq/data';

import { CategoryTabs } from './category-tabs';

const RESOLVE_LABEL_MAP: Record<CategoryId, string> = {
  all: 'All',
  account: 'Account',
  games: 'Games',
  ai: 'AI',
  privacy: 'Privacy',
  billing: 'Billing',
};

const COUNTS: Record<CategoryId, number> = {
  all: 14,
  account: 3,
  games: 3,
  ai: 3,
  privacy: 3,
  billing: 2,
};

describe('CategoryTabs', () => {
  it('renders all 6 categories as tabs', () => {
    render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="all"
        onChange={() => {}}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
      />
    );
    expect(screen.getAllByRole('tab')).toHaveLength(6);
  });

  it('renders a tablist with provided ariaLabel', () => {
    render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="all"
        onChange={() => {}}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
        ariaLabel="FAQ categorie"
      />
    );
    expect(screen.getByRole('tablist', { name: 'FAQ categorie' })).toBeInTheDocument();
  });

  it('marks active tab with aria-selected=true', () => {
    render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="ai"
        onChange={() => {}}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
      />
    );
    const aiTab = screen.getByRole('tab', { name: /AI/ });
    expect(aiTab).toHaveAttribute('aria-selected', 'true');
  });

  it('inactive tabs have aria-selected=false and tabIndex=-1', () => {
    render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="ai"
        onChange={() => {}}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
      />
    );
    const allTab = screen.getByRole('tab', { name: /All/ });
    expect(allTab).toHaveAttribute('aria-selected', 'false');
    expect(allTab).toHaveAttribute('tabIndex', '-1');
  });

  it('fires onChange with the clicked category id', () => {
    const onChange = vi.fn();
    render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="all"
        onChange={onChange}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
      />
    );
    fireEvent.click(screen.getByRole('tab', { name: /Privacy/ }));
    expect(onChange).toHaveBeenCalledWith('privacy');
  });

  it('renders count badges for each category', () => {
    render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="all"
        onChange={() => {}}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
      />
    );
    expect(screen.getByText('14')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('falls back to 0 when count missing for a category', () => {
    const partialCounts = { all: 14 } as Record<CategoryId, number>;
    render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="all"
        onChange={() => {}}
        counts={partialCounts}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
      />
    );
    // 5 categories without counts → 5 zeros plus the explicit 14.
    const zeros = screen.getAllByText('0');
    expect(zeros.length).toBeGreaterThanOrEqual(5);
  });

  it('applies sticky classes when sticky=true (default)', () => {
    const { container } = render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="all"
        onChange={() => {}}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
      />
    );
    expect(container.querySelector('[data-slot="category-tabs"]')?.className).toContain('sticky');
  });

  it('omits sticky classes when sticky=false', () => {
    const { container } = render(
      <CategoryTabs
        categories={FAQ_CATEGORIES}
        active="all"
        onChange={() => {}}
        counts={COUNTS}
        resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
        sticky={false}
      />
    );
    expect(container.querySelector('[data-slot="category-tabs"]')?.className).not.toContain(
      'sticky'
    );
  });
});

/**
 * Keyboard navigation — closes Issue #588 (WAI-ARIA tablist Arrow-key).
 *
 * Wave A.4 absorbed #588 in `shared-game-detail/tabs.tsx` but missed the
 * original CategoryTabs site. This block mirrors the contract from
 * `tabs.test.tsx` (Wave A.4 §3.4):
 *   - ArrowLeft / ArrowRight wrap (last↔first)
 *   - Home / End jump
 *   - Activation is automatic (focus + onChange same tick)
 *   - Other keys (ArrowUp, character keys) are no-ops
 *
 * Pattern: `ControlledCategoryTabs` wraps the component in stateful host so
 * onChange actually flips the active tab → roving tabindex updates → next
 * keyDown lands on the new active tab (mirrors real page-client usage).
 */
function ControlledCategoryTabs({
  initial = 'all',
  onChangeSpy,
}: {
  readonly initial?: CategoryId;
  readonly onChangeSpy?: (id: CategoryId) => void;
}) {
  const [active, setActive] = useState<CategoryId>(initial);
  return (
    <CategoryTabs
      categories={FAQ_CATEGORIES}
      active={active}
      onChange={id => {
        setActive(id);
        onChangeSpy?.(id);
      }}
      counts={COUNTS}
      resolveLabel={cat => RESOLVE_LABEL_MAP[cat.id]}
    />
  );
}

describe('CategoryTabs — keyboard navigation (closes #588)', () => {
  it('ArrowRight advances to next category and wraps to first', () => {
    const spy = vi.fn();
    render(<ControlledCategoryTabs initial="all" onChangeSpy={spy} />);
    fireEvent.keyDown(screen.getByRole('tab', { name: /All/ }), { key: 'ArrowRight' });
    expect(spy).toHaveBeenLastCalledWith('account');

    spy.mockClear();
    render(<ControlledCategoryTabs initial="billing" onChangeSpy={spy} />);
    const billingTabs = screen.getAllByRole('tab', { name: /Billing/ });
    fireEvent.keyDown(billingTabs[billingTabs.length - 1], { key: 'ArrowRight' });
    expect(spy).toHaveBeenLastCalledWith('all');
  });

  it('ArrowLeft retreats to previous category and wraps to last', () => {
    const spy = vi.fn();
    render(<ControlledCategoryTabs initial="account" onChangeSpy={spy} />);
    fireEvent.keyDown(screen.getByRole('tab', { name: /Account/ }), { key: 'ArrowLeft' });
    expect(spy).toHaveBeenLastCalledWith('all');

    spy.mockClear();
    render(<ControlledCategoryTabs initial="all" onChangeSpy={spy} />);
    const allTabs = screen.getAllByRole('tab', { name: /All/ });
    fireEvent.keyDown(allTabs[allTabs.length - 1], { key: 'ArrowLeft' });
    expect(spy).toHaveBeenLastCalledWith('billing');
  });

  it('Home jumps to first category', () => {
    const spy = vi.fn();
    render(<ControlledCategoryTabs initial="privacy" onChangeSpy={spy} />);
    fireEvent.keyDown(screen.getByRole('tab', { name: /Privacy/ }), { key: 'Home' });
    expect(spy).toHaveBeenLastCalledWith('all');
  });

  it('End jumps to last category', () => {
    const spy = vi.fn();
    render(<ControlledCategoryTabs initial="all" onChangeSpy={spy} />);
    fireEvent.keyDown(screen.getByRole('tab', { name: /All/ }), { key: 'End' });
    expect(spy).toHaveBeenLastCalledWith('billing');
  });

  it('ignores ArrowUp / ArrowDown / character keys (horizontal tablist)', () => {
    const spy = vi.fn();
    render(<ControlledCategoryTabs onChangeSpy={spy} />);
    const all = screen.getByRole('tab', { name: /All/ });
    fireEvent.keyDown(all, { key: 'ArrowUp' });
    fireEvent.keyDown(all, { key: 'ArrowDown' });
    fireEvent.keyDown(all, { key: 'a' });
    expect(spy).not.toHaveBeenCalled();
  });

  it('roving tabindex updates after Arrow navigation', () => {
    render(<ControlledCategoryTabs initial="all" />);
    expect(screen.getByRole('tab', { name: /All/ })).toHaveAttribute('tabindex', '0');
    fireEvent.keyDown(screen.getByRole('tab', { name: /All/ }), { key: 'ArrowRight' });
    // After re-render: Account is active → tabindex=0, All → -1
    expect(screen.getByRole('tab', { name: /Account/ })).toHaveAttribute('tabindex', '0');
    expect(screen.getByRole('tab', { name: /All/ })).toHaveAttribute('tabindex', '-1');
  });

  it('preventDefault is implicit — onChange fires synchronously with key event', () => {
    // Asserts the activation contract: focus = activation (no separate Enter
    // press needed). Implementation MUST call onChange in the same handler tick.
    const spy = vi.fn();
    render(<ControlledCategoryTabs initial="all" onChangeSpy={spy} />);
    fireEvent.keyDown(screen.getByRole('tab', { name: /All/ }), { key: 'ArrowRight' });
    expect(spy).toHaveBeenCalledTimes(1);
    expect(spy).toHaveBeenCalledWith('account');
  });
});
