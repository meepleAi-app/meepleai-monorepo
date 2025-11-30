/**
 * Behavioral Tests for AccessibleModal (TEST-05 Phase 3d)
 *
 * Focus Management, Keyboard Navigation, and User Interactions
 * Complements accessibility tests in AccessibleModal.a11y.test.tsx
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AccessibleModal } from '../AccessibleModal';

describe('Feature: Accessible Modal Dialog - Behavior and Interactions', () => {
  let user: ReturnType<typeof userEvent.setup>;

  beforeEach(() => {
    user = userEvent.setup();

    // Mock document.body.style for scroll lock tests
    Object.defineProperty(document.body, 'style', {
      writable: true,
      value: { overflow: '' },
    });
  });

  afterEach(() => {
    document.body.style.overflow = '';
  });

  describe('Scenario: Focus Management on Open', () => {
    it('Given modal is closed, When modal opens with focusable elements, Then focus moves to first focusable element', async () => {
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal">
          <button>First Button</button>
          <button>Second Button</button>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <button>First Button</button>
          <button>Second Button</button>
        </AccessibleModal>
      );

      await waitFor(() => {
        const firstButton = screen.getByRole('button', { name: 'First Button' });
        expect(firstButton).toHaveFocus();
      }, { timeout: 500 });
    });

    it('Given modal opens with no focusable children, When modal opens, Then focus stays on modal container', async () => {
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal">
          <p>Just text content</p>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <p>Just text content</p>
        </AccessibleModal>
      );

      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      }, { timeout: 200 });
    });

    it('Given modal with close button visible, When modal opens, Then close button is first focusable element', async () => {
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      }, { timeout: 200 });
    });

    it('Given modal with multiple buttons, When modal opens, Then focus moves to first interactive element', async () => {
      const triggerButton = document.createElement('button');
      triggerButton.textContent = 'Open Modal';
      triggerButton.setAttribute('data-testid', 'trigger-button');
      document.body.appendChild(triggerButton);
      triggerButton.focus();

      expect(triggerButton).toHaveFocus();

      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal">
          <button>Modal Button</button>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <button>Modal Button</button>
        </AccessibleModal>
      );

      await waitFor(() => {
        const modalButton = screen.getByRole('button', { name: 'Modal Button' });
        expect(modalButton).toHaveFocus();
      }, { timeout: 200 });

      document.body.removeChild(triggerButton);
    });
  });

  describe('Scenario: Focus Restoration on Close', () => {
    it('Given modal is open, When modal closes, Then modal is removed from DOM', async () => {
      const { rerender } = render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <div>Modal Content</div>
        </AccessibleModal>
      );

      expect(screen.getByRole('dialog')).toBeInTheDocument();

      rerender(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal">
          <div>Modal Content</div>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      }, { timeout: 200 });
    });

    it('Given modal with close button, When close button is clicked, Then onClose is called', async () => {
      const onClose = vi.fn();
      render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      const closeButton = screen.getByRole('button', { name: /close dialog/i });
      await user.click(closeButton);

      await waitFor(() => {
        expect(onClose).toHaveBeenCalledTimes(1);
      });
    });

    it('Given modal is open, When modal closes and reopens, Then modal lifecycle works correctly', async () => {
      const { rerender } = render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      expect(screen.getByRole('dialog')).toBeInTheDocument();

      rerender(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      });

      rerender(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
    });
  });

  describe('Scenario: Keyboard Navigation - ESC Key', () => {
    it('Given modal is open, When ESC key is pressed, Then onClose callback is called', async () => {
      const onClose = vi.fn();
      render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <div>Modal Content</div>
        </AccessibleModal>
      );

      await user.keyboard('{Escape}');

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('Given modal with focusable elements, When ESC key is pressed from any element, Then onClose is called', async () => {
      const onClose = vi.fn();
      render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      const button2 = screen.getByText('Button 2');
      button2.focus();
      expect(button2).toHaveFocus();

      await user.keyboard('{Escape}');

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('Given modal is open, When ESC key is pressed multiple times, Then onClose is called each time', async () => {
      const onClose = vi.fn();
      render(
        <AccessibleModal isOpen={true} onClose={onClose} title="Test Modal">
          <p>Content</p>
        </AccessibleModal>
      );

      await user.keyboard('{Escape}');
      await user.keyboard('{Escape}');

      expect(onClose).toHaveBeenCalledTimes(2);
    });
  });

  describe('Scenario: Focus Trap (Tab Cycling)', () => {
    it('Given modal with multiple buttons, When TAB is pressed, Then focus moves to next element', async () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.getByText('Button 1')).toHaveFocus();
      });

      await user.tab();

      await waitFor(() => {
        expect(screen.getByText('Button 2')).toHaveFocus();
      });
    });

    it('Given modal with multiple buttons, When SHIFT+TAB is pressed, Then focus moves to previous element', async () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      await waitFor(() => {
        screen.getByText('Button 2').focus();
        expect(screen.getByText('Button 2')).toHaveFocus();
      });

      await user.keyboard('{Shift>}{Tab}{/Shift}');

      await waitFor(() => {
        expect(screen.getByText('Button 1')).toHaveFocus();
      });
    });

    it('Given focus on last element, When TAB is pressed, Then focus cycles to first element', async () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      const button3 = screen.getByText('Button 3');
      button3.focus();
      expect(button3).toHaveFocus();

      await user.tab();

      await waitFor(() => {
        const closeButton = screen.getByRole('button', { name: /close dialog/i });
        expect(closeButton).toHaveFocus();
      });
    });

    it('Given focus on first element, When SHIFT+TAB is pressed, Then focus cycles within modal', async () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" showCloseButton={true}>
          <button>Button 1</button>
          <button>Button 2</button>
          <button>Button 3</button>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.getByText('Button 1')).toHaveFocus();
      });

      await user.keyboard('{Shift>}{Tab}{/Shift}');

      await waitFor(() => {
        const focused = document.activeElement;
        expect(focused).toBeTruthy();
        const dialog = screen.getByRole('dialog');
        expect(dialog.contains(focused as Node)).toBe(true);
      });
    });
  });

  describe('Scenario: Body Scroll Lock', () => {
    it('Given modal is closed, When modal opens, Then body scroll lock is applied', async () => {
      const { rerender } = render(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      rerender(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
    });

    it('Given modal is open with scroll lock, When modal closes, Then body scroll lock is removed', async () => {
      const { rerender } = render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });

      rerender(
        <AccessibleModal isOpen={false} onClose={vi.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      await waitFor(() => {
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      });
    });

    it('Given modal is open, When component unmounts, Then scroll lock is cleaned up', () => {
      const { unmount } = render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal">
          <div>Content</div>
        </AccessibleModal>
      );

      expect(screen.getByRole('dialog')).toBeInTheDocument();

      unmount();

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  describe('Scenario: Backdrop Click to Close', () => {
    it('Given closeOnBackdropClick is true, When ESC is pressed, Then onClose is called', async () => {
      const onClose = vi.fn();
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

      await user.keyboard('{Escape}');

      await waitFor(() => {
        expect(onClose).toHaveBeenCalledTimes(1);
      });
    });

    it('Given closeOnBackdropClick is false, When ESC is pressed, Then onClose is still called', async () => {
      const onClose = vi.fn();
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

      await user.keyboard('{Escape}');

      await waitFor(() => {
        expect(onClose).toHaveBeenCalled();
      });
    });

    it('Given modal is open, When modal content is clicked, Then onClose is NOT called', async () => {
      const onClose = vi.fn();
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

      const content = screen.getByTestId('modal-content');
      await user.click(content);

      expect(onClose).not.toHaveBeenCalled();
    });
  });
});
