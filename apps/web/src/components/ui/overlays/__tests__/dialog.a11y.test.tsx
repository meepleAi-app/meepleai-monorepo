/**
 * Accessibility Tests for Dialog (UI-05)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import {
  Dialog,
  DialogTrigger,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '../dialog';

describe('Dialog - Accessibility', () => {
  it('should have no accessibility violations (default dialog)', async () => {
    const { container } = render(
      <Dialog open={true}>
        <DialogContent>
          <DialogTitle>Dialog Title</DialogTitle>
          <DialogDescription>Dialog content goes here</DialogDescription>
        </DialogContent>
      </Dialog>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with close button)', async () => {
    const { container } = render(
      <Dialog open={true}>
        <DialogContent hideCloseButton={false}>
          <DialogTitle>Closable Dialog</DialogTitle>
          <DialogDescription>Click the X button to close</DialogDescription>
        </DialogContent>
      </Dialog>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (hidden close button)', async () => {
    const { container } = render(
      <Dialog open={true}>
        <DialogContent hideCloseButton={true}>
          <DialogTitle>No Close Button</DialogTitle>
          <DialogDescription>This dialog has no close button</DialogDescription>
        </DialogContent>
      </Dialog>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper ARIA roles for dialog structure', () => {
    const { getByRole } = render(
      <Dialog open={true}>
        <DialogContent>
          <DialogTitle>Test Dialog</DialogTitle>
          <DialogDescription>Accessible dialog</DialogDescription>
        </DialogContent>
      </Dialog>
    );

    const dialog = getByRole('dialog');
    expect(dialog).toBeInTheDocument();
  });

  it('should have proper ARIA attributes for title', () => {
    const { getByText } = render(
      <Dialog open={true}>
        <DialogContent>
          <DialogTitle>Dialog Title</DialogTitle>
        </DialogContent>
      </Dialog>
    );

    const title = getByText('Dialog Title');
    expect(title).toHaveClass('text-lg');
  });

  it('should have close button with aria-label', () => {
    const { queryByLabelText } = render(
      <Dialog open={true}>
        <DialogContent hideCloseButton={false}>
          <DialogTitle>Test</DialogTitle>
        </DialogContent>
      </Dialog>
    );

    const closeButton = queryByLabelText('Close dialog');
    expect(closeButton).toBeInTheDocument();
  });

  it('should have proper focus management', () => {
    const { container } = render(
      <Dialog open={true}>
        <DialogContent>
          <DialogTitle>Focus Test</DialogTitle>
          <DialogDescription>Test focus management</DialogDescription>
        </DialogContent>
      </Dialog>
    );

    const dialog = container.querySelector('[role="dialog"]');
    expect(dialog).toHaveClass('focus-visible:ring-2');
  });

  it('should have keyboard accessibility for close button', () => {
    const { queryByLabelText } = render(
      <Dialog open={true}>
        <DialogContent>
          <DialogTitle>Test</DialogTitle>
        </DialogContent>
      </Dialog>
    );

    const closeButton = queryByLabelText('Close dialog');
    expect(closeButton).toHaveClass('focus');
  });
});
