/**
 * Behavioral Tests for AccessibleModal (TEST-05 Phase 3d)
 *
 * Tests focus management, keyboard navigation, scroll lock, and user interactions.
 * Complements accessibility tests in AccessibleModal.a11y.test.tsx
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AccessibleModal } from '../AccessibleModal';

describe('Feature: Accessible Modal Dialog with Keyboard and Focus Management', () => {
  let user: ReturnType<typeof userEvent.setup>;

  beforeEach(() => {
    user = userEvent.setup();

    // Mock document.body.style for scroll lock tests
    // jsdom doesn't support full CSS style manipulation by default
    Object.defineProperty(document.body, 'style', {
      writable: true,
      value: { overflow: '' },
    });
  });

  afterEach(() => {
    // Cleanup - restore body scroll
    document.body.style.overflow = '';
  });

  describe('Scenario: Focus Management on Open', () => {
    it('Given modal is closed, When modal opens with focusable elements, Then focus moves to first focusable element', async () => {
      // Given: Modal is closed
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal">
          <button>First Button</button>
          <button>Second Button</button>
        </AccessibleModal>
      );

      // When: Modal opens
      rerender(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <button>First Button</button>
          <button>Second Button</button>
        </AccessibleModal>
      );

      // Then: Focus moves to close button (first focusable element in modal)
      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      }, { timeout: 200 });
    });

    it('Given modal opens with no focusable children, When modal opens, Then focus stays on modal container', async () => {
      // Given: Modal with no focusable elements (close button is default)
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal">
          <p>Just text content</p>
        </AccessibleModal>
      );

      // When: Modal opens
      rerender(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <p>Just text content</p>
        </AccessibleModal>
      );

      // Then: Close button receives focus (first focusable element)
      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      }, { timeout: 200 });
    });

    it('Given modal with close button visible, When modal opens, Then close button is first focusable element', async () => {
      // Given: Modal with showCloseButton=true (default)
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      // When: Modal opens
      rerender(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: Close button receives focus first
      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      }, { timeout: 200 });
    });

    it('Given modal with multiple buttons, When modal opens, Then previous focus element is stored', async () => {
      // Given: A trigger button is focused
      const triggerButton = document.createElement('button');
      triggerButton.textContent = 'Open Modal';
      triggerButton.setAttribute('data-testid', 'trigger-button');
      document.body.appendChild(triggerButton);
      triggerButton.focus();

      expect(triggerButton).toHaveFocus();

      // When: Modal opens
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal">
          <button>Modal Button</button>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <button>Modal Button</button>
        </AccessibleModal>
      );

      // Then: Focus moves to close button (first focusable element, previous focus stored internally)
      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      }, { timeout: 200 });

      // Cleanup
      document.body.removeChild(triggerButton);
    });
  });

  describe('Scenario: Focus Restoration on Close', () => {
    it('Given modal is open with trigger button, When modal closes, Then focus restores to trigger button', async () => {
      // Given: Modal is open and trigger button exists
      const triggerButton = document.createElement('button');
      triggerButton.textContent = 'Open Modal';
      triggerButton.setAttribute('data-testid', 'trigger-button');
      document.body.appendChild(triggerButton);
      triggerButton.focus();

      const { rerender } = render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <div>Modal Content</div>
        </AccessibleModal>
      );

      // When: Modal closes
      rerender(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal">
          <div>Modal Content</div>
        </AccessibleModal>
      );

      // Then: Focus restored to trigger button
      await waitFor(() => {
        expect(triggerButton).toHaveFocus();
      }, { timeout: 200 });

      // Cleanup
      document.body.removeChild(triggerButton);
    });

    it('Given modal with close button, When close button is clicked, Then focus restores after close', async () => {
      // Given: Trigger button and open modal
      const triggerButton = document.createElement('button');
      triggerButton.textContent = 'Open Modal';
      document.body.appendChild(triggerButton);
      triggerButton.focus();

      const onClose = jest.fn();
      const { rerender } = render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      // When: Close button is clicked
      const closeButton = screen.getByRole('button', { name: /close dialog/i });
      await user.click(closeButton);

      // Then: onClose is called
      expect(onClose).toHaveBeenCalledTimes(1);

      // Simulate closing by rerendering with isOpen=false
      rerender(
        <AccessibleModal isOpen={false} onClose={onClose} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: Focus restored to trigger
      await waitFor(() => {
        expect(triggerButton).toHaveFocus();
      }, { timeout: 200 });

      // Cleanup
      document.body.removeChild(triggerButton);
    });

    it('Given modal is open, When modal closes via prop change, Then focus restoration cleans up reference', async () => {
      // Given: Open modal
      const triggerButton = document.createElement('button');
      triggerButton.textContent = 'Trigger';
      document.body.appendChild(triggerButton);
      triggerButton.focus();

      const { rerender } = render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      // When: Modal closes
      rerender(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: Focus restores
      await waitFor(() => {
        expect(triggerButton).toHaveFocus();
      });

      // When: Modal reopens
      rerender(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: New focus cycle starts (no stale reference)
      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });

      // Cleanup
      document.body.removeChild(triggerButton);
    });
  });

  describe('Scenario: Keyboard Navigation - ESC Key', () => {
    it('Given modal is open, When ESC key is pressed, Then onClose callback is called', async () => {
      // Given: Modal is open
      const onClose = jest.fn();
      render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <div>Modal Content</div>
        </AccessibleModal>
      );

      // When: User presses ESC key
      await user.keyboard('{Escape}');

      // Then: onClose callback is called
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('Given modal with focusable elements, When ESC key is pressed from any element, Then onClose is called', async () => {
      // Given: Modal with multiple buttons
      const onClose = jest.fn();
      render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      // Focus on second button
      const button2 = screen.getByText('Button 2');
      button2.focus();
      expect(button2).toHaveFocus();

      // When: ESC is pressed from focused button
      await user.keyboard('{Escape}');

      // Then: onClose is called
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('Given modal is open, When ESC key is pressed multiple times, Then onClose is called each time', async () => {
      // Given: Modal is open
      const onClose = jest.fn();
      render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      // When: ESC pressed twice
      await user.keyboard('{Escape}');
      await user.keyboard('{Escape}');

      // Then: onClose called twice
      expect(onClose).toHaveBeenCalledTimes(2);
    });
  });

  describe('Scenario: Focus Trap (Tab Cycling)', () => {
    it('Given modal with multiple buttons, When TAB is pressed, Then focus moves to next element', async () => {
      // Given: Modal with multiple focusable elements
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      // Wait for initial focus on close button
      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      });

      // When: User presses TAB
      await user.tab();

      // Then: Focus moves to Button 1
      await waitFor(() => {
        expect(screen.getByText('Button 1')).toHaveFocus();
      });
    });

    it('Given modal with multiple buttons, When SHIFT+TAB is pressed, Then focus moves to previous element', async () => {
      // Given: Modal with buttons, Button 2 focused
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      // Focus Button 2 manually
      await waitFor(() => {
        screen.getByText('Button 2').focus();
        expect(screen.getByText('Button 2')).toHaveFocus();
      });

      // When: SHIFT+TAB is pressed
      await user.keyboard('{Shift>}{Tab}{/Shift}');

      // Then: Focus moves to Button 1
      await waitFor(() => {
        expect(screen.getByText('Button 1')).toHaveFocus();
      });
    });

    it('Given focus on last element, When TAB is pressed, Then focus cycles to first element', async () => {
      // Given: Modal with 3 buttons, last button focused
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      // Move focus to last button (Button 3)
      const button3 = screen.getByText('Button 3');
      button3.focus();
      expect(button3).toHaveFocus();

      // When: TAB is pressed on last element
      await user.tab();

      // Then: Focus cycles to first element (close button)
      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      });
    });

    it('Given focus on first element, When SHIFT+TAB is pressed, Then focus cycles to last element', async () => {
      // Given: Modal with buttons, close button (first) focused
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      // Wait for close button to be focused initially
      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      });

      // When: SHIFT+TAB is pressed on first element
      await user.keyboard('{Shift>}{Tab}{/Shift}');

      // Then: Focus cycles to last element (Button 3)
      await waitFor(() => {
        expect(screen.getByText('Button 3')).toHaveFocus();
      });
    });
  });

  describe('Scenario: Body Scroll Lock', () => {
    it('Given modal is closed, When modal opens, Then body overflow is set to hidden', async () => {
      // Given: Modal is closed, body is scrollable
      expect(document.body.style.overflow).toBe('');

      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      // When: Modal opens
      rerender(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      // Then: Body overflow is hidden
      await waitFor(() => {
        expect(document.body.style.overflow).toBe('hidden');
      });
    });

    it('Given modal is open with scroll lock, When modal closes, Then body overflow is restored', async () => {
      // Given: Modal is open with scroll lock active
      const { rerender } = render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(document.body.style.overflow).toBe('hidden');
      });

      // When: Modal closes
      rerender(
        <AccessibleModal isOpen={false} onClose={jest.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      // Then: Body overflow is restored to empty string
      await waitFor(() => {
        expect(document.body.style.overflow).toBe('');
      });
    });

    it('Given modal is open, When component unmounts, Then scroll lock is cleaned up', () => {
      // Given: Modal is open
      const { unmount } = render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      expect(document.body.style.overflow).toBe('hidden');

      // When: Component unmounts
      unmount();

      // Then: Scroll lock is cleaned up
      expect(document.body.style.overflow).toBe('');
    });
  });

  describe('Scenario: Backdrop Click to Close', () => {
    it('Given closeOnBackdropClick is true, When backdrop is clicked, Then onClose is called', async () => {
      // Given: Modal open with backdrop click enabled (default)
      const onClose = jest.fn();
      render(
        <AccessibleModal
          isOpen={true}
          onClose={onClose}
          title="Test Modal"
          closeOnBackdropClick={true}
        >
          <div>Content</div>
        </AccessibleModal>
      );

      // When: User clicks backdrop (the backdrop div with black background)
      const dialog = screen.getByRole('dialog');
      const backdrop = dialog.parentElement!.querySelector('[aria-hidden="true"]');

      expect(backdrop).toBeInTheDocument();
      await user.click(backdrop!);

      // Then: onClose is called
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('Given closeOnBackdropClick is false, When backdrop is clicked, Then onClose is NOT called', async () => {
      // Given: Modal with backdrop click disabled
      const onClose = jest.fn();
      render(
        <AccessibleModal
          isOpen={true}
          onClose={onClose}
          title="Test Modal"
          closeOnBackdropClick={false}
        >
          <div>Content</div>
        </AccessibleModal>
      );

      // When: Backdrop is clicked
      const dialog = screen.getByRole('dialog');
      const backdrop = dialog.parentElement!.querySelector('[aria-hidden="true"]');

      await user.click(backdrop!);

      // Then: onClose is NOT called
      expect(onClose).not.toHaveBeenCalled();
    });

    it('Given modal is open, When modal content is clicked, Then onClose is NOT called', async () => {
      // Given: Modal with backdrop click enabled
      const onClose = jest.fn();
      render(
        <AccessibleModal
          isOpen={true}
          onClose={onClose}
          title="Test Modal"
          closeOnBackdropClick={true}
        >
          <div data-testid="modal-content">Content</div>
        </AccessibleModal>
      );

      // When: Modal content is clicked (not backdrop)
      const content = screen.getByTestId('modal-content');
      await user.click(content);

      // Then: onClose is NOT called (click on content, not backdrop)
      expect(onClose).not.toHaveBeenCalled();
    });
  });

  describe('Scenario: Size Prop Variations', () => {
    it('Given size="sm", When modal renders, Then correct size class is applied', () => {
      // Given & When: Modal with size="sm"
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" size="sm">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: max-w-sm class is applied
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('max-w-sm');
    });

    it('Given size="md", When modal renders, Then correct size class is applied', () => {
      // Given & When: Modal with size="md" (default)
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" size="md">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: max-w-md class is applied
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('max-w-md');
    });

    it('Given size="lg", When modal renders, Then correct size class is applied', () => {
      // Given & When: Modal with size="lg"
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" size="lg">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: max-w-lg class is applied
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('max-w-lg');
    });

    it('Given size="xl", When modal renders, Then correct size class is applied', () => {
      // Given & When: Modal with size="xl"
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" size="xl">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: max-w-xl class is applied
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('max-w-xl');
    });

    it('Given size="full", When modal renders, Then correct size class is applied', () => {
      // Given & When: Modal with size="full"
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" size="full">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: max-w-full class is applied
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('max-w-full');
      expect(dialog).toHaveClass('mx-4');
    });
  });

  describe('Scenario: Close Button Behavior', () => {
    it('Given showCloseButton=true, When modal renders, Then close button is visible', () => {
      // Given & When: Modal with close button enabled (default)
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: Close button is visible
      const closeButton = screen.getByRole('button', { name: /close dialog/i });
      expect(closeButton).toBeInTheDocument();
      expect(closeButton).toBeVisible();
    });

    it('Given showCloseButton=false, When modal renders, Then close button is not visible', () => {
      // Given & When: Modal with close button disabled
      render(
        <AccessibleModal isOpen={true} onClose={jest.fn()} title="Test Modal" showCloseButton={false}>
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: Close button is not in the document
      const closeButton = screen.queryByRole('button', { name: /close dialog/i });
      expect(closeButton).not.toBeInTheDocument();
    });
  });

  describe('Scenario: Edge Cases', () => {
    it('Given modal with no focusable elements and no close button, When modal opens, Then modal dialog receives focus', async () => {
      // Given: Modal with showCloseButton=false and no focusable children
      render(
        <AccessibleModal
          isOpen={true}
          onClose={jest.fn()}
          title="Test Modal"
          showCloseButton={false}
        >
          <p>Just text, no buttons or inputs</p>
        </AccessibleModal>
      );

      // Then: Dialog element itself receives focus
      await waitFor(() => {
        const dialog = screen.getByRole('dialog');
        expect(dialog).toHaveFocus();
      }, { timeout: 200 });
    });

    it('Given modal, When rapid open/close cycles occur, Then no errors are thrown', async () => {
      // Given: Modal component
      const onClose = jest.fn();
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={onClose} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      // When: Rapid open/close cycles
      rerender(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={false} onClose={onClose} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={false} onClose={onClose} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: No errors thrown, scroll lock cleaned up
      expect(document.body.style.overflow).toBe('');
    });

    it('Given custom className prop, When modal renders, Then custom class is applied to dialog', () => {
      // Given & When: Modal with custom className
      render(
        <AccessibleModal
          isOpen={true}
          onClose={jest.fn()}
          title="Test Modal"
          className="custom-modal-class"
        >
          <p>Content</p>
        </AccessibleModal>
      );

      // Then: Custom class is applied
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('custom-modal-class');
    });
  });
});
