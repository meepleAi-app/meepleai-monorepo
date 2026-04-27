import { fireEvent, render, screen } from '@testing-library/react';
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
