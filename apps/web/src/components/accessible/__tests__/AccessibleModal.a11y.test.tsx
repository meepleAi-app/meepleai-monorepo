/**
 * Accessibility Tests for AccessibleModal (UI-05)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { AccessibleModal } from '../AccessibleModal';

describe('AccessibleModal - Accessibility', () => {
  it('should have no accessibility violations when open', async () => {
    const { container } = render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test Modal">
        <p>Modal content</p>
      </AccessibleModal>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations with description', async () => {
    const { container } = render(
      <AccessibleModal
        isOpen={true}
        onClose={() => {}}
        title="Confirm Action"
        description="This action cannot be undone."
      >
        <p>Are you sure?</p>
      </AccessibleModal>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations with form content', async () => {
    const { container } = render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Login">
        <form>
          <label htmlFor="email">Email</label>
          <input id="email" type="email" />
          <button type="submit">Submit</button>
        </form>
      </AccessibleModal>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have role="dialog"', () => {
    render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test">
        <p>Content</p>
      </AccessibleModal>
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
  });

  it('should have aria-modal="true"', () => {
    render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test">
        <p>Content</p>
      </AccessibleModal>
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });

  it('should have aria-labelledby pointing to title', () => {
    render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test Title">
        <p>Content</p>
      </AccessibleModal>
    );

    const dialog = screen.getByRole('dialog');
    const ariaLabelledBy = dialog.getAttribute('aria-labelledby');
    expect(ariaLabelledBy).toBeTruthy();

    // Verify the ID points to the title element
    const titleElement = document.getElementById(ariaLabelledBy!);
    expect(titleElement).toHaveTextContent('Test Title');
  });

  it('should have aria-describedby when description is provided', () => {
    render(
      <AccessibleModal
        isOpen={true}
        onClose={() => {}}
        title="Test"
        description="Test description"
      >
        <p>Content</p>
      </AccessibleModal>
    );

    const dialog = screen.getByRole('dialog');
    const ariaDescribedBy = dialog.getAttribute('aria-describedby');
    expect(ariaDescribedBy).toBeTruthy();

    // Verify the ID points to the description element
    const descElement = document.getElementById(ariaDescribedBy!);
    expect(descElement).toHaveTextContent('Test description');
  });

  it('should have accessible close button', () => {
    render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test" showCloseButton={true}>
        <p>Content</p>
      </AccessibleModal>
    );

    const closeButton = screen.getByRole('button', { name: /close dialog/i });
    expect(closeButton).toBeInTheDocument();
    expect(closeButton).toHaveAttribute('aria-label', 'Close dialog');
  });

  it('should not render close button when showCloseButton is false', () => {
    render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test" showCloseButton={false}>
        <p>Content</p>
      </AccessibleModal>
    );

    const closeButton = screen.queryByRole('button', { name: /close dialog/i });
    expect(closeButton).not.toBeInTheDocument();
  });

  it('should not render when isOpen is false', () => {
    render(
      <AccessibleModal isOpen={false} onClose={() => {}} title="Test">
        <p>Content</p>
      </AccessibleModal>
    );

    const dialog = screen.queryByRole('dialog');
    expect(dialog).not.toBeInTheDocument();
  });

  it('should have tabindex="-1" for focus management', () => {
    render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test">
        <p>Content</p>
      </AccessibleModal>
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('tabindex', '-1');
  });

  it('should apply correct size class for different sizes', () => {
    const { rerender } = render(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test" size="sm">
        <p>Content</p>
      </AccessibleModal>
    );

    let dialog = screen.getByRole('dialog');
    expect(dialog).toHaveClass('max-w-sm');

    rerender(
      <AccessibleModal isOpen={true} onClose={() => {}} title="Test" size="lg">
        <p>Content</p>
      </AccessibleModal>
    );

    dialog = screen.getByRole('dialog');
    expect(dialog).toHaveClass('max-w-lg');
  });
});
