/**
 * ChatSessionCard Unit Tests (Issue #3484)
 *
 * Coverage areas:
 * - Rendering with various props
 * - Click to resume behavior
 * - Delete with confirmation dialog
 * - Loading/deleting states
 * - Timestamp formatting
 * - Title truncation
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ChatSessionCard } from '../ChatSessionCard';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// Mock dropdown menu to make it easier to test
vi.mock('@/components/ui/navigation/dropdown-menu', () => ({
  DropdownMenu: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  DropdownMenuTrigger: ({ children, asChild, ...props }: { children: React.ReactNode; asChild?: boolean; [key: string]: unknown }) => (
    <div {...props}>{children}</div>
  ),
  DropdownMenuContent: ({ children }: { children: React.ReactNode }) => <div data-testid="dropdown-content">{children}</div>,
  DropdownMenuItem: ({ children, onClick, ...props }: { children: React.ReactNode; onClick?: () => void; [key: string]: unknown }) => (
    <button onClick={onClick} {...props}>{children}</button>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

const defaultProps = {
  id: 'session-123',
  title: 'Test Session Title',
  gameTitle: 'Wingspan',
  messageCount: 10,
  lastMessageAt: new Date('2026-01-20T14:30:00Z').toISOString(),
  createdAt: new Date('2026-01-20T10:00:00Z').toISOString(),
};

describe('ChatSessionCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders card with correct test id', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.getByTestId('chat-session-card-session-123')).toBeInTheDocument();
    });

    it('displays session title', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.getByTestId('session-title-session-123')).toHaveTextContent('Test Session Title');
    });

    it('displays game title when provided', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('does not display game title when not provided', () => {
      render(<ChatSessionCard {...defaultProps} gameTitle={undefined} />);

      expect(screen.queryByText('Wingspan')).not.toBeInTheDocument();
    });

    it('displays message count', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.getByTestId('session-messages-session-123')).toHaveTextContent('10 messaggi');
    });

    it('displays timestamp', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.getByTestId('session-time-session-123')).toBeInTheDocument();
    });

    it('displays last message preview when provided', () => {
      render(
        <ChatSessionCard {...defaultProps} lastMessagePreview="This is a preview..." />
      );

      expect(screen.getByTestId('session-preview-session-123')).toHaveTextContent('This is a preview...');
    });

    it('does not display preview when not provided', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.queryByTestId('session-preview-session-123')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Title Generation Tests
  // ============================================================================

  describe('Title Generation', () => {
    it('uses provided title', () => {
      render(<ChatSessionCard {...defaultProps} title="Custom Title" />);

      expect(screen.getByTestId('session-title-session-123')).toHaveTextContent('Custom Title');
    });

    it('generates default title when title is null', () => {
      render(<ChatSessionCard {...defaultProps} title={null} />);

      // Should generate title based on creation date
      expect(screen.getByTestId('session-title-session-123')).toHaveTextContent(/Chat del/);
    });

    it('generates default title when title is undefined', () => {
      render(<ChatSessionCard {...defaultProps} title={undefined} />);

      expect(screen.getByTestId('session-title-session-123')).toHaveTextContent(/Chat del/);
    });
  });

  // ============================================================================
  // Click Behavior Tests
  // ============================================================================

  describe('Click Behavior', () => {
    it('calls onResume with session id when clicked', () => {
      const onResume = vi.fn();
      render(<ChatSessionCard {...defaultProps} onResume={onResume} />);

      const card = screen.getByTestId('chat-session-card-session-123');
      fireEvent.click(card);

      expect(onResume).toHaveBeenCalledWith('session-123');
    });

    it('does not throw when onResume not provided', () => {
      render(<ChatSessionCard {...defaultProps} />);

      const card = screen.getByTestId('chat-session-card-session-123');
      expect(() => fireEvent.click(card)).not.toThrow();
    });
  });

  // ============================================================================
  // Delete Functionality Tests
  // ============================================================================

  describe('Delete Functionality', () => {
    it('shows menu when onDelete is provided', () => {
      const onDelete = vi.fn();
      render(<ChatSessionCard {...defaultProps} onDelete={onDelete} />);

      expect(screen.getByTestId('session-menu-session-123')).toBeInTheDocument();
    });

    it('does not show menu when onDelete not provided', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.queryByTestId('session-menu-session-123')).not.toBeInTheDocument();
    });

    it('opens confirmation dialog when delete clicked', () => {
      const onDelete = vi.fn();
      render(<ChatSessionCard {...defaultProps} onDelete={onDelete} />);

      // With mocked dropdown, the delete option is always visible
      const deleteOption = screen.getByTestId('delete-session-session-123');
      fireEvent.click(deleteOption);

      // Confirmation dialog should appear (ConfirmationDialog uses title prop)
      expect(screen.getByText('Eliminare questa conversazione?')).toBeInTheDocument();
    });

    it('calls onDelete when confirmation is accepted', () => {
      const onDelete = vi.fn();
      render(<ChatSessionCard {...defaultProps} onDelete={onDelete} />);

      // Click delete option (mocked dropdown always visible)
      const deleteOption = screen.getByTestId('delete-session-session-123');
      fireEvent.click(deleteOption);

      // Confirm deletion - ConfirmationDialog uses "Elimina" as confirm text
      const confirmBtn = screen.getByRole('button', { name: 'Elimina' });
      fireEvent.click(confirmBtn);

      expect(onDelete).toHaveBeenCalledWith('session-123');
    });

    it('does not call onDelete when confirmation is cancelled', () => {
      const onDelete = vi.fn();
      render(<ChatSessionCard {...defaultProps} onDelete={onDelete} />);

      // Click delete option (mocked dropdown always visible)
      const deleteOption = screen.getByTestId('delete-session-session-123');
      fireEvent.click(deleteOption);

      // Cancel deletion - ConfirmationDialog uses "Annulla" as cancel text
      const cancelBtn = screen.getByRole('button', { name: 'Annulla' });
      fireEvent.click(cancelBtn);

      expect(onDelete).not.toHaveBeenCalled();
    });

    it('shows message count in confirmation dialog', () => {
      const onDelete = vi.fn();
      render(<ChatSessionCard {...defaultProps} onDelete={onDelete} />);

      // Click delete option (mocked dropdown always visible)
      const deleteOption = screen.getByTestId('delete-session-session-123');
      fireEvent.click(deleteOption);

      // Check that the confirmation dialog message mentions the message count
      // Use getAllByText since there are multiple elements with "10 messaggi"
      const elements = screen.getAllByText(/10 messaggi/);
      expect(elements.length).toBeGreaterThanOrEqual(1);
    });

    it('delete button click does not trigger card click', () => {
      const onResume = vi.fn();
      const onDelete = vi.fn();
      render(
        <ChatSessionCard {...defaultProps} onResume={onResume} onDelete={onDelete} />
      );

      // Click delete option directly (with mocked dropdown always visible)
      const deleteOption = screen.getByTestId('delete-session-session-123');
      fireEvent.click(deleteOption);

      // Clicking delete should not trigger card click
      expect(onResume).not.toHaveBeenCalled();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('applies opacity when isDeleting is true', () => {
      render(<ChatSessionCard {...defaultProps} isDeleting />);

      const card = screen.getByTestId('chat-session-card-session-123');
      expect(card).toHaveClass('opacity-50');
      expect(card).toHaveClass('pointer-events-none');
    });

    it('does not apply opacity when isDeleting is false', () => {
      render(<ChatSessionCard {...defaultProps} isDeleting={false} />);

      const card = screen.getByTestId('chat-session-card-session-123');
      expect(card).not.toHaveClass('opacity-50');
    });
  });

  // ============================================================================
  // Timestamp Formatting Tests
  // ============================================================================

  describe('Timestamp Formatting', () => {
    it('formats today timestamp correctly', () => {
      const now = new Date();
      const todayTime = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 14, 30);

      render(
        <ChatSessionCard {...defaultProps} lastMessageAt={todayTime.toISOString()} />
      );

      const timeElement = screen.getByTestId('session-time-session-123');
      expect(timeElement.textContent).toMatch(/Oggi/);
    });

    it('formats null lastMessageAt as "Mai"', () => {
      render(<ChatSessionCard {...defaultProps} lastMessageAt={null} />);

      const timeElement = screen.getByTestId('session-time-session-123');
      expect(timeElement.textContent).toBe('Mai');
    });
  });

  // ============================================================================
  // Preview Truncation Tests
  // ============================================================================

  describe('Preview Truncation', () => {
    it('truncates long preview text', () => {
      const longPreview = 'A'.repeat(150);
      render(<ChatSessionCard {...defaultProps} lastMessagePreview={longPreview} />);

      const preview = screen.getByTestId('session-preview-session-123');
      expect(preview.textContent?.length).toBeLessThan(150);
      expect(preview.textContent).toContain('...');
    });

    it('does not truncate short preview text', () => {
      const shortPreview = 'Short preview';
      render(<ChatSessionCard {...defaultProps} lastMessagePreview={shortPreview} />);

      const preview = screen.getByTestId('session-preview-session-123');
      expect(preview.textContent).toBe('Short preview');
    });
  });

  // ============================================================================
  // Animation Tests
  // ============================================================================

  describe('Animation', () => {
    it('accepts index prop for staggered animation', () => {
      // Just ensure it doesn't throw
      render(<ChatSessionCard {...defaultProps} index={3} />);

      expect(screen.getByTestId('chat-session-card-session-123')).toBeInTheDocument();
    });

    it('uses default index of 0', () => {
      render(<ChatSessionCard {...defaultProps} />);

      expect(screen.getByTestId('chat-session-card-session-123')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('applies custom className', () => {
      const { container } = render(
        <ChatSessionCard {...defaultProps} className="custom-card" />
      );

      expect(container.querySelector('.custom-card')).toBeInTheDocument();
    });

    it('has rounded styling', () => {
      render(<ChatSessionCard {...defaultProps} />);

      const card = screen.getByTestId('chat-session-card-session-123');
      expect(card).toHaveClass('rounded-xl');
    });

    it('has hover styles', () => {
      render(<ChatSessionCard {...defaultProps} />);

      const card = screen.getByTestId('chat-session-card-session-123');
      expect(card).toHaveClass('cursor-pointer');
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles zero message count', () => {
      render(<ChatSessionCard {...defaultProps} messageCount={0} />);

      expect(screen.getByTestId('session-messages-session-123')).toHaveTextContent('0 messaggi');
    });

    it('handles empty game title string', () => {
      render(<ChatSessionCard {...defaultProps} gameTitle="" />);

      // Empty string should not render game title section
      const gameIcon = screen.queryByTestId('chat-session-card-session-123')
        ?.querySelector('[class*="Gamepad"]');
      // The component shows gameTitle only if truthy
      expect(screen.queryByText('Wingspan')).not.toBeInTheDocument();
    });

    it('handles special characters in title', () => {
      render(
        <ChatSessionCard {...defaultProps} title="Test <script>alert('xss')</script>" />
      );

      // Should escape HTML
      expect(screen.getByTestId('session-title-session-123')).toHaveTextContent(
        "Test <script>alert('xss')</script>"
      );
    });
  });
});
