import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';

// Mock the LibraryHub so we don't pull in its downstream deps
vi.mock('../LibraryHub', () => ({
  LibraryHub: () => <div data-testid="library-hub">Library Hub</div>,
}));

vi.mock('../AddGameDrawer', () => ({
  AddGameDrawerController: () => null,
}));

vi.mock('@/components/layout/FloatingActionPill', () => ({
  FloatingActionPill: () => null,
}));

describe('LibraryContent', () => {
  afterEach(() => {
    vi.clearAllMocks();
    vi.resetModules();
  });

  it('renders the LibraryHub as the sole canonical content', async () => {
    vi.resetModules();
    const mod = await import('../_content');
    render(<mod.LibraryContent />);
    expect(screen.getByTestId('library-hub')).toBeInTheDocument();
  });

  it('exposes a LibraryLoadingSkeleton for the Suspense fallback', async () => {
    vi.resetModules();
    const mod = await import('../_content');
    const { container } = render(<mod.LibraryLoadingSkeleton />);
    // The skeleton is a set of animated placeholder divs; we just verify it renders.
    expect(container.firstChild).toBeTruthy();
  });
});
