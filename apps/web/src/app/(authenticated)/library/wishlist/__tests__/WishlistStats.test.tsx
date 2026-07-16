/**
 * @vitest-environment jsdom
 */

/**
 * WishlistStats — header "qstat" line for /library/wishlist (Issue #3007,
 * Task B5).
 *
 * Mirrors `MeepleWishlistCard.test.tsx`'s i18n strategy: a real `IntlProvider`
 * wired to the flattened `it.json` catalog, so assertions check the actual
 * rendered (interpolated) copy rather than raw message ids.
 */

import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { describe, expect, it } from 'vitest';

import { flattenMessages } from '@/locales';
import itMessages from '@/locales/it.json';

import { WishlistStats } from '../_components/WishlistStats';

import type { Priority } from '../_lib/wishlist-filters';
import type { ReactElement } from 'react';

const MESSAGES = flattenMessages(itMessages as unknown as Record<string, unknown>);
const STATS_I18N = itMessages.pages.library.wishlist.stats;

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

interface WishlistStatsShape {
  total: number;
  highCount: number;
  totalSpend: number;
  priorityCounts: Record<Priority, number>;
}

function buildStats(overrides: Partial<WishlistStatsShape> = {}): WishlistStatsShape {
  return {
    total: 12,
    highCount: 5,
    totalSpend: 350,
    priorityCounts: { high: 5, medium: 4, low: 3 },
    ...overrides,
  };
}

describe('WishlistStats', () => {
  it('renders the total games count', () => {
    renderWithIntl(<WishlistStats stats={buildStats({ total: 12 })} />);
    expect(screen.getByText(STATS_I18N.games.replace('{count}', '12'))).toBeInTheDocument();
  });

  it('renders the high-priority count', () => {
    renderWithIntl(<WishlistStats stats={buildStats({ highCount: 5 })} />);
    expect(screen.getByText(STATS_I18N.highPriority.replace('{count}', '5'))).toBeInTheDocument();
  });

  it('renders the estimated spend formatted as EUR currency', () => {
    renderWithIntl(<WishlistStats stats={buildStats({ totalSpend: 350 })} />);
    // NBSP/narrow-NBSP between amount and currency symbol can differ subtly
    // by ICU data source — assert on the container's normalized text content
    // instead of an exact string built independently via `Intl.NumberFormat`.
    expect(screen.getByTestId('wishlist-stats').textContent).toMatch(/spesa stimata.*350,00.*€/);
  });

  it('renders zero values without crashing', () => {
    renderWithIntl(<WishlistStats stats={buildStats({ total: 0, highCount: 0, totalSpend: 0 })} />);
    expect(screen.getByText(STATS_I18N.games.replace('{count}', '0'))).toBeInTheDocument();
  });
});
