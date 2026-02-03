/**
 * Accessibility Tests for ConfirmationDialog (UI-08)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { ConfirmationDialog } from '../confirmation-dialog';

describe('ConfirmationDialog - Accessibility', () => {
  it('should have no accessibility violations (default variant)', async () => {
    const { container } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Confirm Action"
        message="Are you sure you want to proceed?"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (destructive variant)', async () => {
    const { container } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Delete Item"
        message="This action cannot be undone"
        variant="destructive"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (warning variant)', async () => {
    const { container } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Warning"
        message="Please review before continuing"
        variant="warning"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (with custom button text)', async () => {
    const { container } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Confirm"
        message="Proceed?"
        confirmText="Continue"
        cancelText="Dismiss"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper dialog role', () => {
    const { getByRole } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Test Dialog"
        message="Test message"
      />
    );

    const dialog = getByRole('dialog');
    expect(dialog).toBeInTheDocument();
  });

  it('should have accessible title', () => {
    const { getByText } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Confirmation Title"
        message="Confirmation message"
      />
    );

    const title = getByText('Confirmation Title');
    expect(title).toBeInTheDocument();
  });

  it('should have accessible buttons', () => {
    const { getAllByRole } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Confirm"
        message="Proceed with action?"
        confirmText="Yes"
        cancelText="No"
      />
    );

    const buttons = getAllByRole('button');
    expect(buttons.length).toBeGreaterThanOrEqual(2);
  });

  it('should have proper keyboard accessibility', () => {
    const { getByRole } = render(
      <ConfirmationDialog
        isOpen={true}
        onClose={() => {}}
        onConfirm={() => {}}
        title="Test"
        message="Test"
      />
    );

    const dialog = getByRole('dialog');
    expect(dialog).toHaveClass('focus-visible:ring-2');
  });
});
