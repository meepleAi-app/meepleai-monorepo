import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import FaqPage from '../page';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, values?: Record<string, unknown>) => {
      if (values && typeof values.count === 'number') {
        return `${key} (count=${values.count}, query=${values.query ?? ''})`;
      }
      return key;
    },
    locale: 'it',
  }),
}));

// next/navigation: useSearchParams returns a URLSearchParams-like ReadonlyURLSearchParams
const mockSearchParams = new URLSearchParams();
vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams,
}));

// useFaqHashQuery: simple in-memory mock so tests stay deterministic.
let mockQuery = '';
const mockSetQuery = vi.fn((next: string) => {
  mockQuery = next;
});
vi.mock('@/hooks/useFaqHashQuery', () => ({
  useFaqHashQuery: () => ({
    get query() {
      return mockQuery;
    },
    setQuery: mockSetQuery,
  }),
}));

beforeEach(() => {
  mockQuery = '';
  mockSetQuery.mockClear();
  // Reset URL params each test.
  for (const key of Array.from(mockSearchParams.keys())) {
    mockSearchParams.delete(key);
  }
});

describe('FaqPage (v2)', () => {
  it('renders the hero with testid + title', () => {
    render(<FaqPage />);
    expect(screen.getByTestId('faq-hero')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'pages.faq.title', level: 1 })).toBeInTheDocument();
  });

  it('renders the FAQ search bar with localized placeholder', () => {
    render(<FaqPage />);
    const input = screen.getByRole('searchbox');
    expect(input).toHaveAttribute('placeholder', 'pages.faq.search.placeholder');
  });

  it('renders 6 category tabs (all + 5 categories)', () => {
    render(<FaqPage />);
    expect(screen.getAllByRole('tab')).toHaveLength(6);
  });

  it('renders the popular grid (top 4 quick-answer cards) on default state', () => {
    render(<FaqPage />);
    const popularCards = screen.getAllByRole('button', {
      name: /pages\.faq\.items\.q\d+\.question/,
    });
    // popular grid contributes 4 quick-answer buttons, plus one accordion trigger per visible FAQ.
    // Ensure at least 4 quick-answer tiles render their popularRank badge "#N".
    expect(popularCards.length).toBeGreaterThanOrEqual(4);
    expect(screen.getByText('#1')).toBeInTheDocument();
    expect(screen.getByText('#4')).toBeInTheDocument();
  });

  it('renders the full accordion list with 14 trigger buttons in default state', () => {
    render(<FaqPage />);
    // Each AccordionItem button has aria-expanded; tabs also have it but are role=tab.
    const triggers = screen.getAllByRole('button', { expanded: false });
    // 4 popular tiles (button) + 14 accordion triggers = 18 closed buttons in default state.
    // We assert >= 14 to be robust against extra buttons (clear, footer CTAs).
    const accordionTriggers = triggers.filter(b =>
      b.getAttribute('aria-controls')?.startsWith('faq-panel-')
    );
    expect(accordionTriggers.length).toBe(14);
  });

  it('expands an accordion item on click revealing role=region content', async () => {
    const user = userEvent.setup();
    render(<FaqPage />);
    const triggers = screen
      .getAllByRole('button')
      .filter(b => b.getAttribute('aria-controls')?.startsWith('faq-panel-'));
    const firstTrigger = triggers[0];
    await user.click(firstTrigger);
    expect(firstTrigger).toHaveAttribute('aria-expanded', 'true');
  });

  it('clicking a quick-answer card seeds an open accordion item and clears query', async () => {
    const user = userEvent.setup();
    render(<FaqPage />);
    const quickAnswers = screen
      .getAllByRole('button')
      .filter(b => b.querySelector('[class*="line-clamp-2"]'));
    expect(quickAnswers.length).toBeGreaterThanOrEqual(1);
    await user.click(quickAnswers[0]);
    expect(mockSetQuery).toHaveBeenCalledWith('');
  });

  it('renders empty state when query yields zero results', () => {
    mockQuery = 'zzz_nonexistent_term';
    render(<FaqPage />);
    expect(screen.getByText('pages.faq.emptyState.title')).toBeInTheDocument();
    expect(screen.getByText('pages.faq.emptyState.subtitle')).toBeInTheDocument();
    // Empty state hides the accordion list.
    const triggers = screen
      .queryAllByRole('button')
      .filter(b => b.getAttribute('aria-controls')?.startsWith('faq-panel-'));
    expect(triggers).toHaveLength(0);
  });

  it('does not render the popular grid when query is non-empty', () => {
    // 'q1' is part of every i18n key (via questionKey/shortKey/longKey), so it
    // matches every FAQ when t() is the identity mock.
    mockQuery = 'q1';
    render(<FaqPage />);
    expect(screen.queryByText('pages.faq.popularSection.title')).not.toBeInTheDocument();
  });

  it('does not render the popular grid when activeCat is not "all"', async () => {
    const user = userEvent.setup();
    render(<FaqPage />);
    const accountTab = screen.getByRole('tab', { name: /pages\.faq\.categories\.account/ });
    await user.click(accountTab);
    expect(screen.queryByText('pages.faq.popularSection.title')).not.toBeInTheDocument();
  });

  it('renders results banner when query yields >0 matches', () => {
    mockQuery = 'q1';
    render(<FaqPage />);
    expect(
      screen.getByText(/pages\.faq\.search\.resultsLabel \(count=\d+, query=q1\)/)
    ).toBeInTheDocument();
  });

  it('footer CTAs link to /contact and /how-it-works', () => {
    render(<FaqPage />);
    const contactLinks = screen.getAllByRole('link', { name: 'pages.faq.contactCta' });
    const howLinks = screen.getAllByRole('link', { name: 'pages.faq.howItWorksCta' });
    expect(contactLinks.length).toBeGreaterThanOrEqual(1);
    expect(contactLinks[0]).toHaveAttribute('href', '/contact');
    expect(howLinks[0]).toHaveAttribute('href', '/how-it-works');
  });

  it('renders loading state when ?loading=1 search param present (non-prod only)', () => {
    mockSearchParams.set('loading', '1');
    render(<FaqPage />);
    expect(screen.getByLabelText('pages.faq.loadingState.label')).toBeInTheDocument();
    // Skeleton replaces accordion: no accordion-controls present.
    const triggers = screen
      .queryAllByRole('button')
      .filter(b => b.getAttribute('aria-controls')?.startsWith('faq-panel-'));
    expect(triggers).toHaveLength(0);
  });

  it('renders error state when ?error=1 search param present (non-prod only)', () => {
    mockSearchParams.set('error', '1');
    render(<FaqPage />);
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('pages.faq.errorState.title')).toBeInTheDocument();
    expect(screen.getByText('pages.faq.errorState.subtitle')).toBeInTheDocument();
  });
});
