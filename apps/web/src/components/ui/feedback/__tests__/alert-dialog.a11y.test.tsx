/**
 * Accessibility Tests for AlertDialog (UI-07)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { AlertDialog } from '../alert-dialog';

describe('AlertDialog - Accessibility', () => {
  it('should have no accessibility violations (info variant)', async () => {
    const { container } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Information"
        message="This is an informational alert"
        variant="info"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (success variant)', async () => {
    const { container } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Success"
        message="Operation completed successfully"
        variant="success"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (warning variant)', async () => {
    const { container } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Warning"
        message="Please be careful"
        variant="warning"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (error variant)', async () => {
    const { container } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Error"
        message="An error has occurred"
        variant="error"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (loading variant)', async () => {
    const { container } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Loading"
        message="Please wait..."
        variant="loading"
      />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper dialog role and ARIA attributes', () => {
    const { getByRole } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Test Alert"
        message="Test content"
      />
    );

    const dialog = getByRole('dialog');
    expect(dialog).toBeInTheDocument();
  });

  it('should have accessible title', () => {
    const { getByText } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Alert Title"
        message="Message content"
      />
    );

    const title = getByText('Alert Title');
    expect(title).toBeInTheDocument();
  });

  it('should have accessible button text', () => {
    const { getByRole } = render(
      <AlertDialog
        open={true}
        onOpenChange={() => {}}
        title="Test"
        message="Content"
        buttonText="Confirm"
      />
    );

    const button = getByRole('button');
    expect(button).toHaveTextContent('Confirm');
  });
});
