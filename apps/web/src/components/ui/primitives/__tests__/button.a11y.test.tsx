/**
 * Accessibility Tests for Button (UI-01)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { Button } from '../button';

describe('Button - Accessibility', () => {
  it('should have no accessibility violations (default variant)', async () => {
    const { container } = render(
      <Button onClick={() => {}}>
        Click Me
      </Button>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (outline variant)', async () => {
    const { container } = render(
      <Button variant="outline" onClick={() => {}}>
        Outline Button
      </Button>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (destructive variant)', async () => {
    const { container } = render(
      <Button variant="destructive" onClick={() => {}}>
        Delete
      </Button>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (icon-only button with aria-label)', async () => {
    const { container } = render(
      <Button size="icon" aria-label="Close" onClick={() => {}}>
        ✕
      </Button>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (disabled state)', async () => {
    const { container } = render(
      <Button disabled onClick={() => {}}>
        Submit
      </Button>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (small size)', async () => {
    const { container } = render(
      <Button size="sm" onClick={() => {}}>
        Small
      </Button>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper focus ring for keyboard navigation', () => {
    const { getByRole } = render(
      <Button onClick={() => {}}>
        Keyboard Test
      </Button>
    );

    const button = getByRole('button', { name: /keyboard test/i });
    expect(button).toHaveClass('focus-visible:ring-2');
  });

  it('should be disabled when disabled prop is true', () => {
    const { getByRole } = render(
      <Button disabled onClick={() => {}}>
        Disabled Button
      </Button>
    );

    const button = getByRole('button');
    expect(button).toBeDisabled();
  });
});
