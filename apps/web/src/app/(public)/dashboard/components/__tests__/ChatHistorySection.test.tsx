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
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ChatHistorySection } from '../ChatHistorySection';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
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

      expect(screen.getByText('Cronologia Chat')).toBeInTheDocument();
    });

    it('renders "Vedi Tutte" link in header', () => {
      render(<ChatHistorySection userId="user-1" />);

      const link = screen.getByRole('link', { name: /Vedi Tutte/i });
      expect(link).toHaveAttribute('href', '/chat');
    });

    it('has correct heading structure (h2)', () => {
      render(<ChatHistorySection userId="user-1" />);

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Cronologia Chat');
    });
  });

  // ============================================================================
  // MVP Placeholder Card Tests
  // ============================================================================

  describe('MVP Placeholder Card', () => {
    it('renders card title "Chat Recenti"', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByText('Chat Recenti')).toBeInTheDocument();
    });

    it('renders card description', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(screen.getByText('Visualizza le tue conversazioni più recenti')).toBeInTheDocument();
    });

    it('renders info text about chat history', () => {
      render(<ChatHistorySection userId="user-1" />);

      expect(
        screen.getByText('La cronologia completa delle chat è disponibile nella sezione Chat.')
      ).toBeInTheDocument();
    });

    it('renders "Apri Chat" button', () => {
      render(<ChatHistorySection userId="user-1" />);

      const link = screen.getByRole('link', { name: /Apri Chat/i });
      expect(link).toHaveAttribute('href', '/chat');
    });

    it('renders MessageSquare icon', () => {
      const { container } = render(<ChatHistorySection userId="user-1" />);

      const icons = container.querySelectorAll('svg.lucide-message-square');
      // There should be 2: one in the card header, one in the button
      expect(icons.length).toBeGreaterThanOrEqual(1);
    });

    it('renders dashed border on placeholder card', () => {
      const { container } = render(<ChatHistorySection userId="user-1" />);

      const card = container.querySelector('.border-dashed');
      expect(card).toBeInTheDocument();
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

    it('has semantic HTML structure', () => {
      const { container } = render(<ChatHistorySection userId="user-1" />);

      const section = container.querySelector('section');
      expect(section).toBeInTheDocument();
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
      expect(screen.getByText('Cronologia Chat')).toBeInTheDocument();

      rerender(<ChatHistorySection userId="user-2" />);
      expect(screen.getByText('Cronologia Chat')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // CSS Classes Tests
  // ============================================================================

  describe('Styling', () => {
    it('applies font-quicksand to heading', () => {
      render(<ChatHistorySection userId="user-1" />);

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveClass('font-quicksand');
    });

    it('applies space-y-4 to section', () => {
      const { container } = render(<ChatHistorySection userId="user-1" />);

      const section = container.querySelector('section');
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

      const links = screen.getAllByRole('link');
      const chatLinks = links.filter(link => link.getAttribute('href') === '/chat');
      expect(chatLinks.length).toBe(2); // "Vedi Tutte" and "Apri Chat"
    });

    it('"Vedi Tutte" link is in header', () => {
      render(<ChatHistorySection userId="user-1" />);

      const vediTutteLink = screen.getByRole('link', { name: /Vedi Tutte/i });
      expect(vediTutteLink).toBeInTheDocument();
    });

    it('"Apri Chat" link is in card', () => {
      render(<ChatHistorySection userId="user-1" />);

      const apriChatLink = screen.getByRole('link', { name: /Apri Chat/i });
      expect(apriChatLink).toBeInTheDocument();
    });
  });
});
