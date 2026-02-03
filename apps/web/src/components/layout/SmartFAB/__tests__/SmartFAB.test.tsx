/**
 * SmartFAB Tests
 * Issue #3291 - Phase 5: Smart FAB
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LayoutProvider } from '../../LayoutProvider';
import { SmartFAB } from '../SmartFAB';
import { QuickMenu } from '../QuickMenu';
import type { QuickMenuItem } from '@/config/fab';

// Mock dependencies - mobile by default
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'mobile',
    isMobile: true,
    isTablet: false,
    isDesktop: false,
    viewportWidth: 375,
  }),
}));

// Create query client for tests
const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

// Helper wrapper with library context (has FAB config)
function TestWrapper({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      <LayoutProvider initialContext="library">{children}</LayoutProvider>
    </QueryClientProvider>
  );
}

// Helper wrapper with default context (no FAB config)
function TestWrapperNoFAB({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      <LayoutProvider>{children}</LayoutProvider>
    </QueryClientProvider>
  );
}

const mockQuickMenuItems: QuickMenuItem[] = [
  { id: 'search', icon: 'search', label: 'Cerca', action: 'library:search' },
  { id: 'scan', icon: 'camera', label: 'Scansiona', action: 'library:scan' },
];

describe('SmartFAB', () => {
  it('should render FAB in library context on mobile', () => {
    render(<SmartFAB />, { wrapper: TestWrapper });

    const fab = screen.getByRole('button', { name: /aggiungi gioco/i });
    expect(fab).toBeInTheDocument();
  });

  it('should not render when context has no FAB config', () => {
    render(<SmartFAB />, { wrapper: TestWrapperNoFAB });

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('should render with forceVisible override', () => {
    render(<SmartFAB forceVisible={true} />, { wrapper: TestWrapper });

    const fab = screen.getByRole('button');
    expect(fab).toBeInTheDocument();
  });

  it('should have proper aria attributes', () => {
    render(<SmartFAB />, { wrapper: TestWrapper });

    const fab = screen.getByRole('button');
    expect(fab).toHaveAttribute('aria-label', 'Aggiungi gioco');
    expect(fab).toHaveAttribute('aria-haspopup', 'menu');
  });
});

describe('QuickMenu', () => {
  it('should not render when closed', () => {
    const { container } = render(
      <QuickMenu
        items={mockQuickMenuItems}
        isOpen={false}
        onClose={() => {}}
        onSelect={() => {}}
      />,
      { wrapper: TestWrapperNoFAB }
    );

    expect(container.firstChild).toBeNull();
  });

  it('should not render when items are empty', () => {
    const { container } = render(
      <QuickMenu
        items={[]}
        isOpen={true}
        onClose={() => {}}
        onSelect={() => {}}
      />,
      { wrapper: TestWrapperNoFAB }
    );

    expect(container.firstChild).toBeNull();
  });

  it('should render menu items when open', () => {
    render(
      <QuickMenu
        items={mockQuickMenuItems}
        isOpen={true}
        onClose={() => {}}
        onSelect={() => {}}
      />,
      { wrapper: TestWrapperNoFAB }
    );

    expect(screen.getByText('Cerca')).toBeInTheDocument();
    expect(screen.getByText('Scansiona')).toBeInTheDocument();
  });

  it('should have proper role for menu', () => {
    render(
      <QuickMenu
        items={mockQuickMenuItems}
        isOpen={true}
        onClose={() => {}}
        onSelect={() => {}}
      />,
      { wrapper: TestWrapperNoFAB }
    );

    const menu = screen.getByRole('menu');
    expect(menu).toHaveAttribute('aria-label', 'Azioni rapide');
  });

  it('should call onSelect when item is clicked', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();

    render(
      <QuickMenu
        items={mockQuickMenuItems}
        isOpen={true}
        onClose={() => {}}
        onSelect={onSelect}
      />,
      { wrapper: TestWrapperNoFAB }
    );

    await user.click(screen.getByText('Cerca'));

    expect(onSelect).toHaveBeenCalledWith('search');
  });

  it('should close on Escape key', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();

    render(
      <QuickMenu
        items={mockQuickMenuItems}
        isOpen={true}
        onClose={onClose}
        onSelect={() => {}}
      />,
      { wrapper: TestWrapperNoFAB }
    );

    await user.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalled();
  });
});

describe('SmartFAB accessibility', () => {
  it('should have minimum touch target size', () => {
    render(<SmartFAB />, { wrapper: TestWrapper });

    const fab = screen.getByRole('button');
    expect(fab).toHaveClass('h-14');
    expect(fab).toHaveClass('w-14');
  });

  it('should have accessible label for screen readers', () => {
    render(<SmartFAB />, { wrapper: TestWrapper });

    const fab = screen.getByRole('button', { name: /aggiungi gioco/i });
    expect(fab).toBeInTheDocument();
  });
});

describe('QuickMenu items', () => {
  it('should render menu items with proper role', () => {
    render(
      <QuickMenu
        items={mockQuickMenuItems}
        isOpen={true}
        onClose={() => {}}
        onSelect={() => {}}
      />,
      { wrapper: TestWrapperNoFAB }
    );

    const menuItems = screen.getAllByRole('menuitem');
    expect(menuItems).toHaveLength(2);
  });
});
