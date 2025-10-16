/**
 * Accessibility Tests for AccessibleButton (UI-05)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { AccessibleButton } from '../AccessibleButton';

describe('AccessibleButton - Accessibility', () => {
  it('should have no accessibility violations (primary variant)', async () => {
    const { container } = render(
      <AccessibleButton variant="primary" onClick={() => {}}>
        Click Me
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (secondary variant)', async () => {
    const { container } = render(
      <AccessibleButton variant="secondary" onClick={() => {}}>
        Cancel
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (danger variant)', async () => {
    const { container } = render(
      <AccessibleButton variant="danger" onClick={() => {}}>
        Delete
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (icon-only button with aria-label)', async () => {
    const { container } = render(
      <AccessibleButton iconOnly aria-label="Close dialog" onClick={() => {}}>
        ✕
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (loading state)', async () => {
    const { container } = render(
      <AccessibleButton isLoading loadingText="Saving..." onClick={() => {}}>
        Save
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (toggle button - pressed)', async () => {
    const { container } = render(
      <AccessibleButton isPressed={true} aria-label="Notifications on" onClick={() => {}}>
        🔔
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (toggle button - not pressed)', async () => {
    const { container } = render(
      <AccessibleButton isPressed={false} aria-label="Notifications off" onClick={() => {}}>
        🔕
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (disabled state)', async () => {
    const { container } = render(
      <AccessibleButton disabled onClick={() => {}}>
        Submit
      </AccessibleButton>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have proper ARIA attributes for icon-only button', () => {
    const { getByRole } = render(
      <AccessibleButton iconOnly aria-label="Delete item" onClick={() => {}}>
        🗑️
      </AccessibleButton>
    );

    const button = getByRole('button', { name: /delete item/i });
    expect(button).toHaveAttribute('aria-label', 'Delete item');
  });

  it('should have aria-pressed attribute for toggle buttons', () => {
    const { getByRole } = render(
      <AccessibleButton isPressed={true} aria-label="Toggle" onClick={() => {}}>
        Toggle
      </AccessibleButton>
    );

    const button = getByRole('button', { name: /toggle/i });
    expect(button).toHaveAttribute('aria-pressed', 'true');
  });

  it('should have aria-disabled for disabled buttons', () => {
    const { getByRole } = render(
      <AccessibleButton disabled onClick={() => {}}>
        Disabled
      </AccessibleButton>
    );

    const button = getByRole('button', { name: /disabled/i });
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('should have aria-busy for loading state', () => {
    const { getByRole } = render(
      <AccessibleButton isLoading onClick={() => {}}>
        Save
      </AccessibleButton>
    );

    const button = getByRole('button');
    expect(button).toHaveAttribute('aria-busy', 'true');
  });

  it('should warn in development if icon-only button missing aria-label', () => {
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

    render(
      <AccessibleButton iconOnly onClick={() => {}}>
        ✕
      </AccessibleButton>
    );

    expect(consoleSpy).toHaveBeenCalledWith(
      expect.stringContaining('Icon-only buttons must have an aria-label')
    );

    consoleSpy.mockRestore();
  });
});
