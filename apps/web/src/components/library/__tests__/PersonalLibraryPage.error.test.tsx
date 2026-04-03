import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useLayoutResponsive } from '@/components/layout/LayoutProvider';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import { PersonalLibraryPage } from '../PersonalLibraryPage';

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn() })),
  usePathname: vi.fn(() => '/library'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({ useLibrary: vi.fn() }));
vi.mock('@/components/layout/LayoutProvider', () => ({
  useLayoutResponsive: vi.fn(() => ({ isMobile: false })),
}));

// Mock child components to avoid provider chains
vi.mock('../LibraryEmptyState', () => ({ LibraryEmptyState: () => null }));
vi.mock('../LibraryHeroBanner', () => ({ LibraryHeroBanner: () => null }));
vi.mock('../LibraryPageHeader', () => ({ LibraryPageHeader: () => null }));
vi.mock('../UsageWidget', () => ({ UsageWidget: () => null }));
vi.mock('../LibraryToolbar', () => ({ LibraryToolbar: () => null }));

describe('PersonalLibraryPage — error state', () => {
  it('renders error alert when useLibrary returns an error', () => {
    vi.mocked(useLibrary).mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as any);

    render(<PersonalLibraryPage />);

    expect(screen.getByText(/errore nel caricamento/i)).toBeInTheDocument();
    expect(screen.getByText(/impossibile caricare la libreria/i)).toBeInTheDocument();
  });

  it('does not render error alert when library loads successfully', () => {
    vi.mocked(useLibrary).mockReturnValue({
      data: { items: [], totalCount: 0 },
      isLoading: false,
      error: null,
    } as any);

    render(<PersonalLibraryPage />);

    expect(screen.queryByText(/errore nel caricamento/i)).not.toBeInTheDocument();
  });
});
