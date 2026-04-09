import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';

// Mock the V2 hub so we don't pull in its downstream deps
vi.mock('../v2', () => ({
  LibraryHubV2: () => <div data-testid="library-hub-v2">V2 Library Hub</div>,
}));

// Mock legacy dynamic-imported components
vi.mock('@/components/library/PersonalLibraryPage', () => ({
  PersonalLibraryPage: () => <div data-testid="legacy-personal">Legacy Personal</div>,
}));

vi.mock('../public/PublicLibraryClient', () => ({
  default: () => <div data-testid="legacy-catalog">Legacy Catalog</div>,
}));

vi.mock('../wishlist/page', () => ({
  default: () => <div data-testid="legacy-wishlist">Legacy Wishlist</div>,
}));

vi.mock('../AddGameDrawer', () => ({
  AddGameDrawerController: () => null,
}));

vi.mock('@/components/layout/FloatingActionPill', () => ({
  FloatingActionPill: () => null,
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: () => ({ drawCard: vi.fn() }),
}));

const mockSearchParams = {
  get: vi.fn<(key: string) => string | null>(),
};

vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams,
}));

const originalEnv = process.env.NEXT_PUBLIC_UX_REDESIGN;

describe('LibraryContent feature flag branch', () => {
  afterEach(() => {
    if (originalEnv === undefined) delete process.env.NEXT_PUBLIC_UX_REDESIGN;
    else process.env.NEXT_PUBLIC_UX_REDESIGN = originalEnv;
    vi.clearAllMocks();
    vi.resetModules();
  });

  it('renders V2 LibraryHub when flag is on and no tab', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
    mockSearchParams.get.mockReturnValue(null);
    vi.resetModules();
    const mod = await import('../_content');
    render(<mod.LibraryContent />);
    expect(screen.getByTestId('library-hub-v2')).toBeInTheDocument();
    expect(screen.queryByTestId('legacy-personal')).not.toBeInTheDocument();
  });

  it('renders legacy Personal when flag is off (no tab)', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'false';
    mockSearchParams.get.mockReturnValue(null);
    vi.resetModules();
    const mod = await import('../_content');
    render(<mod.LibraryContent />);
    expect(screen.queryByTestId('library-hub-v2')).not.toBeInTheDocument();
  });

  it('renders legacy Catalog when tab=catalogo (even with flag on)', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
    mockSearchParams.get.mockReturnValue('catalogo');
    vi.resetModules();
    const mod = await import('../_content');
    render(<mod.LibraryContent />);
    expect(screen.queryByTestId('library-hub-v2')).not.toBeInTheDocument();
  });

  it('renders legacy Wishlist when tab=wishlist (even with flag on)', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
    mockSearchParams.get.mockReturnValue('wishlist');
    vi.resetModules();
    const mod = await import('../_content');
    render(<mod.LibraryContent />);
    expect(screen.queryByTestId('library-hub-v2')).not.toBeInTheDocument();
  });
});
