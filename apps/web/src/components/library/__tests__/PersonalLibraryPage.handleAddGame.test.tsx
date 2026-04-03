import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { describe, expect, it, vi } from 'vitest';

import { useLayoutResponsive } from '@/components/layout/LayoutProvider';
import { useLibrary } from '@/hooks/queries/useLibrary';

import { PersonalLibraryPage } from '../PersonalLibraryPage';

// ── Navigation mocks ─────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  usePathname: vi.fn(),
  useSearchParams: vi.fn(),
}));

// ── Data/hook mocks ───────────────────────────────────────────────────────────

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(),
}));

vi.mock('@/components/layout/LayoutProvider', () => ({
  useLayoutResponsive: vi.fn(),
}));

// ── Child component mocks (avoid provider / network dependencies) ─────────────

vi.mock('@/components/library/LibraryEmptyState', () => ({
  LibraryEmptyState: () => <div data-testid="library-empty-state" />,
}));

vi.mock('@/components/library/LibraryHeroBanner', () => ({
  LibraryHeroBanner: () => <div data-testid="library-hero-banner" />,
}));

vi.mock('@/components/library/UsageWidget', () => ({
  UsageWidget: () => <div data-testid="usage-widget" />,
}));

vi.mock('@/components/library/LibraryToolbar', () => ({
  LibraryToolbar: () => <div data-testid="library-toolbar" />,
}));

vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: () => <div data-testid="meeple-card" />,
}));

vi.mock('@/components/ui/FilterChipsRow', () => ({
  FilterChipsRow: () => <div data-testid="filter-chips-row" />,
}));

vi.mock('@/components/ui/SectionBlock', () => ({
  SectionBlock: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="section-block">{children}</div>
  ),
}));

vi.mock('@/components/ui/ViewToggle', () => ({
  ViewToggle: () => <div data-testid="view-toggle" />,
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

function setupEmptyLibrary(mockPush: ReturnType<typeof vi.fn>, searchParamsString = '') {
  vi.mocked(useRouter).mockReturnValue({ push: mockPush } as ReturnType<typeof useRouter>);
  vi.mocked(usePathname).mockReturnValue('/library');
  vi.mocked(useSearchParams).mockReturnValue(
    new URLSearchParams(searchParamsString) as ReturnType<typeof useSearchParams>
  );
  vi.mocked(useLibrary).mockReturnValue({
    data: { items: [], totalCount: 0 },
    isLoading: false,
  } as ReturnType<typeof useLibrary>);
  vi.mocked(useLayoutResponsive).mockReturnValue({
    isMobile: false,
  } as ReturnType<typeof useLayoutResponsive>);
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('PersonalLibraryPage — handleAddGame', () => {
  it('navigates to ?action=add when Add Game button is clicked (empty state)', async () => {
    const mockPush = vi.fn();
    setupEmptyLibrary(mockPush);

    render(<PersonalLibraryPage />);

    // LibraryPageHeader renders two buttons with the same aria-label (desktop + mobile FAB).
    // Both fire handleAddGame, so targeting the first is sufficient.
    const addButtons = screen.getAllByRole('button', {
      name: /aggiungi un gioco alla libreria/i,
    });
    expect(addButtons.length).toBeGreaterThanOrEqual(1);

    await userEvent.click(addButtons[0]);

    expect(mockPush).toHaveBeenCalledWith('/library?action=add');
  });

  it('preserves existing search params when navigating to ?action=add', async () => {
    const mockPush = vi.fn();
    setupEmptyLibrary(mockPush, 'tab=games');

    render(<PersonalLibraryPage />);

    const addButtons = screen.getAllByRole('button', {
      name: /aggiungi un gioco alla libreria/i,
    });

    await userEvent.click(addButtons[0]);

    expect(mockPush).toHaveBeenCalledWith('/library?tab=games&action=add');
  });
});
