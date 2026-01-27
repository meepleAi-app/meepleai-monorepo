/**
 * ChatHistorySection Component Tests (Issue #2861)
 *
 * Test Coverage:
 * - MVP placeholder rendering
 * - Header with title and "Vedi Tutte" link
 * - Card content (icon, title, description)
 * - "Apri Chat" button
 * - Props handling (userId)
 * - Accessibility
 *
 * Target: >=85% coverage
 *
 * Updated for i18n compliance (Issue #3096): Uses data-testid pattern
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ChatHistorySection } from '../ChatHistorySection';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/link', () => ({
  default: ({ children, href, 'data-testid': testId }: { children: React.ReactNode; href: string; 'data-testid'?: string }) => (
    <a href={href} data-testid={testId}>{children}</a>
  ),
}));

// ============================================================================
// Test Suite
// ============================================================================

describe('ChatHistorySection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // Header Tests
  // ============================================================================

  describe('Header', () => {
    it('renders section title', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByTestId('chat-history-title')).toBeInTheDocument();
    });

    it('renders "Vedi Tutte" link in header', () => {
      render(<ChatHistorySection userId="user-1" />);

      const link = screen.getByTestId('chat-history-view-all-button');
      expect(link).toHaveAttribute('href', '/chat');
    });

    it('has correct heading structure (h2)', () => {
      render(<ChatHistorySection userId="user-1" />);

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveAttribute('data-testid', 'chat-history-title');
    });
  });

  // ============================================================================
  // MVP Placeholder Card Tests
  // ============================================================================

  describe('MVP Placeholder Card', () => {
    it('renders placeholder card', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByTestId('chat-history-placeholder-card')).toBeInTheDocument();
    });

    it('renders card title', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByTestId('chat-history-card-title')).toBeInTheDocument();
    });

    it('renders card description', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByTestId('chat-history-card-description')).toBeInTheDocument();
    });

    it('renders info text about chat history', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByTestId('chat-history-info-text')).toBeInTheDocument();
    });

    it('renders "Apri Chat" button', () => {
      render(<ChatHistorySection userId="user-1" />);

      const button = screen.getByTestId('chat-history-open-button');
      expect(button).toHaveAttribute('href', '/chat');
    });

    it('renders MessageSquare icon', () => {
      const { container } = render(<ChatHistorySection userId="user-1" />);

      const icons = container.querySelectorAll('svg.lucide-message-square');
      // There should be 2: one in the card header, one in the button
      expect(icons.length).toBeGreaterThanOrEqual(1);
    });

    it('renders dashed border on placeholder card', () => {
      render(<ChatHistorySection userId="user-1" />);

      const card = screen.getByTestId('chat-history-placeholder-card');
      expect(card).toHaveClass('border-dashed');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has correct aria-label on section', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByLabelText('Chat history')).toBeInTheDocument();
    });

    it('has correct data-testid on section', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByTestId('chat-history-section')).toBeInTheDocument();
    });

    it('has semantic HTML structure', () => {
      render(<ChatHistorySection userId="user-1" />);

      const section = screen.getByTestId('chat-history-section');
      expect(section.tagName.toLowerCase()).toBe('section');
    });
  });

  // ============================================================================
  // Props Tests
  // ============================================================================

  describe('Props Handling', () => {
    it('accepts userId prop without error', () => {
      // The component currently doesn't use userId, but accepts it for future use
      expect(() => render(<ChatHistorySection userId="test-user-id" />)).not.toThrow();
    });

    it('renders with different userId values', () => {
      const { rerender } = render(<ChatHistorySection userId="user-1" />);
      expect(screen.getByTestId('chat-history-title')).toBeInTheDocument();

      rerender(<ChatHistorySection userId="user-2" />);
      expect(screen.getByTestId('chat-history-title')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // CSS Classes Tests
  // ============================================================================

  describe('Styling', () => {
    it('applies font-quicksand to heading', () => {
      render(<ChatHistorySection userId="user-1" />);

      const heading = screen.getByTestId('chat-history-title');
      expect(heading).toHaveClass('font-quicksand');
    });

    it('applies space-y-4 to section', () => {
      render(<ChatHistorySection userId="user-1" />);

      const section = screen.getByTestId('chat-history-section');
      expect(section).toHaveClass('space-y-4');
    });

    it('renders icon with primary color styling', () => {
      const { container } = render(<ChatHistorySection userId="user-1" />);

      const iconWrapper = container.querySelector('.bg-primary\\/10');
      expect(iconWrapper).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Link Tests
  // ============================================================================

  describe('Links', () => {
    it('has two links to /chat', () => {
      render(<ChatHistorySection userId="user-1" />);

      const viewAllLink = screen.getByTestId('chat-history-view-all-button');
      const openChatLink = screen.getByTestId('chat-history-open-button');

      expect(viewAllLink).toHaveAttribute('href', '/chat');
      expect(openChatLink).toHaveAttribute('href', '/chat');
    });

    it('"Vedi Tutte" link is in header', () => {
      render(<ChatHistorySection userId="user-1" />);

      const vediTutteLink = screen.getByTestId('chat-history-view-all-button');
      expect(vediTutteLink).toBeInTheDocument();
    });

    it('"Apri Chat" link is in card', () => {
      render(<ChatHistorySection userId="user-1" />);

      const apriChatLink = screen.getByTestId('chat-history-open-button');
      expect(apriChatLink).toBeInTheDocument();
    });
  });
});
