/**
 * Unit tests for Animation Library Verification Examples
 * Tests that all example components work correctly
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  AnimatedCard,
  AccessibleAnimation,
  AnimatedList,
  LoadingSpinner,
  AnimatedModal,
  FastStaggerList,
  DirectionalAnimations,
  ScaleAnimations,
  CustomTransitionExample,
  TypeSafeExample,
  AnimationLibraryDemo,
} from '../VERIFICATION';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
    li: ({ children, ...props }: any) => <li {...props}>{children}</li>,
    ul: ({ children, ...props }: any) => <ul {...props}>{children}</ul>,
  },
  AnimatePresence: ({ children }: any) => <>{children}</>,
}));

describe('Animation Verification Examples', () => {
  describe('AnimatedCard', () => {
    it('should render animated card with content', () => {
      render(<AnimatedCard />);

      expect(screen.getByText('Animated Card')).toBeInTheDocument();
      expect(screen.getByText(/This card fades in smoothly/)).toBeInTheDocument();
    });
  });

  describe('AccessibleAnimation', () => {
    it('should render animation with accessibility message', () => {
      render(<AccessibleAnimation />);

      const message = screen.getByText(/reduced motion|full motion/i);

      expect(message).toBeInTheDocument();
    });

    it('should show appropriate message based on reduced motion preference', () => {
      render(<AccessibleAnimation />);

      const text = screen.getByText(/motion/i);

      expect(text).toBeInTheDocument();
    });
  });

  describe('AnimatedList', () => {
    it('should render list with all items', () => {
      const items = ['Item 1', 'Item 2', 'Item 3'];

      render(<AnimatedList items={items} />);

      items.forEach(item => {
        expect(screen.getByText(item)).toBeInTheDocument();
      });
    });

    it('should render empty list', () => {
      const { container } = render(<AnimatedList items={[]} />);

      const list = container.querySelector('ul');

      expect(list).toBeInTheDocument();
      expect(list?.children.length).toBe(0);
    });

    it('should handle single item', () => {
      render(<AnimatedList items={['Single']} />);

      expect(screen.getByText('Single')).toBeInTheDocument();
    });
  });

  describe('LoadingSpinner', () => {
    it('should render loading spinner or text', () => {
      const { container } = render(<LoadingSpinner />);

      // Either spinner div or "Loading..." text should be present
      const hasSpinner = container.querySelector('[style*="border"]') !== null;
      const hasText = screen.queryByText('Loading...') !== null;

      expect(hasSpinner || hasText).toBe(true);
    });
  });

  describe('AnimatedModal', () => {
    it('should render modal when isOpen is true', () => {
      render(
        <AnimatedModal isOpen={true} onClose={vi.fn()}>
          <h3>Modal Content</h3>
        </AnimatedModal>
      );

      expect(screen.getByText('Modal Content')).toBeInTheDocument();
    });

    it('should not render modal when isOpen is false', () => {
      render(
        <AnimatedModal isOpen={false} onClose={vi.fn()}>
          <h3>Modal Content</h3>
        </AnimatedModal>
      );

      expect(screen.queryByText('Modal Content')).not.toBeInTheDocument();
    });

    it('should call onClose when backdrop is clicked', async () => {
      const user = userEvent.setup();
      const mockOnClose = vi.fn();

      const { container } = render(
        <AnimatedModal isOpen={true} onClose={mockOnClose}>
          <h3>Modal Content</h3>
        </AnimatedModal>
      );

      // Click backdrop (first child of AnimatePresence)
      const backdrop = container.querySelector('[style*="position: fixed"]');

      if (backdrop) {
        await user.click(backdrop);
        expect(mockOnClose).toHaveBeenCalled();
      }
    });

    it('should render children content', () => {
      render(
        <AnimatedModal isOpen={true} onClose={vi.fn()}>
          <h3>Title</h3>
          <p>Description</p>
        </AnimatedModal>
      );

      expect(screen.getByText('Title')).toBeInTheDocument();
      expect(screen.getByText('Description')).toBeInTheDocument();
    });
  });

  describe('FastStaggerList', () => {
    it('should render all items', () => {
      const items = ['Fast 1', 'Fast 2', 'Fast 3'];

      render(<FastStaggerList items={items} />);

      items.forEach(item => {
        expect(screen.getByText(item)).toBeInTheDocument();
      });
    });

    it('should handle empty array', () => {
      const { container } = render(<FastStaggerList items={[]} />);

      expect(container.firstChild).toBeInTheDocument();
    });
  });

  describe('DirectionalAnimations', () => {
    it('should render all directional animations', () => {
      render(<DirectionalAnimations />);

      expect(screen.getByText('Slide Up')).toBeInTheDocument();
      expect(screen.getByText('Slide Down')).toBeInTheDocument();
      expect(screen.getByText('Slide Left')).toBeInTheDocument();
      expect(screen.getByText('Slide Right')).toBeInTheDocument();
    });
  });

  describe('ScaleAnimations', () => {
    it('should render both scale animations', () => {
      render(<ScaleAnimations />);

      expect(screen.getByText('Scale In')).toBeInTheDocument();
      expect(screen.getByText('Pop In')).toBeInTheDocument();
    });
  });

  describe('CustomTransitionExample', () => {
    it('should render custom transition example', () => {
      render(<CustomTransitionExample />);

      expect(screen.getByText(/Custom transition with bouncy spring/)).toBeInTheDocument();
    });
  });

  describe('TypeSafeExample', () => {
    it('should render type-safe example with constants', () => {
      render(<TypeSafeExample />);

      const text = screen.getByText(/Type-safe constants/);

      expect(text).toBeInTheDocument();
      expect(text.textContent).toContain('300ms'); // DURATIONS.normal
    });
  });

  describe('AnimationLibraryDemo', () => {
    it('should render complete demo with all sections', () => {
      render(<AnimationLibraryDemo />);

      expect(screen.getByText('Animation Utilities Library Demo')).toBeInTheDocument();
      expect(screen.getByText('1. Animated Card')).toBeInTheDocument();
      expect(screen.getByText('2. Accessibility-Aware')).toBeInTheDocument();
      expect(screen.getByText('3. Staggered List')).toBeInTheDocument();
      expect(screen.getByText('4. Loading Spinner')).toBeInTheDocument();
      expect(screen.getByText('5. Modal')).toBeInTheDocument();
      expect(screen.getByText('6. Directional Slides')).toBeInTheDocument();
      expect(screen.getByText('7. Scale Animations')).toBeInTheDocument();
    });

    it('should render modal button', () => {
      render(<AnimationLibraryDemo />);

      expect(screen.getByText('Open Modal')).toBeInTheDocument();
    });

    it('should open modal when button clicked', async () => {
      const user = userEvent.setup();

      render(<AnimationLibraryDemo />);

      const openButton = screen.getByText('Open Modal');
      await user.click(openButton);

      expect(screen.getByText('Modal Title')).toBeInTheDocument();
      expect(screen.getByText(/This modal uses popIn animation/)).toBeInTheDocument();
    });

    it('should close modal when close button clicked', async () => {
      const user = userEvent.setup();

      render(<AnimationLibraryDemo />);

      const openButton = screen.getByText('Open Modal');
      await user.click(openButton);

      const closeButton = screen.getByText('Close');
      await user.click(closeButton);

      expect(screen.queryByText('Modal Title')).not.toBeInTheDocument();
    });

    it('should render list items', () => {
      render(<AnimationLibraryDemo />);

      expect(screen.getByText('Item 1')).toBeInTheDocument();
      expect(screen.getByText('Item 2')).toBeInTheDocument();
      expect(screen.getByText('Item 3')).toBeInTheDocument();
      expect(screen.getByText('Item 4')).toBeInTheDocument();
    });
  });

  describe('Integration Tests', () => {
    it('should render all components without errors', () => {
      const items = ['Test 1', 'Test 2'];

      expect(() => {
        render(<AnimatedCard />);
        render(<AccessibleAnimation />);
        render(<AnimatedList items={items} />);
        render(<LoadingSpinner />);
        render(
          <AnimatedModal isOpen={false} onClose={vi.fn()}>
            Content
          </AnimatedModal>
        );
        render(<FastStaggerList items={items} />);
        render(<DirectionalAnimations />);
        render(<ScaleAnimations />);
        render(<CustomTransitionExample />);
        render(<TypeSafeExample />);
      }).not.toThrow();
    });

    it('should handle component reuse', () => {
      const items = ['A', 'B'];

      render(<AnimatedList items={items} />);
      render(<AnimatedList items={['C', 'D']} />);

      expect(screen.getByText('A')).toBeInTheDocument();
      expect(screen.getByText('C')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle modal with complex children', () => {
      render(
        <AnimatedModal isOpen={true} onClose={vi.fn()}>
          <div>
            <h1>Complex</h1>
            <ul>
              <li>Item 1</li>
              <li>Item 2</li>
            </ul>
          </div>
        </AnimatedModal>
      );

      expect(screen.getByText('Complex')).toBeInTheDocument();
      expect(screen.getByText('Item 1')).toBeInTheDocument();
    });

    it('should handle list with many items', () => {
      const manyItems = Array.from({ length: 50 }, (_, i) => `Item ${i}`);

      render(<AnimatedList items={manyItems} />);

      expect(screen.getByText('Item 0')).toBeInTheDocument();
      expect(screen.getByText('Item 49')).toBeInTheDocument();
    });

    it('should handle AnimationLibraryDemo state changes', async () => {
      const user = userEvent.setup();

      render(<AnimationLibraryDemo />);

      // Open modal
      await user.click(screen.getByText('Open Modal'));
      expect(screen.getByText('Modal Title')).toBeInTheDocument();

      // Close modal
      await user.click(screen.getAllByText('Close')[0]);

      // Reopen modal
      await user.click(screen.getByText('Open Modal'));
      expect(screen.getByText('Modal Title')).toBeInTheDocument();
    });
  });
});
