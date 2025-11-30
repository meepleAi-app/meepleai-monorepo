/**
 * Property and Configuration Tests for AccessibleModal (TEST-05 Phase 3d)
 *
 * Size Variations, Close Button Behavior, and Edge Cases
 * Complements accessibility tests in AccessibleModal.a11y.test.tsx
 */

import { render, screen } from '@testing-library/react';
import { AccessibleModal } from '../AccessibleModal';

describe('Feature: Accessible Modal Dialog - Props and Configuration', () => {
  beforeEach(() => {
    // Mock document.body.style for scroll lock tests
    Object.defineProperty(document.body, 'style', {
      writable: true,
      value: { overflow: '' },
    });
  });

  afterEach(() => {
    document.body.style.overflow = '';
  });

  describe('Scenario: Size Prop Variations', () => {
    it('Given size="sm", When modal renders, Then correct size class is applied', () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" size="sm">
          <p>Content</p>
        </AccessibleModal>
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('sm:max-w-[350px]');
    });

    it('Given size="md", When modal renders, Then correct size class is applied', () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" size="md">
          <p>Content</p>
        </AccessibleModal>
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('sm:max-w-[500px]');
    });

    it('Given size="lg", When modal renders, Then correct size class is applied', () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" size="lg">
          <p>Content</p>
        </AccessibleModal>
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('sm:max-w-[700px]');
    });

    it('Given size="xl", When modal renders, Then correct size class is applied', () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" size="xl">
          <p>Content</p>
        </AccessibleModal>
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('sm:max-w-[900px]');
    });

    it('Given size="full", When modal renders, Then correct size class is applied', () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" size="full">
          <p>Content</p>
        </AccessibleModal>
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('sm:max-w-[95vw]');
    });
  });

  describe('Scenario: Close Button Behavior', () => {
    it('Given showCloseButton=true, When modal renders, Then close button is visible', () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" showCloseButton={true}>
          <p>Content</p>
        </AccessibleModal>
      );

      const closeButton = screen.getByRole('button', { name: /close dialog/i });
      expect(closeButton).toBeInTheDocument();
      expect(closeButton).toBeVisible();
    });

    it('Given showCloseButton=false, When modal renders, Then close button is not visible', () => {
      render(
        <AccessibleModal isOpen={true} onClose={vi.fn()} title="Test Modal" showCloseButton={false}>
          <p>Content</p>
        </AccessibleModal>
      );

      const closeButton = screen.queryByRole('button', { name: /close dialog/i });
      expect(closeButton).not.toBeInTheDocument();
    });
  });

  describe('Scenario: Edge Cases', () => {
    it('Given modal with no focusable elements and no close button, When modal opens, Then modal dialog receives focus', async () => {
      render(
        <AccessibleModal
          isOpen={true}
          onClose={vi.fn()}
          title="Test Modal"
          showCloseButton={false}
        >
          <p>Just text, no buttons or inputs</p>
        </AccessibleModal>
      );

      await vi.waitFor(() => {
        const dialog = screen.getByRole('dialog');
        expect(dialog).toHaveFocus();
      }, { timeout: 200 });
    });

    it('Given modal, When rapid open/close cycles occur, Then no errors are thrown', async () => {
      const onClose = vi.fn();
      const { rerender } = render(
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

      expect(document.body.style.overflow).toBe('');
    });

    it('Given custom className prop, When modal renders, Then custom class is applied to dialog', () => {
      render(
        <AccessibleModal
          isOpen={true}
          onClose={vi.fn()}
          title="Test Modal"
          className="custom-modal-class"
        >
          <p>Content</p>
        </AccessibleModal>
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveClass('custom-modal-class');
    });
  });
});
