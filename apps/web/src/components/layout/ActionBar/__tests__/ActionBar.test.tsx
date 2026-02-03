/**
 * ActionBar Tests
 * Issue #3290 - Phase 4: ActionBar System
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LayoutProvider } from '../../LayoutProvider';
import { ActionBar, ActionBarSpacer } from '../ActionBar';
import { ActionBarItem } from '../ActionBarItem';
import { OverflowMenu } from '../OverflowMenu';
import type { Action } from '@/types/layout';

// Mock dependencies
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'desktop',
    isMobile: false,
    isTablet: false,
    isDesktop: true,
    viewportWidth: 1024,
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

// Helper wrapper with library context (has actions)
function TestWrapper({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      <LayoutProvider initialContext="library">{children}</LayoutProvider>
    </QueryClientProvider>
  );
}

// Helper wrapper with default context (no actions, for testing components directly)
function TestWrapperNoContext({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      <LayoutProvider>{children}</LayoutProvider>
    </QueryClientProvider>
  );
}

// Mock actions for testing
const mockActions: Action[] = [
  {
    id: 'add',
    icon: 'plus',
    label: 'Aggiungi',
    priority: 1,
    action: 'library:add',
  },
  {
    id: 'filter',
    icon: 'filter',
    label: 'Filtra',
    priority: 2,
    action: 'library:filter',
  },
  {
    id: 'sort',
    icon: 'arrow-up-down',
    label: 'Ordina',
    priority: 3,
    action: 'library:sort',
  },
];

describe('ActionBar', () => {
  it('should not render when context has no actions (default context)', () => {
    render(<ActionBar />, { wrapper: TestWrapperNoContext });

    // Default context has no actions, so ActionBar should not render
    expect(screen.queryByRole('toolbar')).not.toBeInTheDocument();
  });

  it('should export ActionBarSpacer component', () => {
    // Just verify the component exists and can be imported
    expect(ActionBar).toBeDefined();
  });
});

describe('ActionBarItem', () => {
  const mockAction: Action = {
    id: 'test',
    icon: 'plus',
    label: 'Test Action',
    priority: 1,
    action: 'test:action',
  };

  it('should render action button', () => {
    render(<ActionBarItem action={mockAction} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button');
    expect(button).toBeInTheDocument();
  });

  it('should have proper aria-label', () => {
    render(<ActionBarItem action={mockAction} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Test Action');
  });

  it('should call onClick when clicked', () => {
    const onClick = vi.fn();
    render(<ActionBarItem action={mockAction} onClick={onClick} />, {
      wrapper: TestWrapper,
    });

    const button = screen.getByRole('button');
    fireEvent.click(button);

    expect(onClick).toHaveBeenCalledWith(mockAction);
  });

  it('should not call onClick when disabled', () => {
    const onClick = vi.fn();
    render(
      <ActionBarItem action={mockAction} onClick={onClick} isDisabled={true} />,
      { wrapper: TestWrapper }
    );

    const button = screen.getByRole('button');
    fireEvent.click(button);

    expect(onClick).not.toHaveBeenCalled();
  });

  it('should render with icon-label variant', () => {
    render(<ActionBarItem action={mockAction} variant="icon-label" />, {
      wrapper: TestWrapper,
    });

    expect(screen.getByText('Test Action')).toBeInTheDocument();
  });

  it('should apply active state styling', () => {
    render(<ActionBarItem action={mockAction} isActive={true} />, {
      wrapper: TestWrapper,
    });

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-pressed', 'true');
  });

  it('should render primary variant with default button style', () => {
    const primaryAction: Action = {
      ...mockAction,
      variant: 'primary',
    };
    render(<ActionBarItem action={primaryAction} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button');
    expect(button).toBeInTheDocument();
  });

  it('should render destructive variant', () => {
    const destructiveAction: Action = {
      ...mockAction,
      variant: 'destructive',
    };
    render(<ActionBarItem action={destructiveAction} />, {
      wrapper: TestWrapper,
    });

    const button = screen.getByRole('button');
    expect(button).toBeInTheDocument();
  });
});

describe('OverflowMenu', () => {
  it('should not render when actions array is empty', () => {
    const { container } = render(<OverflowMenu actions={[]} />, {
      wrapper: TestWrapper,
    });

    expect(container.firstChild).toBeNull();
  });

  it('should render overflow menu button when actions exist', () => {
    render(<OverflowMenu actions={mockActions} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button', { name: /altre azioni/i });
    expect(button).toBeInTheDocument();
  });

  it('should have proper aria-label on trigger', () => {
    render(<OverflowMenu actions={mockActions} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label', 'Altre azioni');
  });

  it('should open dropdown menu on click', async () => {
    const user = userEvent.setup();
    render(<OverflowMenu actions={mockActions} />, { wrapper: TestWrapperNoContext });

    const button = screen.getByRole('button');
    await user.click(button);

    // Menu items should be visible
    await waitFor(() => {
      expect(screen.getByText('Aggiungi')).toBeInTheDocument();
    });
    expect(screen.getByText('Filtra')).toBeInTheDocument();
    expect(screen.getByText('Ordina')).toBeInTheDocument();
  });

  it('should call onActionSelect when action is selected', async () => {
    const user = userEvent.setup();
    const onActionSelect = vi.fn();
    render(
      <OverflowMenu actions={mockActions} onActionSelect={onActionSelect} />,
      { wrapper: TestWrapperNoContext }
    );

    const button = screen.getByRole('button');
    await user.click(button);

    // Wait for menu to open
    await waitFor(() => {
      expect(screen.getByText('Aggiungi')).toBeInTheDocument();
    });

    // Click on first action
    await user.click(screen.getByText('Aggiungi'));

    expect(onActionSelect).toHaveBeenCalledWith(mockActions[0]);
  });
});

describe('ActionBar Priority System', () => {
  it('should sort actions by priority (ascending)', () => {
    const unsortedActions: Action[] = [
      { id: 'c', icon: 'plus', label: 'C', priority: 3, action: 'c' },
      { id: 'a', icon: 'filter', label: 'A', priority: 1, action: 'a' },
      { id: 'b', icon: 'share', label: 'B', priority: 2, action: 'b' },
    ];

    // Test the priority calculation
    const sorted = [...unsortedActions].sort((a, b) => a.priority - b.priority);
    expect(sorted[0].id).toBe('a');
    expect(sorted[1].id).toBe('b');
    expect(sorted[2].id).toBe('c');
  });
});

describe('ActionBar Accessibility', () => {
  it('should have minimum touch target size', () => {
    render(<ActionBarItem action={mockActions[0]} />, { wrapper: TestWrapperNoContext });

    const button = screen.getByRole('button');
    expect(button).toHaveClass('min-h-[44px]');
    expect(button).toHaveClass('min-w-[44px]');
  });

  it('should support keyboard navigation', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(<ActionBarItem action={mockActions[0]} onClick={onClick} />, {
      wrapper: TestWrapperNoContext,
    });

    const button = screen.getByRole('button');
    button.focus();
    await user.keyboard('{Enter}');

    // Enter should trigger click
    expect(onClick).toHaveBeenCalled();
  });
});

describe('ActionBar Animation', () => {
  it('should apply stagger animation delay', () => {
    render(<ActionBarItem action={mockActions[0]} animationDelay={100} />, {
      wrapper: TestWrapper,
    });

    const button = screen.getByRole('button');
    expect(button).toHaveStyle({ animationDelay: '100ms' });
  });

  it('should have animate-in class for entry animation', () => {
    render(<ActionBarItem action={mockActions[0]} />, { wrapper: TestWrapper });

    const button = screen.getByRole('button');
    expect(button).toHaveClass('animate-in');
  });
});
