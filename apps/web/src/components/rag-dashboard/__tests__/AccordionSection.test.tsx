/**
 * Tests for AccordionSection component
 * Issue #3449: Accordion system for collapsible sections
 */

import React from 'react';

import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
    section: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <section {...props}>{children}</section>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Import after mocking
import { AccordionSection, SectionGroup } from '../SectionGroup';

describe('AccordionSection', () => {
  const defaultProps = {
    id: 'test-section',
    title: 'Test Section',
    isOpen: true,
    onToggle: vi.fn(),
    children: <div>Test Content</div>,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render section title', () => {
      render(<AccordionSection {...defaultProps} />);

      expect(screen.getByText('Test Section')).toBeInTheDocument();
    });

    it('should render description when provided', () => {
      render(
        <AccordionSection
          {...defaultProps}
          description="Test description"
        />
      );

      expect(screen.getByText('Test description')).toBeInTheDocument();
    });

    it('should render icon when provided', () => {
      render(
        <AccordionSection
          {...defaultProps}
          icon={<span data-testid="test-icon">🎯</span>}
        />
      );

      expect(screen.getByTestId('test-icon')).toBeInTheDocument();
    });

    it('should render children when open', () => {
      render(<AccordionSection {...defaultProps} isOpen={true} />);

      expect(screen.getByText('Test Content')).toBeInTheDocument();
    });

    it('should not render children when closed', () => {
      render(<AccordionSection {...defaultProps} isOpen={false} />);

      expect(screen.queryByText('Test Content')).not.toBeInTheDocument();
    });

    it('should have correct id attribute', () => {
      render(<AccordionSection {...defaultProps} />);

      expect(document.getElementById('test-section')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Interaction Tests
  // =========================================================================

  describe('Interactions', () => {
    it('should call onToggle when header is clicked', async () => {
      const onToggle = vi.fn();
      const user = userEvent.setup();
      render(<AccordionSection {...defaultProps} onToggle={onToggle} />);

      const button = screen.getByRole('button');
      await user.click(button);

      expect(onToggle).toHaveBeenCalledTimes(1);
    });

    it('should call onToggle when Enter key is pressed', () => {
      const onToggle = vi.fn();
      render(<AccordionSection {...defaultProps} onToggle={onToggle} />);

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: 'Enter' });

      expect(onToggle).toHaveBeenCalledTimes(1);
    });

    it('should call onToggle when Space key is pressed', () => {
      const onToggle = vi.fn();
      render(<AccordionSection {...defaultProps} onToggle={onToggle} />);

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: ' ' });

      expect(onToggle).toHaveBeenCalledTimes(1);
    });

    it('should not call onToggle for other keys', () => {
      const onToggle = vi.fn();
      render(<AccordionSection {...defaultProps} onToggle={onToggle} />);

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: 'Escape' });
      fireEvent.keyDown(button, { key: 'Tab' });

      expect(onToggle).not.toHaveBeenCalled();
    });
  });

  // =========================================================================
  // Accessibility Tests
  // =========================================================================

  describe('Accessibility', () => {
    it('should have aria-expanded attribute', () => {
      const { rerender } = render(
        <AccordionSection {...defaultProps} isOpen={true} />
      );

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-expanded', 'true');

      rerender(<AccordionSection {...defaultProps} isOpen={false} />);
      expect(button).toHaveAttribute('aria-expanded', 'false');
    });

    it('should have aria-controls pointing to content', () => {
      render(<AccordionSection {...defaultProps} />);

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('aria-controls', 'test-section-content');
    });

    it('should have button type attribute', () => {
      render(<AccordionSection {...defaultProps} />);

      const button = screen.getByRole('button');
      expect(button).toHaveAttribute('type', 'button');
    });

    it('should have chevron icon hidden from screen readers', () => {
      render(<AccordionSection {...defaultProps} />);

      const chevron = document.querySelector('[aria-hidden="true"]');
      expect(chevron).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Visual State Tests
  // =========================================================================

  describe('Visual State', () => {
    it('should apply scroll margin class', () => {
      render(<AccordionSection {...defaultProps} />);

      const section = document.getElementById('test-section');
      expect(section).toHaveClass('scroll-mt-24');
    });

    it('should apply custom className', () => {
      render(<AccordionSection {...defaultProps} className="custom-class" />);

      const section = document.getElementById('test-section');
      expect(section).toHaveClass('custom-class');
    });

    it('should render chevron icon', () => {
      render(<AccordionSection {...defaultProps} />);

      // Chevron is rendered as SVG with lucide-react
      const svg = document.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });
});

// =========================================================================
// SectionGroup Tests
// =========================================================================

describe('SectionGroup', () => {
  const defaultProps = {
    id: 'test-group',
    label: 'Test Group',
    icon: '🎯',
    children: <div>Group Content</div>,
  };

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render group label', () => {
      render(<SectionGroup {...defaultProps} />);

      expect(screen.getByText('Test Group')).toBeInTheDocument();
    });

    it('should render group icon', () => {
      render(<SectionGroup {...defaultProps} />);

      expect(screen.getByText('🎯')).toBeInTheDocument();
    });

    it('should render description when provided', () => {
      render(
        <SectionGroup {...defaultProps} description="Test description" />
      );

      expect(screen.getByText('Test description')).toBeInTheDocument();
    });

    it('should render children', () => {
      render(<SectionGroup {...defaultProps} />);

      expect(screen.getByText('Group Content')).toBeInTheDocument();
    });

    it('should have correct id attribute', () => {
      render(<SectionGroup {...defaultProps} />);

      expect(document.getElementById('test-group')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Accessibility Tests
  // =========================================================================

  describe('Accessibility', () => {
    it('should have aria-labelledby pointing to heading', () => {
      render(<SectionGroup {...defaultProps} />);

      const section = document.getElementById('test-group');
      expect(section).toHaveAttribute('aria-labelledby', 'test-group-heading');
    });

    it('should have heading with correct id', () => {
      render(<SectionGroup {...defaultProps} />);

      const heading = document.getElementById('test-group-heading');
      expect(heading).toBeInTheDocument();
      expect(heading?.tagName).toBe('H2');
    });

    it('should have icon hidden from screen readers', () => {
      render(<SectionGroup {...defaultProps} />);

      const iconContainer = document.querySelector('[aria-hidden="true"]');
      expect(iconContainer).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Styling Tests
  // =========================================================================

  describe('Styling', () => {
    it('should apply scroll margin class', () => {
      render(<SectionGroup {...defaultProps} />);

      const section = document.getElementById('test-group');
      expect(section).toHaveClass('scroll-mt-24');
    });

    it('should apply custom className', () => {
      render(<SectionGroup {...defaultProps} className="custom-class" />);

      const section = document.getElementById('test-group');
      expect(section).toHaveClass('custom-class');
    });
  });
});
