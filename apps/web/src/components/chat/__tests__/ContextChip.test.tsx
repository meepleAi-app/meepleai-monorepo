/**
 * ContextChip Component - Unit Tests
 *
 * @issue #1840 (PAGE-004)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ContextChip, type DocumentSource } from '../ContextChip';

describe('ContextChip', () => {
  const defaultProps = {
    gameName: 'Catan',
    gameEmoji: '🎲',
  };

  describe('Rendering', () => {
    it('should render game name with default emoji', () => {
      render(<ContextChip gameName="Catan" />);

      expect(screen.getByText(/Context:/i)).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByRole('img', { name: /game icon/i })).toHaveTextContent('🎲');
    });

    it('should render with custom emoji', () => {
      render(<ContextChip gameName="Wingspan" gameEmoji="🦅" />);

      expect(screen.getByRole('img', { name: /game icon/i })).toHaveTextContent('🦅');
    });

    it('should render without remove button when onRemove not provided', () => {
      render(<ContextChip {...defaultProps} />);

      expect(screen.queryByRole('button', { name: /remove/i })).not.toBeInTheDocument();
    });

    it('should render remove button when onRemove provided', () => {
      const onRemove = vi.fn();
      render(<ContextChip {...defaultProps} onRemove={onRemove} />);

      expect(screen.getByRole('button', { name: /remove catan context/i })).toBeInTheDocument();
    });

    it('should render without sources when empty array', () => {
      render(<ContextChip {...defaultProps} sources={[]} />);

      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('should render sources when provided', () => {
      const sources: DocumentSource[] = [
        { type: 'PDF', count: 1 },
        { type: 'FAQ', count: 15 },
        { type: 'Wiki' },
      ];

      render(<ContextChip {...defaultProps} sources={sources} />);

      expect(screen.getByText('PDF 1')).toBeInTheDocument();
      expect(screen.getByText('FAQ 15')).toBeInTheDocument();
      expect(screen.getByText('Wiki')).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('should call onRemove when remove button clicked', async () => {
      const user = userEvent.setup();
      const onRemove = vi.fn();

      render(<ContextChip {...defaultProps} onRemove={onRemove} />);

      const removeButton = screen.getByRole('button', { name: /remove catan context/i });
      await user.click(removeButton);

      expect(onRemove).toHaveBeenCalledTimes(1);
    });

    it('should not throw when remove button clicked without onRemove', async () => {
      const user = userEvent.setup();
      render(<ContextChip {...defaultProps} />);

      // Should not crash - button should not exist
      const removeButton = screen.queryByRole('button', { name: /remove/i });
      expect(removeButton).not.toBeInTheDocument();
    });
  });

  describe('Source Badges', () => {
    it('should render PDF badge with orange styling', () => {
      const sources: DocumentSource[] = [{ type: 'PDF', count: 1 }];
      render(<ContextChip {...defaultProps} sources={sources} />);

      const badge = screen.getByText('PDF 1');
      expect(badge).toHaveClass('bg-orange-100', 'text-orange-700', 'border-orange-300');
    });

    it('should render FAQ badge with blue styling', () => {
      const sources: DocumentSource[] = [{ type: 'FAQ', count: 10 }];
      render(<ContextChip {...defaultProps} sources={sources} />);

      const badge = screen.getByText('FAQ 10');
      expect(badge).toHaveClass('bg-blue-100', 'text-blue-700', 'border-blue-300');
    });

    it('should render Wiki badge with green styling', () => {
      const sources: DocumentSource[] = [{ type: 'Wiki' }];
      render(<ContextChip {...defaultProps} sources={sources} />);

      const badge = screen.getByText('Wiki');
      expect(badge).toHaveClass('bg-green-100', 'text-green-700', 'border-green-300');
    });

    it('should render source without count', () => {
      const sources: DocumentSource[] = [{ type: 'PDF' }];
      render(<ContextChip {...defaultProps} sources={sources} />);

      expect(screen.getByText('PDF')).toBeInTheDocument();
      expect(screen.queryByText(/PDF \d+/)).not.toBeInTheDocument();
    });

    it('should handle source with count of 0', () => {
      const sources: DocumentSource[] = [{ type: 'FAQ', count: 0 }];
      render(<ContextChip {...defaultProps} sources={sources} />);

      // Count 0 should not display count
      expect(screen.getByText('FAQ')).toBeInTheDocument();
      expect(screen.queryByText('FAQ 0')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have region role with aria-label', () => {
      render(<ContextChip {...defaultProps} />);

      const region = screen.getByRole('region', { name: /game context: catan/i });
      expect(region).toBeInTheDocument();
    });

    it('should have accessible remove button', () => {
      const onRemove = vi.fn();
      render(<ContextChip {...defaultProps} onRemove={onRemove} />);

      const button = screen.getByRole('button', { name: /remove catan context/i });
      expect(button).toHaveAttribute('title', 'Remove context');
    });

    it('should have accessible game icon', () => {
      render(<ContextChip {...defaultProps} />);

      const icon = screen.getByRole('img', { name: /game icon/i });
      expect(icon).toBeInTheDocument();
    });

    it('should have accessible source badges', () => {
      const sources: DocumentSource[] = [
        { type: 'PDF', count: 1 },
        { type: 'FAQ', count: 15 },
      ];

      render(<ContextChip {...defaultProps} sources={sources} />);

      const pdfBadge = screen.getByRole('status', { name: /pdf source: 1 documents/i });
      expect(pdfBadge).toBeInTheDocument();

      const faqBadge = screen.getByRole('status', { name: /faq source: 15 documents/i });
      expect(faqBadge).toBeInTheDocument();
    });
  });

  describe('Custom Styling', () => {
    it('should apply custom className', () => {
      render(<ContextChip {...defaultProps} className="custom-class" />);

      const chip = screen.getByRole('region');
      expect(chip).toHaveClass('custom-class');
    });

    it('should preserve base classes with custom className', () => {
      render(<ContextChip {...defaultProps} className="custom-class" />);

      const chip = screen.getByRole('region');
      expect(chip).toHaveClass('bg-accent/10', 'border-accent', 'custom-class');
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long game names with truncation', () => {
      const longName = 'A Very Long Board Game Name That Should Be Truncated';
      const { container } = render(<ContextChip gameName={longName} />);

      // Check that truncate class exists on the container (flex-1 element)
      const truncateElement = container.querySelector('.truncate');
      expect(truncateElement).toBeTruthy();
      expect(truncateElement).toHaveTextContent(longName);
    });

    it('should handle multiple sources of same type', () => {
      const sources: DocumentSource[] = [
        { type: 'PDF', count: 1 },
        { type: 'PDF', count: 2 },
      ];

      render(<ContextChip {...defaultProps} sources={sources} />);

      const pdfBadges = screen.getAllByText(/PDF/);
      expect(pdfBadges).toHaveLength(2);
    });

    it('should render with empty game name', () => {
      render(<ContextChip gameName="" />);

      expect(screen.getByText(/Context:/i)).toBeInTheDocument();
    });
  });
});
