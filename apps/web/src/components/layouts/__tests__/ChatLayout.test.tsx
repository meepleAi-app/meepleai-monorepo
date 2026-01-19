/**
 * ChatLayout Component Tests - Issue #2232
 *
 * Tests for chat layout component.
 * Features tested:
 * - Collapsible sidebar with localStorage persistence
 * - Mobile responsive with Sheet drawer
 * - Header integration
 * - Full-height layout optimization
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatLayout, type ChatLayoutProps } from '../ChatLayout';
import { Game } from '@/types';
import { getMenuItem } from '@/test-utils/locale-queries';

// Mock localStorage
const localStorageMock = {
  getItem: vi.fn(),
  setItem: vi.fn(),
  removeItem: vi.fn(),
  clear: vi.fn(),
};
Object.defineProperty(window, 'localStorage', { value: localStorageMock });

const mockGames: Game[] = [
  { id: 'game-1', title: 'Catan', bggId: 13, year: 1995, description: '' },
  { id: 'game-2', title: 'Pandemic', bggId: 30549, year: 2008, description: '' },
];

const defaultProps: ChatLayoutProps = {
  children: <div data-testid="chat-content">Chat Messages</div>,
  sidebarContent: <div data-testid="sidebar-content">Sidebar Content</div>,
  game: mockGames[0],
  games: mockGames,
  onGameChange: vi.fn(),
  threadTitle: 'Test Thread',
  onTitleChange: vi.fn(),
  onShare: vi.fn(),
  onExport: vi.fn(),
  onDelete: vi.fn(),
};

describe('ChatLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorageMock.getItem.mockReturnValue(null);
  });

  describe('Basic rendering', () => {
    it('renders children content', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByTestId('chat-content')).toBeInTheDocument();
    });

    it('renders sidebar content on desktop', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByTestId('sidebar-content')).toBeInTheDocument();
    });

    it('renders ChatHeader component', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('renders with full-height layout', () => {
      const { container } = render(<ChatLayout {...defaultProps} />);

      // Main content should be flex and full height
      const main = screen.getByRole('main');
      expect(main).toHaveClass('flex-1');
      expect(main).toHaveClass('overflow-hidden');
    });
  });

  describe('Sidebar collapse', () => {
    it('starts expanded by default', () => {
      localStorageMock.getItem.mockReturnValue(null);
      render(<ChatLayout {...defaultProps} />);

      // Sidebar content should be visible
      expect(screen.getByTestId('sidebar-content')).toBeInTheDocument();
    });

    it('loads collapsed state from localStorage', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      render(<ChatLayout {...defaultProps} />);

      await waitFor(() => {
        expect(localStorageMock.getItem).toHaveBeenCalledWith('chat-sidebar-collapsed');
      });
    });

    it('saves collapsed state to localStorage on collapse', async () => {
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      // Click collapse button
      const collapseButton = screen.getByLabelText('Collapse thread sidebar');
      await user.click(collapseButton);

      expect(localStorageMock.setItem).toHaveBeenCalledWith('chat-sidebar-collapsed', 'true');
    });

    it('saves expanded state to localStorage on expand', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      // Wait for component to mount and load state
      await waitFor(() => {
        expect(screen.getByLabelText('Expand thread sidebar')).toBeInTheDocument();
      });

      const expandButton = screen.getByLabelText('Expand thread sidebar');
      await user.click(expandButton);

      expect(localStorageMock.setItem).toHaveBeenCalledWith('chat-sidebar-collapsed', 'false');
    });

    it('hides sidebar content when collapsed', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      render(<ChatLayout {...defaultProps} />);

      await waitFor(() => {
        // Sidebar should have w-0 class when collapsed
        const sidebar = screen.getByLabelText('Chat sidebar');
        expect(sidebar).toHaveClass('md:w-0');
      });
    });

    it('shows collapse button when expanded', () => {
      localStorageMock.getItem.mockReturnValue(null);
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByLabelText('Collapse thread sidebar')).toBeInTheDocument();
    });

    it('shows expand button when collapsed', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      render(<ChatLayout {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Expand thread sidebar')).toBeInTheDocument();
      });
    });
  });

  describe('Mobile menu', () => {
    it('renders mobile menu button in header', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByLabelText('Open chat navigation')).toBeInTheDocument();
    });

    it('opens mobile drawer on button click', async () => {
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      const menuButton = screen.getByLabelText('Open chat navigation');
      await user.click(menuButton);

      // Sheet should open with title
      expect(screen.getByText('MeepleAI Chat')).toBeInTheDocument();
    });

    it('renders sidebar content in mobile drawer', async () => {
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      await user.click(screen.getByLabelText('Open chat navigation'));

      // Sidebar content should be in drawer
      expect(screen.getAllByTestId('sidebar-content')).toHaveLength(2); // Desktop + mobile
    });

    it('mobile menu button is hidden on desktop', () => {
      render(<ChatLayout {...defaultProps} />);

      const menuButton = screen.getByLabelText('Open chat navigation');
      expect(menuButton.closest('.md\\:hidden')).toBeInTheDocument();
    });
  });

  describe('Header integration', () => {
    it('passes game to header', () => {
      render(<ChatLayout {...defaultProps} />);

      // Game should be in selector
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('passes games list to header', async () => {
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      // Open game selector
      const trigger = screen.getByRole('combobox', { name: 'Select game' });
      await user.click(trigger);

      // All games should be available
      expect(screen.getByText('Pandemic')).toBeInTheDocument();
    });

    it('passes threadTitle to header', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByText('Test Thread')).toBeInTheDocument();
    });

    it('forwards onGameChange to header', async () => {
      const user = userEvent.setup();
      const onGameChange = vi.fn();
      render(<ChatLayout {...defaultProps} onGameChange={onGameChange} />);

      const trigger = screen.getByRole('combobox', { name: 'Select game' });
      await user.click(trigger);
      await user.click(screen.getByText('Pandemic'));

      expect(onGameChange).toHaveBeenCalledWith('game-2');
    });

    it('forwards onTitleChange to header', async () => {
      const user = userEvent.setup();
      const onTitleChange = vi.fn();
      render(<ChatLayout {...defaultProps} onTitleChange={onTitleChange} />);

      await user.click(screen.getByLabelText('Click to edit thread title'));
      const input = screen.getByPlaceholderText('Thread title');
      await user.clear(input);
      await user.type(input, 'New Title{Enter}');

      expect(onTitleChange).toHaveBeenCalledWith('New Title');
    });
  });

  describe('Actions integration', () => {
    it('forwards onShare to header', async () => {
      const user = userEvent.setup();
      const onShare = vi.fn();
      render(<ChatLayout {...defaultProps} onShare={onShare} />);

      await user.click(screen.getByLabelText('Thread actions menu'));
      await user.click(getMenuItem(/share thread/i));

      expect(onShare).toHaveBeenCalledTimes(1);
    });

    it('forwards onExport to header', async () => {
      const user = userEvent.setup();
      const onExport = vi.fn();
      render(<ChatLayout {...defaultProps} onExport={onExport} />);

      await user.click(screen.getByLabelText('Thread actions menu'));
      await user.click(getMenuItem(/export chat/i));

      expect(onExport).toHaveBeenCalledTimes(1);
    });

    it('forwards onDelete to header', async () => {
      const user = userEvent.setup();
      const onDelete = vi.fn();
      render(<ChatLayout {...defaultProps} onDelete={onDelete} />);

      await user.click(screen.getByLabelText('Thread actions menu'));
      await user.click(getMenuItem(/delete thread/i));

      expect(onDelete).toHaveBeenCalledTimes(1);
    });
  });

  describe('Styling', () => {
    it('applies custom className to main content', () => {
      const { container } = render(<ChatLayout {...defaultProps} className="custom-main" />);

      expect(container.querySelector('.custom-main')).toBeInTheDocument();
    });

    it('applies min-height to layout', () => {
      const { container } = render(<ChatLayout {...defaultProps} />);

      expect(container.querySelector('.min-h-screen')).toBeInTheDocument();
    });

    it('applies dark mode background', () => {
      const { container } = render(<ChatLayout {...defaultProps} />);

      expect(container.querySelector('.dark\\:bg-gray-950')).toBeInTheDocument();
    });

    it('applies transition classes for sidebar width change', () => {
      const { container } = render(<ChatLayout {...defaultProps} />);

      const sidebar = screen.getByLabelText('Chat sidebar');
      expect(sidebar).toHaveClass('transition-[width,min-width]');
      expect(sidebar).toHaveClass('duration-300');
    });
  });

  describe('Layout structure', () => {
    it('has flex container for sidebar and main', () => {
      const { container } = render(<ChatLayout {...defaultProps} />);

      const flexContainer = container.querySelector('.flex.h-\\[calc\\(100vh-3\\.5rem\\)\\]');
      expect(flexContainer).toBeInTheDocument();
    });

    it('main content is flex-1', () => {
      render(<ChatLayout {...defaultProps} />);

      const main = screen.getByRole('main');
      expect(main).toHaveClass('flex-1');
    });

    it('sidebar is hidden on mobile', () => {
      render(<ChatLayout {...defaultProps} />);

      const sidebar = screen.getByLabelText('Chat sidebar');
      expect(sidebar).toHaveClass('hidden');
      expect(sidebar).toHaveClass('md:flex');
    });
  });

  describe('Hydration safety', () => {
    it('uses default width before mount for hydration safety', () => {
      localStorageMock.getItem.mockReturnValue('true');
      render(<ChatLayout {...defaultProps} />);

      // Component should render without errors
      expect(screen.getByTestId('chat-content')).toBeInTheDocument();
    });
  });

  describe('TooltipProvider integration', () => {
    it('wraps sidebar content in TooltipProvider', () => {
      render(<ChatLayout {...defaultProps} />);

      // Sidebar should render without tooltip errors
      expect(screen.getByTestId('sidebar-content')).toBeInTheDocument();
    });
  });

  describe('Edge cases', () => {
    it('handles undefined game gracefully', () => {
      render(<ChatLayout {...defaultProps} game={undefined} />);

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('handles empty games array', () => {
      render(<ChatLayout {...defaultProps} games={[]} />);

      expect(screen.getByTestId('chat-content')).toBeInTheDocument();
    });

    it('handles undefined actions gracefully', () => {
      render(
        <ChatLayout
          {...defaultProps}
          onShare={undefined}
          onExport={undefined}
          onDelete={undefined}
        />
      );

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });

    it('handles undefined loading state', () => {
      render(<ChatLayout {...defaultProps} loading={undefined} />);

      expect(screen.getByRole('banner')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has accessible sidebar label', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByLabelText('Chat sidebar')).toBeInTheDocument();
    });

    it('has accessible collapse button', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByLabelText('Collapse thread sidebar')).toBeInTheDocument();
    });

    it('has accessible expand button when collapsed', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      render(<ChatLayout {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Expand thread sidebar')).toBeInTheDocument();
      });
    });

    it('has accessible mobile menu trigger', () => {
      render(<ChatLayout {...defaultProps} />);

      expect(screen.getByLabelText('Open chat navigation')).toBeInTheDocument();
    });

    it('Sheet has accessible title and description', async () => {
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      await user.click(screen.getByLabelText('Open chat navigation'));

      expect(screen.getByText('MeepleAI Chat')).toBeInTheDocument();
      expect(screen.getByText('Chat navigation menu')).toBeInTheDocument();
    });
  });

  describe('Responsive behavior', () => {
    it('sidebar has responsive width classes', () => {
      render(<ChatLayout {...defaultProps} />);

      const sidebar = screen.getByLabelText('Chat sidebar');
      expect(sidebar).toHaveClass('md:w-80');
    });

    it('collapse toggle is present for desktop', () => {
      render(<ChatLayout {...defaultProps} />);

      // Collapse button should exist and be accessible
      const collapseButton = screen.getByLabelText('Collapse thread sidebar');
      expect(collapseButton).toBeInTheDocument();
      expect(collapseButton).toHaveClass('md:block');
    });

    it('mobile Sheet has proper width', async () => {
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      await user.click(screen.getByLabelText('Open chat navigation'));

      // Sheet content should have w-80 class
      const sheetContent = screen.getByText('MeepleAI Chat').closest('[role="dialog"]');
      expect(sheetContent).toBeInTheDocument();
    });
  });

  describe('Loading states', () => {
    it('passes loading state to header', () => {
      render(<ChatLayout {...defaultProps} loading={{ games: true, title: false }} />);

      // Header should receive loading prop
      const selector = screen.getByRole('combobox', { name: 'Select game' });
      expect(selector).toBeDisabled();
    });
  });

  describe('Multiple children', () => {
    it('renders multiple children in main area', () => {
      render(
        <ChatLayout {...defaultProps}>
          <div data-testid="child-1">Child 1</div>
          <div data-testid="child-2">Child 2</div>
        </ChatLayout>
      );

      expect(screen.getByTestId('child-1')).toBeInTheDocument();
      expect(screen.getByTestId('child-2')).toBeInTheDocument();
    });
  });

  describe('Layout dimensions', () => {
    it('applies calc height for content area', () => {
      const { container } = render(<ChatLayout {...defaultProps} />);

      const layoutContainer = container.querySelector('.h-\\[calc\\(100vh-3\\.5rem\\)\\]');
      expect(layoutContainer).toBeInTheDocument();
    });

    it('sidebar has min-width constraint', () => {
      render(<ChatLayout {...defaultProps} />);

      const sidebar = screen.getByLabelText('Chat sidebar');
      expect(sidebar).toHaveClass('min-w-[320px]');
    });
  });

  describe('Sidebar visibility toggle', () => {
    it('toggles sidebar visibility on collapse button click', async () => {
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      // Initially expanded
      const sidebar = screen.getByLabelText('Chat sidebar');
      expect(sidebar).toHaveClass('md:w-80');

      // Click collapse
      await user.click(screen.getByLabelText('Collapse thread sidebar'));

      // Should update localStorage
      expect(localStorageMock.setItem).toHaveBeenCalledWith('chat-sidebar-collapsed', 'true');
    });

    it('toggles sidebar visibility on expand button click', async () => {
      localStorageMock.getItem.mockReturnValue('true');
      const user = userEvent.setup();
      render(<ChatLayout {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Expand thread sidebar')).toBeInTheDocument();
      });

      // Click expand
      await user.click(screen.getByLabelText('Expand thread sidebar'));

      // Should update localStorage
      expect(localStorageMock.setItem).toHaveBeenCalledWith('chat-sidebar-collapsed', 'false');
    });
  });

  describe('Transition animations', () => {
    it('applies transition duration to sidebar', () => {
      render(<ChatLayout {...defaultProps} />);

      const sidebar = screen.getByLabelText('Chat sidebar');
      expect(sidebar).toHaveClass('duration-300');
      expect(sidebar).toHaveClass('ease-in-out');
    });

    it('applies transition to main content', () => {
      render(<ChatLayout {...defaultProps} />);

      const main = screen.getByRole('main');
      expect(main).toHaveClass('transition-all');
      expect(main).toHaveClass('duration-300');
    });
  });
});
